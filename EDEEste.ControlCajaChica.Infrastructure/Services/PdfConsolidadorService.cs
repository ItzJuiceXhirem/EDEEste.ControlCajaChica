using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Entities;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace EDEEste.ControlCajaChica.Infrastructure.Services
{
    /// <summary>
    /// Arma el expediente PDF de una solicitud de reposicion: una portada-resumen con
    /// la tabla de gastos, seguida, por cada comprobante adjunto, de una pagina de
    /// transcripcion (los datos del gasto + la etiqueta que digito el custodio) y
    /// despues el archivo original.
    ///
    /// Decide QUE va en el expediente y en que orden -- el COMO se dibuja cada pieza
    /// con MigraDoc vive en <see cref="ExpedienteMaquetador"/>, separado a proposito:
    /// un cambio de diseno visual no deberia tener que tocar esta logica de
    /// ensamblaje, y viceversa.
    ///
    /// MigraDoc genera cada pieza (paginas nuevas, texto, imagenes) pero no puede
    /// insertar paginas de un PDF que no genero el; por eso el ensamblaje final usa
    /// PDFsharp solo para concatenar paginas ya generadas -- el PDF original de cada
    /// comprobante se copia tal cual, nunca se reabre para editarlo ni se
    /// re-renderiza, asi que el archivo guardado en disco (y su HashSHA256) no se
    /// tocan en ningun momento.
    /// </summary>
    public class PdfConsolidadorService : IPdfConsolidadorService
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IIdentityService _identityService;

        public PdfConsolidadorService(IFileStorageService fileStorageService, IIdentityService identityService)
        {
            _fileStorageService = fileStorageService;
            _identityService = identityService;
        }

        public async Task<byte[]> ConsolidarComprobantesAsync(SolicitudReposicion solicitud, CancellationToken cancellationToken = default)
        {
            using var documentoFinal = new PdfDocument();

            var nombreCustodio = await ResolverNombreCustodioAsync(solicitud.FondoCajaChica?.CustodioId);
            AgregarPaginas(documentoFinal, ExpedienteMaquetador.GenerarResumen(solicitud, nombreCustodio));

            foreach (var gasto in solicitud.Gastos)
            {
                foreach (var comprobante in gasto.Comprobantes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AgregarComprobante(documentoFinal, gasto, comprobante);
                }
            }

            EstamparNumerosDePagina(documentoFinal);

            using var salida = new MemoryStream();
            // closeStream: false a proposito: por defecto Save() cierra el stream que
            // recibe, y si lo cerrara aca, salida.ToArray() de la linea siguiente
            // fallaria con ObjectDisposedException porque el stream ya no existiria.
            documentoFinal.Save(salida, closeStream: false);
            return salida.ToArray();
        }

        /// <summary>
        /// Numera el expediente completo al final, cuando ya se sabe cuantas paginas
        /// tiene.
        ///
        /// No se puede hacer con un footer de MigraDoc: el documento se arma juntando
        /// varios PDF chicos independientes (y los originales de los comprobantes, que
        /// ni siquiera generamos nosotros), asi que cada pieza numeraria desde 1 por su
        /// cuenta. Para un expediente contable la numeracion continua importa: es lo
        /// que permite afirmar que no falta ninguna hoja.
        /// </summary>
        private static void EstamparNumerosDePagina(PdfDocument documento)
        {
            var fuente = new XFont(ResolutorFuentesEmbebidas.NombreFamilia, 8, XFontStyleEx.Regular);
            var total = documento.PageCount;

            for (var indice = 0; indice < total; indice++)
            {
                var pagina = documento.Pages[indice];

                // Append dibuja encima del contenido que ya trae la pagina, sin
                // reescribirlo: las paginas importadas de un PDF ajeno quedan intactas.
                using var lienzo = XGraphics.FromPdfPage(pagina, XGraphicsPdfPageOptions.Append);

                var area = new XRect(0, pagina.Height.Point - 25, pagina.Width.Point, 15);
                lienzo.DrawString(
                    $"Pagina {indice + 1} de {total}",
                    fuente,
                    XBrushes.Gray,
                    area,
                    XStringFormats.TopCenter);
            }
        }

        private async Task<string> ResolverNombreCustodioAsync(string? custodioId)
        {
            if (string.IsNullOrWhiteSpace(custodioId))
            {
                return "(sin custodio asignado)";
            }

            var nombre = await _identityService.ObtenerNombreUsuarioAsync(custodioId);
            return nombre ?? custodioId;
        }

        private void AgregarComprobante(PdfDocument documentoFinal, Gasto gasto, ComprobanteAdjunto comprobante)
        {
            var rutaFisica = _fileStorageService.ObtenerRutaFisica(comprobante.RutaArchivo);
            var esImagen = comprobante.TipoMime.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

            var portada = ExpedienteMaquetador.NuevoDocumento();
            ExpedienteMaquetador.ComponerPortada(portada.LastSection, gasto, comprobante);
            AgregarPaginas(documentoFinal, ExpedienteMaquetador.Renderizar(portada));

            if (esImagen)
            {
                var imagen = ExpedienteMaquetador.NuevoDocumento();
                ExpedienteMaquetador.ComponerImagen(imagen.LastSection, rutaFisica);
                AgregarPaginas(documentoFinal, ExpedienteMaquetador.Renderizar(imagen));
                return;
            }

            if (!File.Exists(rutaFisica))
            {
                AgregarPaginas(documentoFinal, ExpedienteMaquetador.GenerarAviso("El comprobante no existe en el servidor."));
                return;
            }

            // Un PDF guardado pero truncado o corrupto (por ejemplo, una subida
            // cortada por un corte de red) pasa el chequeo de magic bytes de
            // ValidadorComprobante -- que solo mira que el archivo EMPIECE con "%PDF"
            // -- pero PdfReader.Open no puede parsearlo despues. Sin este try/catch,
            // CrearSolicitudReposicionHandler genera este PDF ANTES de guardar la
            // solicitud, asi que un solo comprobante ilegible tumbaba la reposicion
            // completa; y el custodio no tiene como resolverlo por su cuenta, porque
            // ComprobanteAdjunto ya esta firmado con HMAC y es inmutable. Se prefiere
            // dejar constancia visible del problema dentro del propio expediente,
            // igual que ya se hacia para un archivo faltante.
            try
            {
                AgregarPaginas(documentoFinal, File.ReadAllBytes(rutaFisica));
            }
            catch (Exception)
            {
                AgregarPaginas(documentoFinal, ExpedienteMaquetador.GenerarAviso("El comprobante no se pudo leer (archivo dañado o incompleto)."));
            }
        }

        private static void AgregarPaginas(PdfDocument destino, byte[] pdfBytes)
        {
            using var stream = new MemoryStream(pdfBytes);
            using var origen = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
            foreach (var pagina in origen.Pages)
            {
                destino.AddPage(pagina);
            }
        }
    }
}

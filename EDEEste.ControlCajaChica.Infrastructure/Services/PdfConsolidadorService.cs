using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace EDEEste.ControlCajaChica.Infrastructure.Services
{
    /// <summary>
    /// Arma el expediente PDF de una solicitud de reposición: una portada-resumen con
    /// la tabla de gastos, seguida, por cada comprobante adjunto, de una página de
    /// transcripción (los datos del gasto + la etiqueta que digitó el custodio) y
    /// después el archivo original.
    ///
    /// Decide QUÉ va en el expediente y en qué orden -- el CÓMO se dibuja cada pieza
    /// con MigraDoc vive en <see cref="ExpedienteMaquetador"/>, separado a propósito:
    /// un cambio de diseño visual no debería tener que tocar esta lógica de
    /// ensamblaje, y viceversa.
    ///
    /// MigraDoc genera cada pieza (páginas nuevas, texto, imágenes) pero no puede
    /// insertar páginas de un PDF que no generó él; por eso el ensamblaje final usa
    /// PDFsharp solo para concatenar páginas ya generadas -- el PDF original de cada
    /// comprobante se copia tal cual, nunca se reabre para editarlo ni se
    /// re-renderiza, así que el archivo guardado en disco (y su HashSHA256) no se
    /// tocan en ningun momento.
    /// </summary>
    public class PdfConsolidadorService : IPdfConsolidadorService
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IIdentityService _identityService;
        private readonly string _rutaLogo;

        public PdfConsolidadorService(
            IFileStorageService fileStorageService,
            IIdentityService identityService,
            IOptions<OpcionesAlmacenamiento> opciones)
        {
            _fileStorageService = fileStorageService;
            _identityService = identityService;
            /* OJO: App_Data está en el .gitignore, así que el logo no viaja con el
               repositorio. Al desplegar o clonar en otra máquina hay que copiar a mano
               App_Data/recursos/logo-edeeste.png; si falta, los PDFs salen sin logo,
               sin error ni aviso. */
            _rutaLogo = Path.Combine(opciones.Value.RutaRaiz, "recursos", "logo-edeeste.png");
        }

        public async Task<byte[]> ConsolidarComprobantesAsync(SolicitudReposicion solicitud, CancellationToken cancellationToken = default)
        {
            using var documentoFinal = new PdfDocument();

            var nombreCustodio = await ResolverNombreCustodioAsync(solicitud.FondoCajaChica?.CustodioId);
            AgregarPaginas(documentoFinal, ExpedienteMaquetador.GenerarResumen(solicitud, nombreCustodio, _rutaLogo));

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
            /* closeStream: false a propósito: por defecto Save() cierra el stream que
               recibe, y si lo cerrara acá, salida.ToArray() de la línea siguiente
               fallaría con ObjectDisposedException porque el stream ya no existiría. */
            documentoFinal.Save(salida, closeStream: false);
            return salida.ToArray();
        }

        /// <summary>
        /// Numera el expediente completo al final, cuando ya se sabe cuántas páginas
        /// tiene.
        ///
        /// No se puede hacer con un footer de MigraDoc: el documento se arma juntando
        /// varios PDF chicos independientes (y los originales de los comprobantes, que
        /// ni siquiera generamos nosotros), así que cada pieza numeraría desde 1 por su
        /// cuenta. Para un expediente contable la numeración continua importa: es lo
        /// que permite afirmar que no falta ninguna hoja.
        /// </summary>
        private static void EstamparNumerosDePagina(PdfDocument documento)
        {
            var fuente = new XFont(ResolutorFuentesEmbebidas.NombreFamilia, 8, XFontStyleEx.Regular);
            var total = documento.PageCount;

            for (var indice = 0; indice < total; indice++)
            {
                var pagina = documento.Pages[indice];

                /* Append dibuja encima del contenido que ya trae la página, sin
                   reescribirlo: las páginas importadas de un PDF ajeno quedan intactas. */
                using var lienzo = XGraphics.FromPdfPage(pagina, XGraphicsPdfPageOptions.Append);

                var area = new XRect(0, pagina.Height.Point - 25, pagina.Width.Point, 15);
                lienzo.DrawString(
                    $"Página {indice + 1} de {total}",
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

            var portada = ExpedienteMaquetador.NuevoDocumento(_rutaLogo);
            ExpedienteMaquetador.ComponerPortada(portada.LastSection, gasto, comprobante);
            AgregarPaginas(documentoFinal, ExpedienteMaquetador.Renderizar(portada));

            if (esImagen)
            {
                var imagen = ExpedienteMaquetador.NuevoDocumento(_rutaLogo);
                ExpedienteMaquetador.ComponerImagen(imagen.LastSection, rutaFisica);
                AgregarPaginas(documentoFinal, ExpedienteMaquetador.Renderizar(imagen));
                return;
            }

            if (!File.Exists(rutaFisica))
            {
                AgregarPaginas(documentoFinal, ExpedienteMaquetador.GenerarAviso("El comprobante no existe en el servidor.", _rutaLogo));
                return;
            }

            /* Un PDF guardado pero truncado o corrupto (por ejemplo, una subida
               cortada por un corte de red) pasa el chequeo de magic bytes de
               ValidadorComprobante -- que solo mira que el archivo EMPIECE con "%PDF"
               -- pero PdfReader.Open no puede parsearlo después. Sin este try/catch,
               CrearSolicitudReposicionHandler genera este PDF ANTES de guardar la
               solicitud, así que un solo comprobante ilegible tumbaba la reposición
               completa; y el custodio no tiene cómo resolverlo por su cuenta, porque
               ComprobanteAdjunto ya está firmado con HMAC y es inmutable. Se prefiere
               dejar constancia visible del problema dentro del propio expediente,
               igual que ya se hacía para un archivo faltante. */
            try
            {
                AgregarPaginas(documentoFinal, File.ReadAllBytes(rutaFisica));
            }
            catch (Exception)
            {
                AgregarPaginas(documentoFinal, ExpedienteMaquetador.GenerarAviso("El comprobante no se pudo leer (archivo dañado o incompleto).", _rutaLogo));
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

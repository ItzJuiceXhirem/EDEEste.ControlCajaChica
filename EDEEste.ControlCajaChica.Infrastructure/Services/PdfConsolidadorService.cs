using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Domain.Entities;
using Microsoft.Extensions.Logging;
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
        private const string AvisoNoExiste = "El comprobante no existe en el servidor.";
        private const string AvisoAlterado =
            "El comprobante fue modificado después de registrarse. No se incluye en el expediente.";
        private const string AvisoIlegible = "El comprobante no se pudo leer (archivo dañado o incompleto).";

        private readonly IFileStorageService _fileStorageService;
        private readonly IIdentityService _identityService;
        private readonly ILogger<PdfConsolidadorService> _logger;

        public PdfConsolidadorService(
            IFileStorageService fileStorageService,
            IIdentityService identityService,
            ILogger<PdfConsolidadorService> logger)
        {
            _fileStorageService = fileStorageService;
            _identityService = identityService;
            _logger = logger;
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
                    await AgregarComprobanteAsync(documentoFinal, solicitud.Id, gasto, comprobante, cancellationToken);
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

        /// <summary>
        /// Portada del comprobante y, detras, su contenido -- o una pagina de aviso en su
        /// lugar si no existe, fue modificado despues de registrarse o no se puede leer.
        ///
        /// Ninguno de esos casos bloquea la reposicion completa, a proposito:
        /// CrearSolicitudReposicionHandler genera este PDF ANTES de guardar la
        /// solicitud, y el custodio no tiene como corregir un comprobante por su cuenta
        /// (ComprobanteAdjunto esta firmado con HMAC y es inmutable). Se deja constancia
        /// visible dentro del propio expediente, y el Gerente decide si rechaza.
        /// </summary>
        private async Task AgregarComprobanteAsync(
            PdfDocument documentoFinal,
            Guid solicitudId,
            Gasto gasto,
            ComprobanteAdjunto comprobante,
            CancellationToken cancellationToken)
        {
            var portada = ExpedienteMaquetador.NuevoDocumento();
            ExpedienteMaquetador.ComponerPortada(portada.LastSection, gasto, comprobante);
            AgregarPaginas(documentoFinal, ExpedienteMaquetador.Renderizar(portada));

            var archivo = await _fileStorageService.LeerArchivoVerificadoAsync(
                comprobante.RutaArchivo, comprobante.HashSHA256, cancellationToken);

            if (archivo is null)
            {
                AgregarPaginas(documentoFinal, ExpedienteMaquetador.GenerarAviso(AvisoNoExiste));
                return;
            }

            // Tambien la fila: sin ella, quien pudiera escribir en la BDD repuntaria a
            // la vez la ruta y el hash, y la comparacion del archivo pasaria. Un
            // comprobante siempre nace con hash, asi que "sin hash" tambien es anomalo.
            if (!comprobante.IntegridadVerificada || archivo.Integridad != IntegridadArchivo.Integro)
            {
                _logger.LogCritical(
                    "ALERTA DE MANIPULACION: el comprobante {ComprobanteId} del gasto {GastoId} se excluyo del " +
                    "expediente de la solicitud {SolicitudId} (fila integra: {FilaIntegra}, archivo: {Integridad}).",
                    comprobante.Id, gasto.Id, solicitudId, comprobante.IntegridadVerificada, archivo.Integridad);

                AgregarPaginas(documentoFinal, ExpedienteMaquetador.GenerarAviso(AvisoAlterado));
                return;
            }

            // Un archivo integro todavia puede estar truncado o corrupto desde que se
            // subio (por ejemplo, una subida cortada por la red): pasa el chequeo de
            // magic bytes de ValidadorComprobante, que solo mira como EMPIEZA, pero
            // PdfReader o MigraDoc no pueden abrirlo despues.
            try
            {
                AgregarPaginas(documentoFinal, EsImagen(comprobante)
                    ? RenderizarImagen(archivo.Contenido)
                    : archivo.Contenido);
            }
            catch (Exception)
            {
                AgregarPaginas(documentoFinal, ExpedienteMaquetador.GenerarAviso(AvisoIlegible));
            }
        }

        private static bool EsImagen(ComprobanteAdjunto comprobante) =>
            comprobante.TipoMime.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

        private static byte[] RenderizarImagen(byte[] contenido)
        {
            var documento = ExpedienteMaquetador.NuevoDocumento();
            ExpedienteMaquetador.ComponerImagen(documento.LastSection, contenido);
            return ExpedienteMaquetador.Renderizar(documento);
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

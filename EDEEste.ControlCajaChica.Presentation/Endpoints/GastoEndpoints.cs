using System;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Constants;

namespace EDEEste.ControlCajaChica.Presentation.Endpoints
{
    /// <summary>
    /// Visualización de un comprobante adjunto de un gasto (imagen o PDF).
    ///
    /// Mismo motivo que ReposicionEndpoints: MapStaticAssets sirve wwwroot con un
    /// manifiesto armado al compilar, no archivos que subió un usuario en tiempo de
    /// ejecución. Sin nombre de descarga (fileDownloadName) el navegador abre el
    /// archivo en la pestaña en vez de forzar la descarga, que es lo que hace falta
    /// para "ver" un comprobante desde la ficha de Gastos.
    /// </summary>
    public static class GastoEndpoints
    {
        public static IEndpointRouteBuilder MapGastoEndpoints(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/gastos/comprobantes/{id:guid}/archivo", async (
                Guid id,
                IGastoRepository gastos,
                IFileStorageService almacenamiento,
                CancellationToken cancellationToken) =>
            {
                var comprobante = await gastos.ObtenerComprobanteAsync(id, cancellationToken);

                if (comprobante is null)
                {
                    return Results.NotFound();
                }

                var contenido = await almacenamiento.LeerArchivoAsync(comprobante.RutaArchivo);
                if (contenido is null)
                {
                    return Results.NotFound();
                }

                return Results.File(contenido, comprobante.TipoMime);
            })
            .RequireAuthorization(Permisos.VerGastos);

            return endpoints;
        }
    }
}

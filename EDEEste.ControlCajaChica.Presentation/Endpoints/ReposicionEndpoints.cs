using System;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;

namespace EDEEste.ControlCajaChica.Presentation.Endpoints
{
    /// <summary>
    /// Descarga del expediente PDF de una reposicion.
    ///
    /// Hace falta un endpoint propio porque los archivos que sube o genera la
    /// aplicacion en tiempo de ejecucion no los sirve MapStaticAssets: ese trabaja
    /// con un manifiesto que se arma al compilar. Servirlos por aqui tambien es lo
    /// correcto en seguridad, porque deja un unico lugar donde exigir permisos.
    /// </summary>
    public static class ReposicionEndpoints
    {
        public static IEndpointRouteBuilder MapReposicionEndpoints(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/reposiciones/{id:guid}/pdf", async (
                Guid id,
                IReposicionRepository reposiciones,
                IFileStorageService almacenamiento,
                CancellationToken cancellationToken) =>
            {
                var solicitud = await reposiciones.ObtenerConDetalleAsync(id, cancellationToken);

                if (solicitud is null || string.IsNullOrWhiteSpace(solicitud.RutaPdfConsolidado))
                {
                    return Results.NotFound();
                }

                var contenido = await almacenamiento.LeerArchivoAsync(solicitud.RutaPdfConsolidado);
                if (contenido is null)
                {
                    return Results.NotFound();
                }

                return Results.File(contenido, "application/pdf", $"reposicion-{id}.pdf");
            });

            // PENDIENTE: cuando el login este funcionando hay que exigir aqui los roles
            // que corresponden (Custodio, Gerente, Finanzas y Auditor). Hoy queda
            // abierto porque no hay forma de iniciar sesion todavia.

            return endpoints;
        }
    }
}

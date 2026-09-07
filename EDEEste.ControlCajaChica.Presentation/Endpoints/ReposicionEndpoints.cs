using System;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Constants;

namespace EDEEste.ControlCajaChica.Presentation.Endpoints
{
    /// <summary>
    /// Descarga del expediente PDF de una reposición.
    ///
    /// Hace falta un endpoint propio porque los archivos que sube o genera la
    /// aplicación en tiempo de ejecución no los sirve MapStaticAssets: ese trabaja
    /// con un manifiesto que se arma al compilar. Servirlos por aquí también es lo
    /// correcto en seguridad, porque deja un único lugar donde exigir permisos.
    /// </summary>
    public static class ReposicionEndpoints
    {
        public static IEndpointRouteBuilder MapReposicionEndpoints(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/reposiciones/{id:guid}/pdf", async (
                Guid id,
                HttpContext contexto,
                IReposicionRepository reposiciones,
                IIdentityService identidad,
                ICurrentUserService usuarioActual,
                IFileStorageService almacenamiento,
                CancellationToken cancellationToken) =>
            {
                // Una sola respuesta para las tres ramas de abajo, mismo motivo que
                // GastoEndpoints: un mensaje distinto por rama ("no existe" vs "no
                // tiene permiso" vs "el archivo no esta") volveria el texto de la
                // respuesta un canal para distinguir "el Id no existe" de "existe pero
                // no es tuyo". Con cuerpo y no vacio para que
                // UseStatusCodePagesWithReExecute (Program.cs) no la reejecute contra
                // /not-found (ver el comentario del DELETE de staging en GastoEndpoints).
                static IResult ExpedienteNoDisponible() => Results.Json(
                    new { error = "La reposición no existe o no tiene acceso a su expediente." },
                    statusCode: StatusCodes.Status404NotFound);

                var solicitud = await reposiciones.ObtenerConDetalleAsync(id, cancellationToken);

                if (solicitud is null || string.IsNullOrWhiteSpace(solicitud.RutaPdfConsolidado))
                {
                    return ExpedienteNoDisponible();
                }

                // Mismo motivo que GastoEndpoints: sin esto, un Custodio con el Id de
                // una reposicion de otro fondo (enlace reenviado, historial del
                // navegador, Id consecutivo) podia descargar el expediente de un
                // custodio distinto. NotFound y no Forbid, para no confirmar que el
                // Id corresponde a una reposicion real de otro fondo.
                // El ?. es a proposito y no cosmetico: FondoCajaChica es nullable y,
                // si por lo que sea no viniera cargado, "null != usuarioId" da true y
                // se niega el acceso. Falla cerrado, que es lo correcto aqui.
                var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
                if (usuario.Id is not { } usuarioId)
                {
                    return ExpedienteNoDisponible();
                }

                if (await identidad.EstaEnRolAsync(usuarioId, RolesApp.Custodio) && solicitud.FondoCajaChica?.CustodioId != usuarioId)
                {
                    return ExpedienteNoDisponible();
                }

                var contenido = await almacenamiento.LeerArchivoAsync(solicitud.RutaPdfConsolidado);
                if (contenido is null)
                {
                    return ExpedienteNoDisponible();
                }

                // Mismo motivo que GastoEndpoints: un expediente es informacion
                // sensible de un tercero, no algo que deba sobrevivir en una cache
                // intermedia o en el disco del navegador.
                contexto.Response.Headers.CacheControl = "private, no-store";

                return Results.File(contenido, "application/pdf", $"reposicion-{id}.pdf");
            })
            .RequireAuthorization(Permisos.DescargarExpediente);

            return endpoints;
        }
    }
}

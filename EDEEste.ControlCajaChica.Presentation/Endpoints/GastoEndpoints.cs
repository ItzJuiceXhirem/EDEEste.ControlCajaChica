using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Features.Gastos;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Infrastructure.Configuration;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace EDEEste.ControlCajaChica.Presentation.Endpoints
{
    /// <summary>
    /// Visualización y subida de comprobantes de gasto.
    ///
    /// La subida va por aquí (HTTP multipart) y no por el circuito de Blazor
    /// Server a propósito: los bytes de un archivo real chocan con el límite de
    /// mensaje de SignalR (32 KB por defecto) o retrasan los pings de
    /// mantenimiento más allá de su margen -- bajo IIS eso tumba el circuito
    /// entero, no solo la subida. Un multipart normal además es lo único que un
    /// antivirus de red o el Request Filtering de IIS pueden inspeccionar; los
    /// frames de WebSocket son opacos para ellos.
    ///
    /// El archivo llega primero a una carpeta de staging por usuario (nunca a su
    /// ubicación final): RegistrarGastoHandler es quien decide, al guardar el
    /// gasto, si se promueve o se descarta. Así la fila de ComprobanteAdjunto en
    /// BDD -- que va firmada con HMAC -- se escribe una sola vez con su ruta
    /// definitiva, en vez de guardarse con una ruta de staging y corregirla
    /// después.
    /// </summary>
    public static class GastoEndpoints
    {
        // 10 MB: mismo tope que ya exigía RegistrarGasto.razor.cs del lado del
        // circuito. FormOptions.MultipartBodyLengthLimit (Program.cs) es el techo
        // duro del servidor por encima de este; esta es la validación de negocio.
        private const long TamanoMaximoArchivo = 10 * 1024 * 1024;

        // Cuota de staging por usuario: no es la regla de negocio de "20
        // comprobantes por gasto" (esa vive en RegistrarGastoHandler, sobre el
        // comando ya armado) sino un techo anti-DoS para que nadie llene el disco
        // subiendo sin nunca completar un formulario.
        private const int CuotaArchivosStaging = 40;
        private const long CuotaBytesStaging = 200 * 1024 * 1024;

        public static IEndpointRouteBuilder MapGastoEndpoints(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/gastos/comprobantes/{id:guid}/archivo", async (
                Guid id,
                HttpContext contexto,
                IGastoRepository gastos,
                IFondoRepository fondos,
                IIdentityService identidad,
                ICurrentUserService usuarioActual,
                IFileStorageService almacenamiento,
                CancellationToken cancellationToken) =>
            {
                // Una sola respuesta, reusada en las cuatro ramas de abajo -- a
                // proposito, no por ahorrar codigo: si cada rama tuviera su propio
                // mensaje ("no existe" vs "no tiene permiso" vs "el archivo no esta"),
                // el texto de la respuesta se volveria un canal para distinguir "el Id
                // no existe" de "existe pero no es tuyo", justo lo que el 404 uniforme
                // (en vez de 404 aqui y 403 alla) existe para evitar. Con cuerpo y no
                // vacio: UseStatusCodePagesWithReExecute (Program.cs) reejecuta
                // cualquier respuesta de error sin cuerpo contra /not-found -- se
                // descubrio con el DELETE de staging (ver su comentario).
                static IResult ComprobanteNoDisponible() => Results.Json(
                    new { error = "El comprobante no existe o no tiene acceso a él." },
                    statusCode: StatusCodes.Status404NotFound);

                var comprobante = await gastos.ObtenerComprobanteAsync(id, cancellationToken);

                if (comprobante is null)
                {
                    return ComprobanteNoDisponible();
                }

                // VerGastos por si solo no basta: sin esto, un Custodio con el enlace de
                // otro fondo (o simplemente probando Id consecutivos) podia abrir el
                // comprobante de un custodio distinto. NotFound y no Forbid, para no
                // confirmar que el Id corresponde a un comprobante real de otro fondo.
                var gasto = await gastos.ObtenerPorIdAsync(comprobante.GastoId, cancellationToken);
                if (gasto is null)
                {
                    return ComprobanteNoDisponible();
                }

                var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
                if (usuario.Id is not { } usuarioId)
                {
                    return ComprobanteNoDisponible();
                }

                if (await identidad.EstaEnRolAsync(usuarioId, RolesApp.Custodio))
                {
                    var fondo = await fondos.ObtenerPorIdAsync(gasto.FondoCajaChicaId, cancellationToken);
                    if (fondo is null || fondo.CustodioId != usuarioId)
                    {
                        return ComprobanteNoDisponible();
                    }
                }

                var contenido = await almacenamiento.LeerArchivoAsync(comprobante.RutaArchivo);
                if (contenido is null)
                {
                    return ComprobanteNoDisponible();
                }

                // Un comprobante es informacion sensible de un tercero; sin esto, un
                // proxy o el propio cache de disco del navegador (relevante en una PC
                // compartida de oficina) podria conservarlo mas alla de esta respuesta.
                contexto.Response.Headers.CacheControl = "private, no-store";

                return Results.File(contenido, comprobante.TipoMime);
            })
            .RequireAuthorization(Permisos.VerGastos);

            endpoints.MapPost("/gastos/comprobantes/staging", async (
                HttpContext contexto,
                IFormFile? archivo,
                ICurrentUserService usuarioActual,
                IFileStorageService almacenamiento,
                CancellationToken cancellationToken) =>
            {
                if (archivo is null || archivo.Length == 0)
                {
                    return Results.BadRequest(new { error = "No se recibió ningún archivo." });
                }

                if (archivo.Length > TamanoMaximoArchivo)
                {
                    return Results.BadRequest(new
                    {
                        error = $"El archivo supera el tamaño máximo de {TamanoMaximoArchivo / 1024 / 1024} MB."
                    });
                }

                var nombreOriginal = SanearNombreArchivo(archivo.FileName);
                if (nombreOriginal.Length == 0)
                {
                    return Results.BadRequest(new { error = "El nombre del archivo no es válido." });
                }

                if (!ValidadorComprobante.EsFormatoAceptado(nombreOriginal, archivo.ContentType))
                {
                    return Results.BadRequest(new { error = "Formato no aceptado (solo PDF, JPG, JPEG o PNG)." });
                }

                var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
                if (usuario.Id is not { } usuarioId)
                {
                    // Con cuerpo y no vacio por el mismo motivo que el BadRequest de
                    // mas abajo: una respuesta de error sin cuerpo la reejecuta
                    // UseStatusCodePagesWithReExecute contra /not-found.
                    return Results.Json(new { error = "No se pudo identificar al usuario." }, statusCode: StatusCodes.Status401Unauthorized);
                }

                // Barrido oportunista: es el mecanismo real de limpieza bajo IIS (el
                // app pool se duerme y el BackgroundService de respaldo puede no
                // llegar a correr nunca), y de paso libera cupo antes de contarlo.
                await almacenamiento.LimpiarStagingDelUsuarioAsync(usuarioId, OpcionesAlmacenamiento.RetencionStaging, cancellationToken);

                var (archivosEnStaging, bytesEnStaging) = await almacenamiento.ContarStagingAsync(usuarioId, cancellationToken);
                if (archivosEnStaging >= CuotaArchivosStaging || bytesEnStaging + archivo.Length > CuotaBytesStaging)
                {
                    return Results.BadRequest(new
                    {
                        error = "Alcanzó el límite de archivos pendientes de guardar. Registre o descarte algunos antes de subir más."
                    });
                }

                await using var contenido = archivo.OpenReadStream();

                // No debería pasar -- ASP.NET Core siempre entrega un stream que se
                // puede rebobinar para archivos de un formulario -- pero si pasara,
                // es mejor rechazar el archivo que dejar que la comprobación de
                // firma de abajo degrade en silencio.
                if (!contenido.CanSeek)
                {
                    return Results.BadRequest(new { error = "No se pudo leer el archivo." });
                }

                if (!await ValidadorComprobante.CoincideConFirmaEsperadaAsync(contenido, archivo.ContentType, cancellationToken))
                {
                    return Results.BadRequest(new { error = "El contenido del archivo no coincide con su tipo declarado." });
                }

                if (ValidadorComprobante.ExtensionCanonica(nombreOriginal) is not { } extension)
                {
                    return Results.BadRequest(new { error = "Formato no aceptado (solo PDF, JPG, JPEG o PNG)." });
                }

                var referencia = await almacenamiento.GuardarComprobanteEnStagingAsync(
                    usuarioId, nombreOriginal, extension, contenido, cancellationToken);

                return Results.Ok(new { referencia, nombreOriginal, tamanoBytes = archivo.Length });
            })
            .RequireAuthorization(Permisos.RegistrarGasto)
            .RequireRateLimiting("subida-comprobantes");

            endpoints.MapDelete("/gastos/comprobantes/staging/{referencia:guid}", async (
                Guid referencia,
                HttpContext contexto,
                IAntiforgery antiforgery,
                ICurrentUserService usuarioActual,
                IFileStorageService almacenamiento,
                CancellationToken cancellationToken) =>
            {
                // Este endpoint no recibe ningun formulario, asi que a diferencia del
                // POST de arriba (donde el binding de IFormFile activa la validacion
                // automatica) no queda protegido contra CSRF por si solo. Sin esto,
                // cualquier pagina de terceros podria borrarle a un usuario logueado
                // un archivo que acaba de subir con solo hacerle visitar un DELETE
                // oculto. Se valida a mano y se traduce a 400 -- sin el catch, un
                // token invalido revienta con una excepcion sin manejar (500).
                //
                // BadRequest() CON cuerpo y no vacio: UseStatusCodePagesWithReExecute
                // (Program.cs) reejecuta cualquier respuesta de error SIN cuerpo contra
                // /not-found, preservando el metodo original -- un DELETE reejecutado
                // ahi da 405 (esa pagina solo acepta GET/HEAD/POST), enmascarando por
                // completo este 400. Se descubrio probando este mismo endpoint.
                try
                {
                    await antiforgery.ValidateRequestAsync(contexto);
                }
                catch (AntiforgeryValidationException)
                {
                    return Results.BadRequest(new { error = "Token de seguridad inválido o vencido. Recargue la página e intente de nuevo." });
                }

                var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
                if (usuario.Id is not { } usuarioId)
                {
                    // Con cuerpo y no vacio por el mismo motivo que el BadRequest de
                    // mas abajo: una respuesta de error sin cuerpo la reejecuta
                    // UseStatusCodePagesWithReExecute contra /not-found.
                    return Results.Json(new { error = "No se pudo identificar al usuario." }, statusCode: StatusCodes.Status401Unauthorized);
                }

                await almacenamiento.EliminarStagingAsync(usuarioId, referencia, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(Permisos.RegistrarGasto);

            return endpoints;
        }

        /// <summary>
        /// Path.GetFileName descarta cualquier componente de ruta que venga en el
        /// nombre (un multipart es un formulario cualquiera, no algo que el
        /// navegador sanee por nosotros). Los caracteres de control no tienen nada
        /// que hacer en un nombre que despues se muestra tal cual en pantalla.
        /// </summary>
        private static string SanearNombreArchivo(string nombreOriginal)
        {
            var nombre = Path.GetFileName(nombreOriginal).Trim();
            var limpio = new string(nombre.Where(c => !char.IsControl(c)).ToArray());

            return limpio.Length > 255 ? limpio[..255] : limpio;
        }
    }
}

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Features.Gastos;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Presentation.Components.Account;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace EDEEste.ControlCajaChica.Presentation.Endpoints
{
    /// <summary>
    /// Foto de perfil: subirla y servirla.
    ///
    /// La subida va por HTTP multipart y no por el circuito de Blazor Server por la
    /// misma razon que los comprobantes de gasto (ver GastoEndpoints): una foto de
    /// camara supera el limite de mensaje de SignalR y bajo IIS tumba el circuito
    /// entero, no solo la subida.
    ///
    /// A diferencia de un comprobante, aqui NO hay staging: aquel existe porque el
    /// gasto se confirma en una transaccion aparte que puede fallar por concurrencia
    /// (BalanceActual es token de concurrencia), y hasta entonces el archivo no tiene
    /// dueño. Una foto de perfil se escribe y se referencia en el acto, sobre la
    /// propia fila del usuario, que nadie mas toca en operacion normal.
    ///
    /// La pantalla de Perfil es SSR estatico (no declara @rendermode, usa HttpContext
    /// en cascada y EditForm method="post"), asi que aqui NO se puede usar el patron de
    /// RegistrarGasto -- no hay circuito para InputFile ni para interop de JS. Se posta
    /// con un formulario HTML normal y se responde con una redireccion mas un mensaje
    /// en la cookie de estado, que es exactamente como esa pantalla ya guarda el resto
    /// del perfil.
    /// </summary>
    public static class PerfilEndpoints
    {
        // Bastante mas bajo que los 10 MB de un comprobante: una foto de perfil se
        // muestra en un circulo de 84 px, no es un documento contable que alguien vaya
        // a ampliar para leerle la letra chica.
        private const long TamanoMaximoFoto = 3 * 1024 * 1024;

        private const string PaginaPerfil = "/Account/Manage";

        /// <summary>
        /// Misma cookie que usa IdentityRedirectManager, para que el
        /// &lt;StatusMessage /&gt; de la pantalla la muestre sin saber de donde vino. Un
        /// mensaje que empieza con "Error" se pinta como error; cualquier otro, como
        /// exito (ver StatusMessage.razor).
        /// </summary>
        private static IResult VolverAlPerfil(HttpContext contexto, string mensaje)
        {
            contexto.Response.Cookies.Append(
                IdentityRedirectManager.StatusCookieName,
                mensaje,
                new CookieOptions
                {
                    SameSite = SameSiteMode.Strict,
                    HttpOnly = true,
                    IsEssential = true,
                    MaxAge = TimeSpan.FromSeconds(5)
                });

            return Results.Redirect(PaginaPerfil);
        }

        public static IEndpointRouteBuilder MapPerfilEndpoints(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/usuarios/{usuarioId}/foto", async (
                string usuarioId,
                HttpContext contexto,
                IAuthorizationService autorizacion,
                ICurrentUserService usuarioActual,
                IIdentityService identidad,
                IFileStorageService almacenamiento,
                CancellationToken cancellationToken) =>
            {
                // Una sola respuesta para todas las ramas de fallo, igual que
                // ComprobanteNoDisponible() en GastoEndpoints: si "no existe", "no
                // tiene foto" y "no es tuya y no eres administrador" respondieran
                // distinto, el cuerpo se volveria un canal para sondear que Ids
                // existen. Lleva cuerpo JSON porque una respuesta de error SIN cuerpo
                // la reejecuta UseStatusCodePagesWithReExecute contra /not-found.
                static IResult FotoNoDisponible() => Results.Json(
                    new { error = "La foto no existe o no tiene acceso a ella." },
                    statusCode: StatusCodes.Status404NotFound);

                var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
                if (usuario.Id is not { } usuarioActualId)
                {
                    return FotoNoDisponible();
                }

                // La propia siempre; la de otro solo con el permiso que ya protege la
                // pantalla de Usuarios, que es el unico sitio donde se ven las ajenas.
                var esPropia = string.Equals(usuarioId, usuarioActualId, StringComparison.Ordinal);
                if (!esPropia)
                {
                    var permitido = await autorizacion.AuthorizeAsync(contexto.User, Permisos.AdministrarUsuarios);
                    if (!permitido.Succeeded)
                    {
                        return FotoNoDisponible();
                    }
                }

                var rutaRelativa = await identidad.ObtenerRutaFotoPerfilAsync(usuarioId);
                if (string.IsNullOrWhiteSpace(rutaRelativa))
                {
                    // Pedir la PROPIA foto y no tener una no es un error: es el estado
                    // normal de quien todavia no subio ninguna. El sidebar la pide
                    // siempre, sin saber de antemano si existe (consultarlo antes
                    // seria una consulta mas sobre el mismo DbContext de la pagina, ver
                    // el comentario en NavMenu.razor) -- un 204 no aparece en la
                    // consola del navegador como error (a diferencia de un 404) y no
                    // filtra nada, porque ya conoces tu propio estado. Pedir la de OTRO
                    // usuario y que no tenga sigue devolviendo el mismo 404 uniforme:
                    // ahi si importa no distinguir "no existe" de "no tiene foto".
                    return esPropia ? Results.NoContent() : FotoNoDisponible();
                }

                var tipoMime = ValidadorComprobante.MimeCanonicoPorExtension(Path.GetExtension(rutaRelativa));
                if (tipoMime is null)
                {
                    return FotoNoDisponible();
                }

                var contenido = await almacenamiento.LeerArchivoAsync(rutaRelativa);
                if (contenido is null || contenido.Length == 0)
                {
                    return FotoNoDisponible();
                }

                // Sin cache: la foto se reemplaza en su sitio (nombre determinista por
                // usuario), asi que una copia guardada por el navegador o un proxy
                // seguiria mostrando la anterior despues de cambiarla.
                contexto.Response.Headers.CacheControl = "private, no-store";

                return Results.File(contenido, tipoMime);
            })
            .RequireAuthorization(Permisos.GestionarPerfilPropio);

            endpoints.MapPost("/perfil/foto", async (
                IFormFile? archivo,
                HttpContext contexto,
                ICurrentUserService usuarioActual,
                IIdentityService identidad,
                IFileStorageService almacenamiento,
                CancellationToken cancellationToken) =>
            {
                if (archivo is null || archivo.Length == 0)
                {
                    return VolverAlPerfil(contexto, "Error: no se recibió ninguna imagen.");
                }

                if (archivo.Length > TamanoMaximoFoto)
                {
                    return VolverAlPerfil(contexto,
                        $"Error: la imagen supera el tamaño máximo de {TamanoMaximoFoto / 1024 / 1024} MB.");
                }

                // Path.GetFileName descarta cualquier componente de ruta que venga en el
                // nombre. No hace falta el saneado completo de GastoEndpoints (control
                // de caracteres, truncado a 255): de este nombre solo se usa la
                // extension, nunca se guarda ni se muestra -- el archivo final se llama
                // por el HMAC del usuario.
                var nombreArchivo = Path.GetFileName(archivo.FileName ?? string.Empty).Trim();

                if (!ValidadorComprobante.EsFormatoAceptado(nombreArchivo, archivo.ContentType)
                    || ValidadorComprobante.ExtensionCanonica(nombreArchivo) is not { } extension
                    // La lista blanca de ValidadorComprobante es la de comprobantes e
                    // incluye PDF. Para una foto de perfil se rechaza explicitamente
                    // aqui, en vez de parametrizar un validador que hoy tiene un
                    // contrato claro para gastos: se lee mejor en el sitio de uso.
                    || extension == ".pdf")
                {
                    return VolverAlPerfil(contexto, "Error: formato no aceptado (solo JPG, JPEG o PNG).");
                }

                var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
                if (usuario.Id is not { } usuarioId)
                {
                    return VolverAlPerfil(contexto, "Error: no se pudo identificar al usuario.");
                }

                await using var contenido = archivo.OpenReadStream();

                // No deberia pasar con un archivo de formulario, pero si pasara es
                // mejor rechazarlo que dejar que la comprobacion de firma degrade en
                // silencio (CoincideConFirmaEsperadaAsync falla cerrado ante un stream
                // no rebobinable, esto solo lo hace explicito).
                if (!contenido.CanSeek)
                {
                    return VolverAlPerfil(contexto, "Error: no se pudo leer el archivo.");
                }

                if (!await ValidadorComprobante.CoincideConFirmaEsperadaAsync(contenido, archivo.ContentType, cancellationToken))
                {
                    return VolverAlPerfil(contexto, "Error: el archivo no es una imagen válida.");
                }

                // Se lee la ruta anterior ANTES de escribir: si la extension cambia
                // (.jpg -> .png) el nombre cambia, y el archivo viejo hay que borrarlo.
                var rutaAnterior = await identidad.ObtenerRutaFotoPerfilAsync(usuarioId);

                var rutaNueva = await almacenamiento.GuardarFotoPerfilAsync(
                    usuarioId, extension, contenido, cancellationToken);

                await identidad.ActualizarFotoPerfilAsync(usuarioId, rutaNueva);

                // Recien despues de que la fila apunta a la foto nueva: al reves, un
                // fallo entre medio dejaria la fila apuntando a un archivo ya borrado.
                // Un huerfano en disco es aceptable; una fila rota nunca lo es (mismo
                // criterio que CrearSolicitudReposicionHandler).
                if (!string.IsNullOrWhiteSpace(rutaAnterior)
                    && !string.Equals(rutaAnterior, rutaNueva, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        await almacenamiento.EliminarArchivoAsync(rutaAnterior);
                    }
                    catch (Exception)
                    {
                        // Best-effort: que no se caiga una subida correcta porque el
                        // archivo viejo estaba bloqueado (antivirus, por ejemplo).
                    }
                }

                return VolverAlPerfil(contexto, "Su foto de perfil fue actualizada.");
            })
            .RequireAuthorization(Permisos.GestionarPerfilPropio)
            .RequireRateLimiting("subida-foto-perfil");

            endpoints.MapPost("/perfil/foto/eliminar", async (
                HttpContext contexto,
                IAntiforgery antiforgery,
                ICurrentUserService usuarioActual,
                IIdentityService identidad,
                IFileStorageService almacenamiento,
                CancellationToken cancellationToken) =>
            {
                // A diferencia del POST de arriba, este formulario no lleva ningun
                // archivo, asi que el binding de IFormFile no activa la validacion
                // automatica de antiforgery. Se valida a mano, igual que el DELETE de
                // staging en GastoEndpoints: sin esto, una pagina de terceros podria
                // borrarle la foto a un usuario logueado con solo hacerle visitar un
                // formulario oculto.
                try
                {
                    await antiforgery.ValidateRequestAsync(contexto);
                }
                catch (AntiforgeryValidationException)
                {
                    return VolverAlPerfil(contexto,
                        "Error: token de seguridad inválido o vencido. Recargue la página e intente de nuevo.");
                }

                var usuario = await usuarioActual.ObtenerAsync(cancellationToken);
                if (usuario.Id is not { } usuarioId)
                {
                    return VolverAlPerfil(contexto, "Error: no se pudo identificar al usuario.");
                }

                var rutaActual = await identidad.ObtenerRutaFotoPerfilAsync(usuarioId);
                if (string.IsNullOrWhiteSpace(rutaActual))
                {
                    // Idempotente: quitar una foto que ya no esta no es un error.
                    return VolverAlPerfil(contexto, "Su foto de perfil fue eliminada.");
                }

                // Primero la fila y despues el archivo, mismo orden que al subir: un
                // huerfano en disco es aceptable, una fila apuntando a un archivo que
                // ya no existe no lo es.
                await identidad.ActualizarFotoPerfilAsync(usuarioId, null);

                try
                {
                    await almacenamiento.EliminarArchivoAsync(rutaActual);
                }
                catch (Exception)
                {
                    // Best-effort, igual que al reemplazar.
                }

                return VolverAlPerfil(contexto, "Su foto de perfil fue eliminada.");
            })
            .RequireAuthorization(Permisos.GestionarPerfilPropio);

            return endpoints;
        }
    }
}

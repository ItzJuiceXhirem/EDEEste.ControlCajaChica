using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EDEEste.ControlCajaChica.Presentation.Components.Pages
{
    public partial class Usuarios
    {
        private enum FiltroDirectorio { Todos, Aprobados, Denegados }

        [Inject] private IIdentityService IdentityService { get; set; } = default!;
        [Inject] private IPasswordResetService PasswordResetService { get; set; } = default!;
        [Inject] private ICurrentUserService CurrentUserService { get; set; } = default!;
        [Inject] private NavigationManager NavigationManager { get; set; } = default!;
        [Inject] private IJSRuntime JsRuntime { get; set; } = default!;

        private IReadOnlyList<UsuarioResumenDto>? usuarios;
        private IReadOnlyList<SolicitudPasswordResetResumenDto>? solicitudesReset;
        private string? procesandoId;
        private string? procesandoResetId;
        private string? mensaje;
        private string? error;
        private string? urlResetGenerada;
        private bool copiadoAlPortapapeles;

        private FiltroDirectorio filtroDirectorio = FiltroDirectorio.Todos;
        private string busquedaDirectorio = string.Empty;

        private bool HayAlgoPendiente =>
            Filtrar(EstadoAccesoUsuario.Pendiente).Count > 0 || (solicitudesReset?.Count ?? 0) > 0;

        private string Subtitulo => HayAlgoPendiente
            ? "Primero las solicitudes activas; después, el directorio completo."
            : "No queda ninguna solicitud pendiente; el directorio completo, abajo.";

        protected override async Task OnInitializedAsync() => await RecargarAsync();

        private async Task RecargarAsync()
        {
            usuarios = await IdentityService.ListarUsuariosAsync();
            solicitudesReset = await PasswordResetService.ListarPendientesAsync();
        }

        private IReadOnlyList<UsuarioResumenDto> Filtrar(EstadoAccesoUsuario estado) =>
            usuarios?.Where(u => u.EstadoAcceso == estado).ToList() ?? [];

        /// <summary>
        /// Sólo aprobados y denegados: los pendientes viven en su propia tarjeta de
        /// trabajo, no en el directorio -- mezclarlos ahí les daría una fila sin rol
        /// ni acciones de directorio que de verdad les apliquen.
        /// </summary>
        private IEnumerable<UsuarioResumenDto> DirectorioFiltrado
        {
            get
            {
                if (usuarios is null)
                {
                    return [];
                }

                var filtrados = filtroDirectorio switch
                {
                    FiltroDirectorio.Aprobados => usuarios.Where(u => u.EstadoAcceso == EstadoAccesoUsuario.Aprobado),
                    FiltroDirectorio.Denegados => usuarios.Where(u => u.EstadoAcceso == EstadoAccesoUsuario.Denegado),
                    _ => usuarios.Where(u => u.EstadoAcceso is EstadoAccesoUsuario.Aprobado or EstadoAccesoUsuario.Denegado)
                };

                return string.IsNullOrWhiteSpace(busquedaDirectorio)
                    ? filtrados
                    : filtrados.Where(u => Coincide(u, busquedaDirectorio.Trim()));
            }
        }

        private static bool Coincide(UsuarioResumenDto usuario, string busqueda) =>
            ContieneSinAcentos(usuario.Usuario, busqueda) || ContieneSinAcentos(usuario.Nombre, busqueda);

        private static bool ContieneSinAcentos(string texto, string busqueda) =>
            NormalizarParaBusqueda(texto).Contains(NormalizarParaBusqueda(busqueda), StringComparison.OrdinalIgnoreCase);

        private static string NormalizarParaBusqueda(string valor)
        {
            var normalizado = valor.Normalize(NormalizationForm.FormD);
            var sinDiacriticos = normalizado.Where(c =>
                CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);

            return new string(sinDiacriticos.ToArray()).Normalize(NormalizationForm.FormC);
        }

        private void CambiarFiltroDirectorio(FiltroDirectorio nuevo) => filtroDirectorio = nuevo;

        /// <summary>
        /// Iniciales para el círculo dorado: primera letra del primer y del último
        /// nombre/apellido, para que "Gerente Principal" dé "GP" en vez de "GE".
        /// </summary>
        private static string Iniciales(string nombre)
        {
            var partes = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return partes.Length switch
            {
                0 => "?",
                1 => partes[0][..1].ToUpperInvariant(),
                _ => (partes[0][..1] + partes[^1][..1]).ToUpperInvariant()
            };
        }

        /// <summary>dd/MM/yyyy, h:mm a.m./p.m. -- mismo formato acordado en Reposiciones.</summary>
        private static string FormatoFechaHora(DateTime fechaUtc)
        {
            var local = fechaUtc.ToLocalTime();
            var sufijo = local.Hour < 12 ? "a.m." : "p.m.";
            return local.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) + ", "
                + local.ToString("h:mm", CultureInfo.InvariantCulture) + " " + sufijo;
        }

        private async Task AprobarAsync(UsuarioResumenDto usuario, string? rol)
        {
            // El select arranca en la opción vacía; ignorarla evita aprobar sin rol.
            if (string.IsNullOrWhiteSpace(rol))
            {
                return;
            }

            await EjecutarAsync(
                usuario,
                () => IdentityService.AprobarAccesoAsync(usuario.Id, rol),
                $"'{usuario.Usuario}' ahora tiene acceso como {rol}.");
        }

        private async Task CambiarRolAsync(UsuarioResumenDto usuario, string? rol)
        {
            if (string.IsNullOrWhiteSpace(rol) || rol == usuario.Rol)
            {
                return;
            }

            await EjecutarAsync(
                usuario,
                () => IdentityService.CambiarRolAsync(usuario.Id, rol),
                $"'{usuario.Usuario}' pasó a ser {rol}.");
        }

        private async Task DenegarAsync(UsuarioResumenDto usuario) =>
            await EjecutarAsync(
                usuario,
                () => IdentityService.DenegarAccesoAsync(usuario.Id),
                $"Se le negó el acceso a '{usuario.Usuario}'.");

        /// <summary>
        /// Envuelve las tres operaciones: bloquea la fila mientras corre, traduce el
        /// resultado a un aviso y recarga la lista para que el usuario cambie de
        /// sección en pantalla.
        /// </summary>
        private async Task EjecutarAsync(
            UsuarioResumenDto usuario,
            Func<Task<Application.Common.Models.ResultadoIdentidad>> operacion,
            string mensajeExito)
        {
            mensaje = null;
            error = null;
            urlResetGenerada = null;
            copiadoAlPortapapeles = false;
            procesandoId = usuario.Id;

            try
            {
                var resultado = await operacion();

                if (resultado.Exitoso)
                {
                    mensaje = mensajeExito;
                }
                else
                {
                    error = string.Join(" ", resultado.Errores);
                }

                await RecargarAsync();
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            finally
            {
                procesandoId = null;
            }
        }

        private async Task IgnorarResetAsync(SolicitudPasswordResetResumenDto solicitud)
        {
            mensaje = null;
            error = null;
            urlResetGenerada = null;
            copiadoAlPortapapeles = false;
            procesandoResetId = solicitud.Id;

            try
            {
                var resultado = await PasswordResetService.IgnorarAsync(solicitud.Id);
                if (resultado.Exitoso)
                {
                    mensaje = $"Se ignoró la solicitud de '{solicitud.Usuario}'. Su contraseña no cambió.";
                }
                else
                {
                    error = string.Join(" ", resultado.Errores);
                }

                await RecargarAsync();
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            finally
            {
                procesandoResetId = null;
            }
        }

        private async Task AceptarResetAsync(SolicitudPasswordResetResumenDto solicitud)
        {
            mensaje = null;
            error = null;
            urlResetGenerada = null;
            copiadoAlPortapapeles = false;
            procesandoResetId = solicitud.Id;

            try
            {
                var administrador = await CurrentUserService.ObtenerAsync();
                var resultado = await PasswordResetService.AceptarAsync(solicitud.Id, administrador.Id ?? string.Empty);

                if (resultado.Exitoso)
                {
                    // El secreto va en la query (?t=) y no en la ruta: es lo unico que
                    // hace que este enlace, y no el Id solo (que el solicitante ya
                    // conoce), sirva para completar el cambio.
                    urlResetGenerada = NavigationManager.ToAbsoluteUri(
                        $"Account/ResetPassword/{resultado.Valor.SolicitudId}?t={Uri.EscapeDataString(resultado.Valor.Secreto)}").AbsoluteUri;
                }
                else
                {
                    error = string.Join(" ", resultado.Errores);
                }

                await RecargarAsync();
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            finally
            {
                procesandoResetId = null;
            }
        }

        private async Task CopiarUrlAsync()
        {
            if (urlResetGenerada is null)
            {
                return;
            }

            try
            {
                await JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", urlResetGenerada);
                copiadoAlPortapapeles = true;
            }
            catch (JSException)
            {
                // El portapapeles puede fallar por permisos del navegador o porque el
                // documento perdió el foco -- nunca debe tumbar el circuito completo
                // por un botón de conveniencia. El campo de texto de arriba sigue
                // siendo seleccionable a mano como respaldo.
                copiadoAlPortapapeles = false;
                error = "No se pudo copiar automáticamente. Seleccione el enlace y cópielo manualmente.";
            }
        }
    }
}

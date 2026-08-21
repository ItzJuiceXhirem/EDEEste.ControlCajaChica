using System.Collections.Generic;
using System.Linq;
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

        protected override async Task OnInitializedAsync() => await RecargarAsync();

        private async Task RecargarAsync()
        {
            usuarios = await IdentityService.ListarUsuariosAsync();
            solicitudesReset = await PasswordResetService.ListarPendientesAsync();
        }

        private IReadOnlyList<UsuarioResumenDto> Filtrar(EstadoAccesoUsuario estado) =>
            usuarios?.Where(u => u.EstadoAcceso == estado).ToList() ?? [];

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
            System.Func<Task<Application.Common.Models.ResultadoIdentidad>> operacion,
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
            catch (System.Exception ex)
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
            catch (System.Exception ex)
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
                    urlResetGenerada = NavigationManager.ToAbsoluteUri($"Account/ResetPassword/{resultado.Valor}").AbsoluteUri;
                }
                else
                {
                    error = string.Join(" ", resultado.Errores);
                }

                await RecargarAsync();
            }
            catch (System.Exception ex)
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


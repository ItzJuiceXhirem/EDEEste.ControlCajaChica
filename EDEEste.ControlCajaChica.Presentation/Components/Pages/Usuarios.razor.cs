using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace EDEEste.ControlCajaChica.Presentation.Components.Pages
{
    public partial class Usuarios
    {
        [Inject] private IIdentityService IdentityService { get; set; } = default!;

        private IReadOnlyList<UsuarioResumenDto>? usuarios;
        private string? procesandoId;
        private string? mensaje;
        private string? error;

        protected override async Task OnInitializedAsync() => await RecargarAsync();

        private async Task RecargarAsync() => usuarios = await IdentityService.ListarUsuariosAsync();

        private IReadOnlyList<UsuarioResumenDto> Filtrar(EstadoAccesoUsuario estado) =>
            usuarios?.Where(u => u.EstadoAcceso == estado).ToList() ?? [];

        private async Task AprobarAsync(UsuarioResumenDto usuario, string? rol)
        {
            // El select arranca en la opcion vacia; ignorarla evita aprobar sin rol.
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
                $"'{usuario.Usuario}' paso a ser {rol}.");
        }

        private async Task DenegarAsync(UsuarioResumenDto usuario) =>
            await EjecutarAsync(
                usuario,
                () => IdentityService.DenegarAccesoAsync(usuario.Id),
                $"Se le denego el acceso a '{usuario.Usuario}'.");

        /// <summary>
        /// Envuelve las tres operaciones: bloquea la fila mientras corre, traduce el
        /// resultado a un aviso y recarga la lista para que el usuario cambie de
        /// seccion en pantalla.
        /// </summary>
        private async Task EjecutarAsync(
            UsuarioResumenDto usuario,
            System.Func<Task<Application.Common.Models.ResultadoIdentidad>> operacion,
            string mensajeExito)
        {
            mensaje = null;
            error = null;
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
    }
}

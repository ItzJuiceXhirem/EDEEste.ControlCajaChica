using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    /* Unico punto de entrada de la aplicacion hacia ASP.NET Identity. Trabaja solo
       con identificadores y texto para que ni Application ni las paginas Razor
       tengan que depender de UserManager/RoleManager directamente */

    /* El inicio de sesion NO esta aqui a proposito: emitir la cookie es una
       responsabilidad de la capa web, asi que eso se queda en SignInManager */
    public interface IIdentityService
    {
        Task<string?> ObtenerNombreUsuarioAsync(string usuarioId);

        Task<bool> EstaEnRolAsync(string usuarioId, string rol);

        /* Crea el usuario y le asigna el rol de forma atomica: si falla la asignacion
           del rol se elimina el usuario, para no dejar cuentas sin rol.*/
        Task<ResultadoIdentidad> CrearUsuarioAsync(string usuario, string password, string nombre, string rol);

        /* Alta por auto-registro: crea la cuenta SIN rol y en estado Pendiente. Se
           separa de CrearUsuarioAsync porque aquel exige un rol y borra la cuenta si
           no puede asignarlo; aqui la ausencia de rol es justamente lo correcto
           hasta que un Administrador revise la solicitud. */
        Task<ResultadoIdentidad> CrearUsuarioPendienteAsync(string usuario, string password, string nombre);

        // crea el rol si aun no existe
        Task AsegurarRolAsync(string rol);

        // --- Gestion de accesos (pantalla del Administrador) ---

        Task<EstadoAccesoUsuario?> ObtenerEstadoAccesoAsync(string usuarioId);

        /// <summary>Asigna el rol indicado y deja la cuenta en Aprobado.</summary>
        Task<ResultadoIdentidad> AprobarAccesoAsync(string usuarioId, string rol);

        /// <summary>Pasa la cuenta a Denegado conservando el rol que tuviera.</summary>
        Task<ResultadoIdentidad> DenegarAccesoAsync(string usuarioId);

        /// <summary>Reemplaza el rol de una cuenta ya aprobada.</summary>
        Task<ResultadoIdentidad> CambiarRolAsync(string usuarioId, string nuevoRol);

        /// <summary>Si se pasa un estado, filtra por el; si no, devuelve todos.</summary>
        Task<IReadOnlyList<UsuarioResumenDto>> ListarUsuariosAsync(EstadoAccesoUsuario? estado = null);

        /// <summary>Gatilla la pantalla de configuracion inicial mientras sea false.</summary>
        Task<bool> ExisteAdministradorAsync();
    }
}

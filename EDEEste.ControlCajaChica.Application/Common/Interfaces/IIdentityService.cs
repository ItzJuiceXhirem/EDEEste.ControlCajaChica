using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Models;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    /// <summary>
    /// Unico punto de entrada de la aplicacion hacia ASP.NET Identity. Trabaja solo
    /// con identificadores y texto para que ni Application ni las paginas Razor
    /// tengan que depender de UserManager/RoleManager directamente.
    ///
    /// El inicio de sesion NO esta aqui a proposito: emitir la cookie es una
    /// responsabilidad de la capa web, asi que eso se queda en SignInManager.
    /// </summary>
    public interface IIdentityService
    {
        Task<string?> ObtenerNombreUsuarioAsync(string usuarioId);

        Task<bool> EstaEnRolAsync(string usuarioId, string rol);

        /// <summary>
        /// Crea el usuario y le asigna el rol de forma atomica: si falla la asignacion
        /// del rol se elimina el usuario, para no dejar cuentas sin rol.
        /// </summary>
        Task<ResultadoIdentidad> CrearUsuarioAsync(string email, string password, string nombre, string rol);

        /// <summary>Crea el rol si aun no existe. Es idempotente.</summary>
        Task AsegurarRolAsync(string rol);

        /// <summary>Token de confirmacion de correo, ya codificado para viajar en una URL.</summary>
        Task<string?> GenerarTokenConfirmacionEmailAsync(string usuarioId);

        /// <summary>Indica si la configuracion exige confirmar el correo antes de iniciar sesion.</summary>
        bool RequiereCuentaConfirmada { get; }
    }
}

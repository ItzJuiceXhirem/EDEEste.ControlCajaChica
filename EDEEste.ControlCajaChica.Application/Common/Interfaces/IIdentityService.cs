using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Models;

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
        Task<ResultadoIdentidad> CrearUsuarioAsync(string email, string password, string nombre, string rol);

        // crea el rol si aun no existe
        Task AsegurarRolAsync(string rol);

        // token de confirmacion de correo, ya codificado para viajar en una URL
        Task<string?> GenerarTokenConfirmacionEmailAsync(string usuarioId);

        // indica si la configuracion exige confirmar el correo antes de iniciar sesion
        bool RequiereCuentaConfirmada { get; }
    }
}

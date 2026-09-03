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

        /// <summary>
        /// Version en lote de <see cref="ObtenerNombreUsuarioAsync"/>: una sola
        /// consulta para varios ids en vez de una por id. Los ids que no
        /// correspondan a ningun usuario simplemente no aparecen en el
        /// resultado -- el llamador decide el valor de respaldo (normalmente el
        /// propio id) igual que hoy hace con el "?? usuarioId" del metodo singular.
        /// </summary>
        Task<IReadOnlyDictionary<string, string>> ObtenerNombresUsuarioAsync(IEnumerable<string> usuarioIds);

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

        /// <summary>
        /// Marca "ahora" como el ultimo acceso y devuelve la fecha de la sesion
        /// ANTERIOR a esta (null si es el primer inicio de sesion de la cuenta). No
        /// emite la cookie -- eso lo sigue haciendo SignInManager en la pagina de
        /// Login -- solo actualiza el dato. La implementacion debe escribir con un
        /// UPDATE dirigido a esa unica columna, sin pasar por SaveChanges: esto se
        /// llama en CADA login del sistema, y no debe poder generar una fila de
        /// bitacora ni arrastrar el resto de la fila del usuario (PasswordHash
        /// incluido).
        /// </summary>
        Task<DateTime?> RegistrarAccesoAsync(string usuarioId);
    }
}

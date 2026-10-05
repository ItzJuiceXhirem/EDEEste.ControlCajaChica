using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    /// <summary>
    /// Único punto de entrada de la aplicacion hacia ASP.NET Identity. Trabaja solo
    /// con identificadores y texto para que ni Application ni las páginas Razor
    /// tengan que depender de UserManager/RoleManager directamente.
    ///
    /// El inicio de sesión NO esta aquí a propósito: emitir la cookie es una
    /// responsabilidad de la capa web, así que eso se queda en SignInManager.
    /// </summary>
    public interface IIdentityService
    {
        Task<string?> ObtenerNombreUsuarioAsync(string usuarioId);

        /// <summary>
        /// Versión en lote de <see cref="ObtenerNombreUsuarioAsync"/>: una sola
        /// consulta para varios ids en vez de una por id. Los ids que no
        /// correspondan a ningún usuario simplemente no aparecen en el
        /// resultado -- el llamador decide el valor de respaldo (normalmente el
        /// propio id) igual que hoy hace con el "?? usuarioId" del método singular.
        /// </summary>
        Task<IReadOnlyDictionary<string, string>> ObtenerNombresUsuarioAsync(IEnumerable<string> usuarioIds);

        Task<bool> EstaEnRolAsync(string usuarioId, string rol);

        /* Crea el usuario y le asigna el rol de forma atómica: si falla la asignación
           del rol se elimina el usuario, para no dejar cuentas sin rol.*/
        Task<ResultadoIdentidad> CrearUsuarioAsync(string usuario, string password, string nombre, string rol);

        /* Alta por auto-registro: crea la cuenta SIN rol y en estado Pendiente. Se
           separa de CrearUsuarioAsync porque aquel exige un rol y borra la cuenta si
           no puede asignarlo; aqui la ausencia de rol es justamente lo correcto
           hasta que un Administrador revise la solicitud. */
        Task<ResultadoIdentidad> CrearUsuarioPendienteAsync(string usuario, string password, string nombre);

        // crea el rol si aún no existe
        Task AsegurarRolAsync(string rol);

        // --- Gestión de accesos (pantalla del Administrador) ---

        Task<EstadoAccesoUsuario?> ObtenerEstadoAccesoAsync(string usuarioId);

        // Asigna el rol indicado y deja la cuenta en Aprobado
        Task<ResultadoIdentidad> AprobarAccesoAsync(string usuarioId, string rol);

        // Pasa la cuenta a Denegado conservando el rol que tuviera
        Task<ResultadoIdentidad> DenegarAccesoAsync(string usuarioId);

        // Reemplaza el rol de una cuenta ya aprobada
        Task<ResultadoIdentidad> CambiarRolAsync(string usuarioId, string nuevoRol);

        // Si se pasa un estado, filtra por él; si no, devuelve todos
        Task<IReadOnlyList<UsuarioResumenDto>> ListarUsuariosAsync(EstadoAccesoUsuario? estado = null);

        // Gatilla la pantalla de configuración inicial mientras sea false
        Task<bool> ExisteAdministradorAsync();

        /// <summary>
        /// Marca "ahora" como el último acceso y devuelve la fecha de la sesión
        /// ANTERIOR a esta (null si es el primer inicio de sesión de la cuenta). No
        /// emite la cookie -- eso lo sigue haciendo SignInManager en la página de
        /// Login -- solo actualiza el dato. La implementación debe escribir con un
        /// UPDATE dirigido a esa única columna, sin pasar por SaveChanges: esto se
        /// llama en CADA login del sistema, y no debe poder generar una fila de
        /// bitácora ni arrastrar el resto de la fila del usuario (PasswordHash
        /// incluido).
        /// </summary>
        Task<DateTime?> RegistrarAccesoAsync(string usuarioId);

        // --- Foto de perfil ---

      /* Ruta relativa de la foto de perfil, o null si no tiene. Se usa para servir
         el archivo, para saber si pintar la foto o las iniciales, y para conocer la
         ruta anterior al reemplazarla (y poder borrar la vieja). */
        Task<string?> ObtenerRutaFotoPerfilAsync(string usuarioId);

        /// <summary>
        /// Fija (o limpia, con null) la ruta de la foto de perfil. Igual que
        /// <see cref="RegistrarAccesoAsync"/>, la implementación debe escribir con un
        /// UPDATE dirigido a esa única columna y no con UserManager.UpdateAsync: ese
        /// haría un SaveChanges de la fila entera, arrastrando PasswordHash y
        /// SecurityStamp por el ChangeTracker y disparando la bitácora de auditoría por
        /// un cambio de foto.
        /// </summary>
        Task ActualizarFotoPerfilAsync(string usuarioId, string? rutaRelativa);

        // --- Tema (claro/oscuro) ---

      /* "oscuro" o null (claro), tal cual esta guardado -- quien llama decide como
         tratar cualquier valor que no sea exactamente "oscuro" (ver App.razor). */
        Task<string?> ObtenerTemaPreferidoAsync(string usuarioId);

        /// <summary>
        /// Fija (o limpia, con null) el tema preferido. Mismo motivo que
        /// <see cref="ActualizarFotoPerfilAsync"/>: UPDATE dirigido a una sola
        /// columna, sin pasar por UserManager.UpdateAsync ni la bitácora de auditoría.
        /// </summary>
        Task ActualizarTemaPreferidoAsync(string usuarioId, string? tema);
    }
}

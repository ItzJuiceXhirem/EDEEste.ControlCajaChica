using System.Collections.Generic;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    /// <summary>
    /// Flujo de restablecimiento de contraseña mediado por el Administrador (V2:
    /// cuentas manuales). No expone SolicitudPasswordReset (vive en Infrastructure,
    /// igual que Usuario) ni el token de Identity: solo ids de solicitud como string,
    /// para que ni Application ni las páginas Razor dependan de esos tipos.
    /// </summary>
    public interface IPasswordResetService
    {
      /* Crea la solicitud (o reutiliza la que ya estuviera Pendiente para ese
         usuario) y devuelve su id. Null si el usuario no existe. */
        Task<string?> SolicitarAsync(string usuario);

        // Para el sondeo de la pantalla de espera. Null si el id no existe
        Task<EstadoSolicitudPasswordReset?> ObtenerEstadoAsync(string solicitudId);

        Task<IReadOnlyList<SolicitudPasswordResetResumenDto>> ListarPendientesAsync();

        /// <summary>
        /// Genera el token de Identity y un secreto de un solo uso, y pasa la
        /// solicitud a Aprobada. El secreto del resultado NUNCA se guarda (solo su
        /// hash) y no se le puede mostrar de nuevo a nadie después de esta llamada --
        /// quien llama es responsable de entregárselo al Administrador para que lo
        /// mande por un canal aparte (nunca al solicitante).
        /// </summary>
        Task<ResultadoOperacion<EnlaceRestablecimientoDto>> AceptarAsync(string solicitudId, string administradorId);

        Task<ResultadoOperacion<string>> IgnorarAsync(string solicitudId, string administradorId);

        /// <summary>
        /// Si la solicitud existe, está Aprobada, no expiró y el secreto coincide --
        /// lo que decide si /Account/ResetPassword/{id}?t={secreto} muestra el
        /// formulario. El Id solo no alcanza: sin el secreto correcto, esto es false
        /// aunque la solicitud esté aprobada.
        /// </summary>
        Task<bool> PuedeRestablecerAsync(string solicitudId, string secreto);

        // Aplica la nueva contraseña con el token guardado y marca la solicitud como Usada
        Task<ResultadoOperacion<string>> RestablecerAsync(string solicitudId, string secreto, string nuevaPassword);
    }
}

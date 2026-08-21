using System.Collections.Generic;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    /* Flujo de restablecimiento de contrasena mediado por el Administrador (V2:
       cuentas manuales). No expone SolicitudPasswordReset (vive en Infrastructure,
       igual que Usuario) ni el token de Identity: solo ids de solicitud como string,
       para que ni Application ni las paginas Razor dependan de esos tipos. */
    public interface IPasswordResetService
    {
        /// <summary>
        /// Crea la solicitud (o reutiliza la que ya estuviera Pendiente para ese
        /// usuario) y devuelve su id. Null si el usuario no existe.
        /// </summary>
        Task<string?> SolicitarAsync(string usuario);

        /// <summary>Para el sondeo de la pantalla de espera. Null si el id no existe.</summary>
        Task<EstadoSolicitudPasswordReset?> ObtenerEstadoAsync(string solicitudId);

        Task<IReadOnlyList<SolicitudPasswordResetResumenDto>> ListarPendientesAsync();

        /// <summary>
        /// Genera el token de Identity y pasa la solicitud a Aprobada. El Valor del
        /// resultado es el mismo solicitudId, para que quien llama pueda armar la URL
        /// unica sin volver a consultar.
        /// </summary>
        Task<ResultadoOperacion<string>> AceptarAsync(string solicitudId, string administradorId);

        Task<ResultadoOperacion<string>> IgnorarAsync(string solicitudId);

        /// <summary>Si la solicitud existe y esta Aprobada -- lo que decide si /Account/ResetPassword/{id} muestra el formulario.</summary>
        Task<bool> PuedeRestablecerAsync(string solicitudId);

        /// <summary>Aplica la nueva contrasena con el token guardado y marca la solicitud Usada.</summary>
        Task<ResultadoOperacion<string>> RestablecerAsync(string solicitudId, string nuevaPassword);
    }
}

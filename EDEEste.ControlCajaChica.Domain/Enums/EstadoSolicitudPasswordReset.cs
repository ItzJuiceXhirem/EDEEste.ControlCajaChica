using System;

namespace EDEEste.ControlCajaChica.Domain.Enums
{
    /// <summary>
    /// Estado de una solicitud de restablecimiento de contraseña (flujo V2, cuentas
    /// manuales). El Administrador decide entre Aprobada e Ignorada; Usada se marca
    /// sola cuando el usuario efectivamente cambia la contraseña, para que el enlace
    /// único deje de servir después de usarse una vez.
    /// </summary>
    public enum EstadoSolicitudPasswordReset
    {
        Pendiente = 1,
        Aprobada = 2,
        Ignorada = 3,
        Usada = 4
    }
}

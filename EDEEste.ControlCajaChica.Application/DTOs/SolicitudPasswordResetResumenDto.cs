using System;

namespace EDEEste.ControlCajaChica.Application.DTOs
{
    /// <summary>
    /// Vista de una solicitud de restablecimiento pendiente, para la seccion
    /// correspondiente de la pantalla de administracion de cuentas.
    /// </summary>
    public sealed record SolicitudPasswordResetResumenDto(
        string Id,
        string Usuario,
        string Nombre,
        DateTime FechaSolicitud);
}

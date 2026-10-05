using System;

namespace EDEEste.ControlCajaChica.Application.DTOs
{
  /* Vista de una solicitud de restablecimiento pendiente, para la sección
     correspondiente de la pantalla de administración de cuentas. */
    public sealed record SolicitudPasswordResetResumenDto(
        string Id,
        string Usuario,
        string Nombre,
        DateTime FechaSolicitud);
}

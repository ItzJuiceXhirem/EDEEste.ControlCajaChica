using System;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.DTOs
{
    /// <summary>
    /// Vista de un usuario para la pantalla de administracion de accesos. Aplana el
    /// rol (que en realidad vive en AspNetUserRoles) para no tener que consultarlo
    /// aparte por cada fila desde la pantalla.
    /// </summary>
    public sealed record UsuarioResumenDto(
        string Id,
        string Usuario,
        string Nombre,
        string? Rol,
        EstadoAccesoUsuario EstadoAcceso);
}

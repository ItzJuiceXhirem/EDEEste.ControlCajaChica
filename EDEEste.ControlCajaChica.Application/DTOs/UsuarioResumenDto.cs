using System;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.DTOs
{
    /// <summary>
    /// Vista de un usuario para la pantalla de administracion de accesos. Aplana el
    /// rol (que en realidad vive en AspNetUserRoles) para no tener que consultarlo
    /// aparte por cada fila desde la pantalla.
    /// </summary>
    /// <param name="TieneFoto">
    /// Si mostrar la foto de perfil (via el endpoint que la sirve) o las iniciales. Se
    /// expone como bandera y no como la ruta de almacenamiento: la pantalla nunca sirve
    /// el archivo por su cuenta, asi que la ruta interna no le aporta nada y no tiene
    /// por que salir de Infrastructure.
    /// </param>
    public sealed record UsuarioResumenDto(
        string Id,
        string Usuario,
        string Nombre,
        string? Rol,
        EstadoAccesoUsuario EstadoAcceso,
        DateTime FechaSolicitud,
        string? Extension,
        bool TieneFoto);
}

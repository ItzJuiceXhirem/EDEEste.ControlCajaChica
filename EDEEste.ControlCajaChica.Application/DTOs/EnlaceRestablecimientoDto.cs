namespace EDEEste.ControlCajaChica.Application.DTOs
{
    /// <summary>
    /// Lo que hace falta para armar la URL unica de restablecimiento. El Secreto
    /// viaja SOLO en este DTO, de ida, en el momento de Aprobar -- nunca se vuelve a
    /// poder recuperar despues (solo se guarda su hash), asi que si se pierde aqui,
    /// hay que aprobar de nuevo.
    /// </summary>
    public sealed record EnlaceRestablecimientoDto(string SolicitudId, string Secreto);
}

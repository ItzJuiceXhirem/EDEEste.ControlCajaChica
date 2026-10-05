namespace EDEEste.ControlCajaChica.Application.DTOs
{
    /// <summary>
    /// Lo que hace falta para armar la URL única de restablecimiento. El Secreto
    /// viaja SOLO en este DTO, de ida, en el momento de Aprobar -- nunca se vuelve a
    /// poder recuperar después (solo se guarda su hash), así que si se pierde aquí,
    /// hay que aprobar de nuevo.
    /// </summary>
    public sealed record EnlaceRestablecimientoDto(string SolicitudId, string Secreto);
}

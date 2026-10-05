namespace EDEEste.ControlCajaChica.Application.DTOs
{
    /// <summary>
    /// Resultado de comparar un archivo en disco con el hash registrado al guardarlo.
    /// Son tres estados y no un bool: "no hay hash registrado" no es ni integro ni
    /// alterado, y cada llamador decide que hacer en ese caso.
    /// </summary>
    public enum IntegridadArchivo
    {
        Integro = 1,
        Alterado = 2,
        SinHashRegistrado = 3
    }
}

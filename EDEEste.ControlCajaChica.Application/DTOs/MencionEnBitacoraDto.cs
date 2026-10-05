using System;

namespace EDEEste.ControlCajaChica.Application.DTOs
{
    /// <summary>
    /// La última vez que la bitácora registró algo sobre un registro: qué tabla, qué
    /// registro, qué acción, cuándo y quién. Es lo que se muestra de un registro que
    /// figura en la bitácora pero ya no existe en su tabla.
    /// </summary>
    public sealed class MencionEnBitacoraDto
    {
        public string NombreTabla { get; init; } = string.Empty;
        public string RegistroId { get; init; } = string.Empty;
        public long Secuencia { get; init; }
        public string TipoAccion { get; init; } = string.Empty;
        public DateTime FechaEjecucion { get; init; }
        public string UsuarioId { get; init; } = string.Empty;
    }
}

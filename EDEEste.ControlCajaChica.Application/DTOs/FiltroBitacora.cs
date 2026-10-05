using System;

namespace EDEEste.ControlCajaChica.Application.DTOs
{
    /// <summary>
    /// Criterios para consultar la bitácora de auditoría. Todo es opcional: un
    /// criterio vacío no filtra.
    /// </summary>
    public sealed class FiltroBitacora
    {
        public string? UsuarioId { get; init; }
        public string? NombreTabla { get; init; }

        // El estado de EF que registró la bitácora: "Added", "Modified" o "Deleted".
        public string? TipoAccion { get; init; }

        /* En UTC, como FechaEjecucion. Desde es inclusivo y Hasta exclusivo: para
           filtrar un día completo, Hasta es el inicio del día siguiente. */
        public DateTime? Desde { get; init; }
        public DateTime? Hasta { get; init; }

        // Coincidencia parcial: basta con pegar un trozo del Id del registro.
        public string? RegistroId { get; init; }
    }
}

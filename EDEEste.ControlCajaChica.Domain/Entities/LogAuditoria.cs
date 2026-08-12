using System;
using System.Collections.Generic;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public class LogAuditoria
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UsuarioId { get; set; } = string.Empty;
        public string TipoAccion { get; set; } = string.Empty;
        public string NombreTabla { get; set; } = string.Empty;
        public string RegistroId { get; set; } = string.Empty;
        public string? ValoresAnteriores { get; set; }
        public string? ValoresNuevos { get; set; }
        public DateTime FechaEjecucion { get; set; } = DateTime.UtcNow;
    }
}

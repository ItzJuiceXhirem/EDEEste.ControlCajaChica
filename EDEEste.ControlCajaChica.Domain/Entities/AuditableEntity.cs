using System;
using System.Collections.Generic;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public abstract class AuditableEntity
    {
        public string CreadoPorId { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; } //= DateTime.UtcNow;
        public string? ModificadoPorId { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public bool IsDeleted { get; set; }
    }
}
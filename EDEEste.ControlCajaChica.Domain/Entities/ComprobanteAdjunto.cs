using System;
using System.Collections.Generic;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public class ComprobanteAdjunto
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Gasto GastoId { get; set; } = new Gasto(); //FK
        public string NombreOriginal { get; set; } = string.Empty;
        public string RutaArchivo { get; set; } = string.Empty;
        public string TipoMime { get; set; } = string.Empty;
        public long TamanoBytes { get; set; }
        public string HashSHA256 { get; set; }
        public DateTime FechaSubida { get; set; } = DateTime.UtcNow;
    }
}

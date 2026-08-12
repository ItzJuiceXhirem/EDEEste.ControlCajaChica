using System;
using System.Collections.Generic;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public class ArqueoCaja
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid FondoCajaChicaId { get; set; } = Guid.NewGuid(); //FK
        public string CodigoArqueo { get; set; } = string.Empty;
        public DateTime FechaArqueo { get; set; } = DateTime.UtcNow;
        public decimal MontoEfectivoContado { get; set; }
        public decimal MontoComprobantesPendientes { get; set; }
        public decimal SaldoTeorico { get; set; }
        public decimal Diferencia { get; set; }
        public string Resultado { get; set; } = string.Empty;
        public string Observaciones { get; set; } = string.Empty;
        public string RealizadoPorUsuarioId { get; set; } = string.Empty; //FK
    }
}

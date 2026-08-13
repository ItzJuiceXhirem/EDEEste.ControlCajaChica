using EDEEste.ControlCajaChica.Domain.Enums;
using EDEEste.ControlCajaChica.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public class ArqueoCaja : AuditableEntity, ITamperProofEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public FondoCajaChica FondoCajaChicaId { get; set; } //FK
        public string CodigoArqueo { get; set; } = string.Empty;
        public DateTime FechaArqueo { get; set; }
        public decimal MontoEfectivoContado { get; set; }
        public decimal MontoComprobantesPendientes { get; set; }
        public decimal SaldoTeorico { get; set; }
        public decimal Diferencia { get; set; }
        public ResultadoArqueo Resultado { get; set; }
        public string? Observaciones { get; set; }
        public string RealizadoPorUsuarioId { get; set; } = string.Empty; //FK

        // Navegación
        public ICollection<DetalleArqueoDenominacion> DetallesDenominacion { get; set; } = new List<DetalleArqueoDenominacion>();
        public string HashFirma { get; set; } = string.Empty;

        public string ObtenerCadenaParaHash()
        {
            return $"{Id} | {MontoEfectivoContado} | {SaldoTeorico} | {Diferencia} | {Resultado}";
        }
    }
}

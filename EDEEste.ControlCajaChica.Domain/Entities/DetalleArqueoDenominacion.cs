using System;
using System.Collections.Generic;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public class DetalleArqueoDenominacion : AuditableEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ArqueoCajaId { get; set; }
        public ArqueoCaja? ArqueoCaja { get; set; }
        public decimal ValorDenominacion { get; set; } //Ej: 2000 pesos, 1000, 500
        public int Cantidad { get; set; }
        public decimal SubtotalDenominacion => ValorDenominacion * Cantidad;
    }
}

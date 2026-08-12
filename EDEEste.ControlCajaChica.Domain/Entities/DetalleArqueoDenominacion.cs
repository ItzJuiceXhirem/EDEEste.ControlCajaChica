using System;
using System.Collections.Generic;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public class DetalleArqueoDenominacion
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ArqueoCaja ArqueoCajaId { get; set; } = new ArqueoCaja(); //FK
        public decimal ValorDenominacion { get; set; }
        public int Cantidad { get; set; }
        public decimal SubtotalDenominacion { get; set; }
    }
}

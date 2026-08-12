using System;
using System.Collections.Generic;
using System.Text;
using EDEEste.ControlCajaChica.Domain.Interfaces;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public class Gasto : AuditableEntity, ITamperProofEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        //public FondoCajaChica FondoCajaChica { get; set; } //FK
        public CategoriaGasto CategoriaGasto { get; set; } //FK
        // public Guid? ReposicionId { get; set; } //FK
        public string Proveedor { get; set; }
        public string RNCProveedor { get; set; }
        public string NCF { get; set; } = string.Empty;
        public string? Concepto { get; set; }
        public decimal Subtotal { get; set; }
        public decimal MontoITBIS { get; set; }
        public decimal MontoTotal { get; set; }
        public DateTime FechaGasto { get; set; }
        public string EstadoGasto { get; set; }
        public string RegistradoPorUsuarioId { get; set; } = string.Empty; //FK
        public string HashFirma { get; set; } = string.Empty;

        public string ObtenerCadenaParaHash()
        {
            return $"{Id} | {MontoTotal} | {NCF} | {FechaGasto:0}";
        }
    }
}

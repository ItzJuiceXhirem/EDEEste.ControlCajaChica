using System;
using System.Collections.Generic;
using EDEEste.ControlCajaChica.Domain.Interfaces;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public class FondoCajaChica : AuditableEntity, ITamperProofEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public decimal BalanceActual { get; set; }
        public required decimal MontoFijo { get; set; }
        public decimal LimitePorGasto { get; set; }
        public decimal PorcentajeAlertaReposicion { get; set; }
        public string CustodioId { get; set; } = string.Empty; //FK
        public string Estado { get; set; }
        public string HashFirma { get; set; } = string.Empty;

        public string ObtenerCadenaParaHash()
        {
            return $"{Id} | {BalanceActual} | {MontoFijo} | {CustodioId}";
        }
    }
}

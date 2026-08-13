using EDEEste.ControlCajaChica.Domain.Enums;
using EDEEste.ControlCajaChica.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public class SolicitudReposicion : AuditableEntity, ITamperProofEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public FondoCajaChica FondoCajaChicaId { get; set; } //FK
        public string CodigoSolicitud { get; set; } = string.Empty;
        public decimal MontoReclamado { get; set; }
        public DateTime FechaSolicitud { get; set; }
        public string SolicitoUsuarioId { get; set; } = string.Empty; //FK
        public string? AprobadorUsuarioId { get; set; } //FK
        public DateTime? FechaAprobacion { get; set; }
        public string? PagadorUsuarioId { get; set; }//FK
        public DateTime? FechaPago { get; set; }
        public string? ReferenciaPago { get; set;}
        public string? RutaPdfConsolidado { get; set; }
        public EstadoReposicion Estado { get; set; }

        public string HashFirma { get; set; } = string.Empty;

        public string ObtenerCadenaParaHash()
        {
            return $"{Id} | {MontoReclamado} | {CodigoSolicitud} | {Estado}";
        }
    }
}

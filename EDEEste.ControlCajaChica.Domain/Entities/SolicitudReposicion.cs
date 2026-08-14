using EDEEste.ControlCajaChica.Domain.Common;
using EDEEste.ControlCajaChica.Domain.Enums;
using EDEEste.ControlCajaChica.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public class SolicitudReposicion : AuditableEntity, ITamperProofEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid FondoCajaChicaId { get; set; }
        public FondoCajaChica? FondoCajaChica { get; set; }

        public string CodigoSolicitud { get; set; } = string.Empty;
        public decimal MontoReclamado { get; set; }
        public DateTime FechaSolicitud { get; set; }
        public string SolicitoUsuarioId { get; set; } = string.Empty; //FK hacia Usuario (Identity)
        public string? GerenteUsuarioId { get; set; } //FK hacia Usuario (Identity)
        public DateTime? FechaAprobacion { get; set; }
        public string? FinanzasUsuarioId { get; set; } //FK hacia Usuario (Identity)
        public DateTime? FechaPago { get; set; }
        public string? ReferenciaPago { get; set;}
        public string? RutaPdfConsolidado { get; set; }
        public EstadoReposicion Estado { get; set; }

        // Gastos que entran en esta reposicion; es lo que alimenta el PDF consolidado.
        public ICollection<Gasto> Gastos { get; set; } = new List<Gasto>();

        public string HashFirma { get; set; } = string.Empty;

        [NotMapped]
        public bool IntegridadVerificada { get; set; } = true;

        // Se firma toda la cadena de aprobacion (quien solicito, quien aprobo, quien
        // pago y cuando) porque es justo lo que un fraude querria reescribir.
        public string ObtenerCadenaParaHash() =>
            new ConstructorFirma(nameof(SolicitudReposicion))
                .Agregar(Id)
                .Agregar(FondoCajaChicaId)
                .Agregar(CodigoSolicitud)
                .Agregar(MontoReclamado)
                .Agregar(FechaSolicitud)
                .Agregar(SolicitoUsuarioId)
                .Agregar(GerenteUsuarioId)
                .Agregar(FechaAprobacion)
                .Agregar(FinanzasUsuarioId)
                .Agregar(FechaPago)
                .Agregar(ReferenciaPago)
                .Agregar(RutaPdfConsolidado)
                .Agregar((long)Estado)
                .Agregar(IsDeleted)
                .ToString();
    }
}

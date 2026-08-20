using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using EDEEste.ControlCajaChica.Domain.Common;
using EDEEste.ControlCajaChica.Domain.Enums;
using EDEEste.ControlCajaChica.Domain.Interfaces;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public class Gasto : AuditableEntity, ITamperProofEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid FondoCajaChicaId { get; set; }
        public FondoCajaChica? FondoCajaChica { get; set; }

        public Guid CategoriaGastoId { get; set; }
        public CategoriaGasto? CategoriaGasto { get; set; }

        public Guid? ReposicionId { get; set; }
        public SolicitudReposicion? Reposicion { get; set; }
        public string Proveedor { get; set; } = string.Empty;

        public string RNCProveedor { get; set; } = string.Empty;
        public string NCF { get; set; } = string.Empty; 
        public string? Concepto { get; set; }
        public decimal Subtotal { get; set; }
        public decimal MontoITBIS { get; set; }
        public decimal MontoTotal { get; set; }
        public DateTime FechaGasto { get; set; }
        public EstadoGasto Estado { get; set; }

        // Por que se anulo el gasto. Lo escribe quien pide la anulacion (custodio) o
        // quien la ejecuta (gerente), y se limpia si la anulacion se revierte.
        public string? MotivoAnulacion { get; set; }

        public string RegistradoPorUsuarioId { get; set; } = string.Empty; // Identity

        // Navegación
        public ICollection<ComprobanteAdjunto> Comprobantes { get; set; } = new List<ComprobanteAdjunto>();

        public string HashFirma { get; set; } = string.Empty;

        [NotMapped]
        public bool IntegridadVerificada { get; set; } = true;

        public string ObtenerCadenaParaHash() =>
            new ConstructorFirma(nameof(Gasto))
                .Agregar(Id)
                .Agregar(FondoCajaChicaId)
                .Agregar(CategoriaGastoId)
                .Agregar(ReposicionId)
                .Agregar(Proveedor)
                .Agregar(RNCProveedor)
                .Agregar(NCF)
                .Agregar(Concepto)
                .Agregar(MotivoAnulacion)
                .Agregar(Subtotal)
                .Agregar(MontoITBIS)
                .Agregar(MontoTotal)
                .Agregar(FechaGasto)
                .Agregar((long)Estado)
                .Agregar(RegistradoPorUsuarioId)
                .Agregar(IsDeleted)
                .ToString();
    }
}

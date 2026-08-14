using EDEEste.ControlCajaChica.Domain.Common;
using EDEEste.ControlCajaChica.Domain.Enums;
using EDEEste.ControlCajaChica.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Entities
{
    public class ArqueoCaja : AuditableEntity, ITamperProofEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid FondoCajaChicaId { get; set; }
        public FondoCajaChica? FondoCajaChica { get; set; }

        public string CodigoArqueo { get; set; } = string.Empty;
        public DateTime FechaArqueo { get; set; }
        public decimal MontoEfectivoContado { get; set; }
        public decimal MontoComprobantesPendientes { get; set; }
        public decimal SaldoTeorico { get; set; }
        public decimal Diferencia { get; set; }
        public ResultadoArqueo Resultado { get; set; }
        public string? Observaciones { get; set; }
        public string RealizadoPorUsuarioId { get; set; } = string.Empty; //FK hacia Usuario (Identity)

        // Navegación
        public ICollection<DetalleArqueoDenominacion> DetallesDenominacion { get; set; } = new List<DetalleArqueoDenominacion>();

        public string HashFirma { get; set; } = string.Empty;

        [NotMapped]
        public bool IntegridadVerificada { get; set; } = true;

        public string ObtenerCadenaParaHash() =>
            new ConstructorFirma(nameof(ArqueoCaja))
                .Agregar(Id)
                .Agregar(FondoCajaChicaId)
                .Agregar(CodigoArqueo)
                .Agregar(FechaArqueo)
                .Agregar(MontoEfectivoContado)
                .Agregar(MontoComprobantesPendientes)
                .Agregar(SaldoTeorico)
                .Agregar(Diferencia)
                .Agregar((long)Resultado)
                .Agregar(Observaciones)
                .Agregar(RealizadoPorUsuarioId)
                .Agregar(IsDeleted)
                .ToString();
    }
}

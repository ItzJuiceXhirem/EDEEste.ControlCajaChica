using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using EDEEste.ControlCajaChica.Domain.Common;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Domain.Enums;
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

      /* Tope por gasto expresado como porcentaje del monto fijo. Se fija un 2.5%
         como punto de partida, pero el Administrador lo ajusta por fondo: un fondo
         de operaciones y uno de caja menuda no se rigen por el mismo número.
         Se guarda como porcentaje igual que PorcentajeAlertaReposicion. */
        public decimal PorcentajeMaximoPorGasto { get; set; } = LimitesFondo.TopePorGastoPorDefecto;

        public decimal PorcentajeAlertaReposicion { get; set; }
        public string CustodioId { get; set; } = string.Empty; //FK hacia Usuario (Identity)

        public EstadoFondo Estado { get; set; } = EstadoFondo.Activo;

        // Navegación
        public ICollection<Gasto> Gastos { get; set; } = new List<Gasto>();
        public ICollection<SolicitudReposicion> Reposiciones { get; set; } = new List<SolicitudReposicion>();
        public ICollection<ArqueoCaja> Arqueos { get; set; } = new List<ArqueoCaja>();

        public string HashFirma { get; set; } = string.Empty;

        [NotMapped]
        public bool IntegridadVerificada { get; set; } = true;

        public string ObtenerCadenaParaHash() =>
            new ConstructorFirma(nameof(FondoCajaChica))
                .Agregar(Id)
                .Agregar(BalanceActual)
                .Agregar(MontoFijo)
                .Agregar(LimitePorGasto)
                .Agregar(PorcentajeMaximoPorGasto)
                .Agregar(PorcentajeAlertaReposicion)
                .Agregar(CustodioId)
                .Agregar((long)Estado)
                .Agregar(IsDeleted)
                .ToString();
    }
}

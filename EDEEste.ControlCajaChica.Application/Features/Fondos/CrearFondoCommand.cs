using System;
using EDEEste.ControlCajaChica.Domain.Constants;

namespace EDEEste.ControlCajaChica.Application.Features.Fondos
{
    /// <summary>Crea un fondo de caja chica nuevo, con el efectivo completo asignado.</summary>
    public sealed class CrearFondoCommand
    {
        public decimal MontoFijo { get; set; }
        public decimal LimitePorGasto { get; set; }
        public decimal PorcentajeMaximoPorGasto { get; set; } = LimitesFondo.TopePorGastoPorDefecto;
        public decimal PorcentajeAlertaReposicion { get; set; } = LimitesFondo.AlertaReposicionPorDefecto;
        public string CustodioId { get; set; } = string.Empty;
    }
}

using System;

namespace EDEEste.ControlCajaChica.Application.Features.Fondos
{
    /// <summary>Crea un fondo de caja chica nuevo, con el efectivo completo asignado.</summary>
    public sealed class CrearFondoCommand
    {
        public decimal MontoFijo { get; set; }
        public decimal LimitePorGasto { get; set; }
        public decimal PorcentajeMaximoPorGasto { get; set; } = 2.5m;
        public decimal PorcentajeAlertaReposicion { get; set; } = 30m;
        public string CustodioId { get; set; } = string.Empty;
    }
}

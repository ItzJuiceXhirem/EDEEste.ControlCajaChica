using System;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Features.Fondos
{
    /// <summary>
    /// Actualiza los parámetros operativos de un fondo ya creado. MontoFijo y
    /// BalanceActual no aparecen aquí, y es deliberado: el fondo fijo es inmutable
    /// tras la creación, y el balance solo lo mueven los casos de uso operativos. Al
    /// no existir la propiedad, la regla no depende de que alguien se acuerde de validarla
    /// </summary>
    public sealed class ActualizarParametrosFondoCommand
    {
        public Guid FondoCajaChicaId { get; set; }
        public decimal LimitePorGasto { get; set; }
        public decimal PorcentajeMaximoPorGasto { get; set; }
        public decimal PorcentajeAlertaReposicion { get; set; }
        public string CustodioId { get; set; } = string.Empty;
        public EstadoFondo Estado { get; set; }
    }
}

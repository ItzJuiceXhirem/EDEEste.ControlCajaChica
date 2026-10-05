using System;
using System.Collections.Generic;
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

        private static readonly IReadOnlyList<EstadoFondo> DesdeEnReposicion =
        [
            EstadoFondo.EnReposicion,
            EstadoFondo.Inactivo
        ];

        private static readonly IReadOnlyList<EstadoFondo> DesdeActivoOInactivo =
        [
            EstadoFondo.Activo,
            EstadoFondo.Inactivo
        ];

        /// <summary>
        /// Los estados que el Administrador puede elegir a mano, según el estado en que
        /// está el fondo. EnReposicion lo pone y lo libera el propio sistema (al aprobar,
        /// pagar o rechazar una reposición), así que nunca se elige: puesto a mano, el
        /// estado dejaría de corresponder a una solicitud real.
        ///
        /// Desde EnReposicion solo se puede mantener o cerrar el fondo. Ponerlo Activo a
        /// mano lo dejaría con una reposición en camino y el estado equivocado. Cerrarlo
        /// sí se permite siempre: no tiene sentido reponer un fondo que se va a cerrar, y
        /// la solicitud en curso se rechaza sola (ver ActualizarParametrosFondoHandler).
        ///
        /// Vive aquí y no en la pantalla para que la lista que se ofrece y la regla que
        /// se valida sean la misma.
        /// </summary>
        public static IReadOnlyList<EstadoFondo> EstadosAsignablesDesde(EstadoFondo estadoActual) =>
            estadoActual == EstadoFondo.EnReposicion ? DesdeEnReposicion : DesdeActivoOInactivo;
    }
}

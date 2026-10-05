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

        /// <summary>
        /// Los unicos estados que el Administrador puede elegir a mano. EnReposicion y
        /// BloqueadaPorArqueo son estados que algun dia asignara el propio sistema, pero
        /// todavia nadie los escribe ni los libera: puestos a mano, el fondo quedaba sin
        /// poder registrar gastos, arquear ni pedir reposicion (los tres exigen Activo).
        /// </summary>
        public static readonly IReadOnlyList<EstadoFondo> EstadosAsignables =
        [
            EstadoFondo.Activo,
            EstadoFondo.Inactivo
        ];
    }
}

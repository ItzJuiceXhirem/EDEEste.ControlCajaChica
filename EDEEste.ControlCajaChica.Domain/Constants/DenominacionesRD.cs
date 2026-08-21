using System;
using System.Collections.Generic;

namespace EDEEste.ControlCajaChica.Domain.Constants
{
    /// <summary>
    /// Denominaciones del peso dominicano en circulación.
    ///
    /// Vive en Domain y no en la pantalla porque es un hecho del negocio: el handler
    /// del arqueo la usa para rechazar un conteo con una denominación que no existe,
    /// y esa validación no puede depender de que el formulario esté bien armado.
    /// </summary>
    public static class DenominacionesRD
    {
        public static readonly IReadOnlyList<decimal> Billetes =
        [
            2000m, 1000m, 500m, 200m, 100m, 50m
        ];

        public static readonly IReadOnlyList<decimal> Monedas =
        [
            25m, 10m, 5m, 1m
        ];

        /// <summary>De mayor a menor, que es el orden en que se cuenta una caja.</summary>
        public static readonly IReadOnlyList<decimal> Todas =
        [
            2000m, 1000m, 500m, 200m, 100m, 50m, 25m, 10m, 5m, 1m
        ];
    }
}

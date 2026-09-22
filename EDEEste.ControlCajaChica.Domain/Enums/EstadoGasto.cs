using System;
using System.Collections.Generic;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Enums
{
    public enum EstadoGasto
    {
        PendienteReposicion = 1,
        EnProcesoReposicion = 2,
        Repuesto = 3,
        Rechazado = 4,
        Anulado = 5,

        /// <summary>
        /// El custodio pidió anular el gasto y falta que el gerente lo confirme. El
        /// dinero NO vuelve al fondo todavía: si volviera aquí, un custodio podría
        /// inflar el fondo por su cuenta sin que nadie lo apruebe.
        /// </summary>
        AnulacionPendiente = 6
    }
}

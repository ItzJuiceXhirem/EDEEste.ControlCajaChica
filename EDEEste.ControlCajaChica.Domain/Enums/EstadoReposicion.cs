using System;
using System.Collections.Generic;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Enums
{
    public enum EstadoReposicion
    {
        Borrador = 1,
        PendienteAprobacion = 2,
        Aprobada = 3,
        Pagada = 4,
        Rechazada = 5,

        // Finanzas la devolvió al Gerente (con motivo) en vez de pagarla. El Gerente la
        // aprueba de nuevo (con motivo) o la rechaza. Se agrega al final para no
        // renumerar los estados que ya hay guardados.
        DevueltaPorFinanzas = 6
    }
}

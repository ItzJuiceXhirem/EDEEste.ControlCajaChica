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
        Anulado = 5
    }
}

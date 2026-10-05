using System;

namespace EDEEste.ControlCajaChica.Application.Features.Gastos
{
    // Devuelve un gasto con anulación pendiente a PendienteReposicion
    public sealed class RevertirAnulacionGastoCommand
    {
        public Guid GastoId { get; set; }
    }
}

using System;

namespace EDEEste.ControlCajaChica.Application.Features.Gastos
{
    /// <summary>Devuelve un gasto con anulacion pendiente a PendienteReposicion.</summary>
    public sealed class RevertirAnulacionGastoCommand
    {
        public Guid GastoId { get; set; }
    }
}

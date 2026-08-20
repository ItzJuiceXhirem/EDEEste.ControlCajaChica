using System;

namespace EDEEste.ControlCajaChica.Application.Features.Gastos
{
    /// <summary>
    /// Pide anular un gasto. No lo anula: lo deja en AnulacionPendiente hasta que el
    /// Gerente lo confirme o lo revierta.
    /// </summary>
    public sealed class SolicitarAnulacionGastoCommand
    {
        public Guid GastoId { get; set; }
        public string Motivo { get; set; } = string.Empty;
    }
}

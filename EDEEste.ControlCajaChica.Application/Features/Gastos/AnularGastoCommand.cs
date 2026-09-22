using System;

namespace EDEEste.ControlCajaChica.Application.Features.Gastos
{
    /// <summary>
    /// Anula un gasto. Cubre dos casos con el mismo comando porque son la misma
    /// decisión del mismo rol (Gerente) sobre el mismo expediente:
    ///  - Anulación directa, desde PendienteReposicion.
    ///  - Confirmación de una anulación que el Custodio ya había pedido, desde
    ///    AnulacionPendiente.
    /// En ambos casos es el momento en que el dinero vuelve al fondo.
    /// </summary>
    public sealed class AnularGastoCommand
    {
        public Guid GastoId { get; set; }

        /// <summary>
        /// Obligatorio en la anulación directa. Al confirmar una anulación que ya
        /// venía pedida, es opcional: si se deja vacío se conserva el motivo que
        /// escribió el Custodio, y si se escribe algo lo reemplaza.
        /// </summary>
        public string? Motivo { get; set; }
    }
}

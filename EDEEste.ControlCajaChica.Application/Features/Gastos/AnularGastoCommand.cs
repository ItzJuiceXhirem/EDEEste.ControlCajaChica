using System;

namespace EDEEste.ControlCajaChica.Application.Features.Gastos
{
    /// <summary>
    /// Anula un gasto. Cubre dos casos con el mismo comando porque son la misma
    /// decision del mismo rol (Gerente) sobre el mismo expediente:
    ///  - Anulacion directa, desde PendienteReposicion.
    ///  - Confirmacion de una anulacion que el Custodio ya habia pedido, desde
    ///    AnulacionPendiente.
    /// En ambos casos es el momento en que el dinero vuelve al fondo.
    /// </summary>
    public sealed class AnularGastoCommand
    {
        public Guid GastoId { get; set; }

        /// <summary>
        /// Obligatorio en la anulacion directa. Al confirmar una anulacion que ya
        /// venia pedida, es opcional: si se deja vacio se conserva el motivo que
        /// escribio el Custodio, y si se escribe algo lo reemplaza.
        /// </summary>
        public string? Motivo { get; set; }
    }
}

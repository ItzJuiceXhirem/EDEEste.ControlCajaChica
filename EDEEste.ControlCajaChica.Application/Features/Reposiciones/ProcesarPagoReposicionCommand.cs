using System;

namespace EDEEste.ControlCajaChica.Application.Features.Reposiciones
{
    /// <summary>Registra el pago de una solicitud de reposicion ya aprobada.</summary>
    public sealed class ProcesarPagoReposicionCommand
    {
        public Guid ReposicionId { get; set; }

        /// <summary>
        /// Numero de transferencia, cheque o asiento. Obligatorio: es la prueba
        /// externa de que el dinero salio de tesoreria.
        /// </summary>
        public string ReferenciaPago { get; set; } = string.Empty;
    }
}

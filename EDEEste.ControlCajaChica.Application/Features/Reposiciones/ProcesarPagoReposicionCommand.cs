using System;

namespace EDEEste.ControlCajaChica.Application.Features.Reposiciones
{
    // Registra el pago de una solicitud de reposición ya aprobada
    public sealed class ProcesarPagoReposicionCommand
    {
        public Guid ReposicionId { get; set; }

      /* Número de transferencia, cheque o asiento. Obligatorio: es la prueba
         externa de que el dinero salió de tesorería. */
        public string ReferenciaPago { get; set; } = string.Empty;
    }
}

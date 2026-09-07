using System;
using System.Collections.Generic;

namespace EDEEste.ControlCajaChica.Application.Features.Arqueos
{
    /// <summary>
    /// Registra el conteo fisico de un fondo por denominacion, para compararlo contra
    /// el saldo teorico (BalanceActual).
    /// </summary>
    public sealed class RegistrarArqueoMensualCommand
    {
        public Guid FondoCajaChicaId { get; set; }
        public DateTime FechaArqueo { get; set; } = DateTime.Today;
        public string? Observaciones { get; set; }
        public List<ConteoDenominacion> Denominaciones { get; set; } = new();

        public sealed class ConteoDenominacion
        {
            public decimal ValorDenominacion { get; set; }
            public int Cantidad { get; set; }
        }
    }
}

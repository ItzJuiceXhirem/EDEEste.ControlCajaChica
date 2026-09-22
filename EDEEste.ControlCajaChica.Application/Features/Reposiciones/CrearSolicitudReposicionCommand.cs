using System;

namespace EDEEste.ControlCajaChica.Application.Features.Reposiciones
{
    /// <summary>
    /// Solicita la reposición de todos los gastos pendientes de un fondo. No recibe
    /// la lista de gastos a propósito: se toman todos los que estén pendientes al
    /// momento, para que no se pueda armar una reposición "a la carta" dejando
    /// facturas incómodas fuera del expediente.
    /// </summary>
    public sealed class CrearSolicitudReposicionCommand
    {
        public Guid FondoCajaChicaId { get; set; }
    }
}

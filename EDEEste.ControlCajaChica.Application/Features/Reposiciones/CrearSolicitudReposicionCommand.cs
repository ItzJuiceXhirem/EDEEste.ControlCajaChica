using System;

namespace EDEEste.ControlCajaChica.Application.Features.Reposiciones
{
    /// <summary>
    /// Solicita la reposicion de todos los gastos pendientes de un fondo. No recibe
    /// la lista de gastos a proposito: se toman todos los que esten pendientes al
    /// momento, para que no se pueda armar una reposicion "a la carta" dejando
    /// facturas incomodas fuera del expediente.
    /// </summary>
    public sealed class CrearSolicitudReposicionCommand
    {
        public Guid FondoCajaChicaId { get; set; }
    }
}

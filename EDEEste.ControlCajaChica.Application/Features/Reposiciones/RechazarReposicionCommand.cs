using System;

namespace EDEEste.ControlCajaChica.Application.Features.Reposiciones
{
    /// <summary>
    /// Rechaza una solicitud de reposición: la pendiente de aprobación, o la que
    /// Finanzas devolvió.
    /// </summary>
    public sealed class RechazarReposicionCommand
    {
        public Guid ReposicionId { get; set; }

      /* Obligatorio al rechazar una solicitud pendiente: es lo que el Custodio lee para
         saber qué corregir. Se ignora si la solicitud viene devuelta por Finanzas: ahí
         el motivo del rechazo es el que Finanzas ya dio (MotivoDevolucion), no uno
         nuevo del Gerente. */
        public string? Motivo { get; set; }
    }
}

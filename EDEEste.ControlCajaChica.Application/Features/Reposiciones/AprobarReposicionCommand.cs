using System;

namespace EDEEste.ControlCajaChica.Application.Features.Reposiciones
{
    /// <summary>
    /// Aprueba una solicitud de reposición: la pendiente de aprobación, o la que
    /// Finanzas devolvió. Rechazar es otro comando (RechazarReposicionCommand): son
    /// decisiones con reglas de motivo distintas y, separadas, ningún handler necesita
    /// una bandera que cambie qué se valida.
    /// </summary>
    public sealed class AprobarReposicionCommand
    {
        public Guid ReposicionId { get; set; }

      /* Solo se exige al aprobar de nuevo una solicitud devuelta por Finanzas: es lo que
         le dice a Finanzas por qué vuelve a recibirla. La primera aprobación no lleva
         motivo (la pantalla solo pide confirmar, contra el clic equivocado). */
        public string? Motivo { get; set; }
    }
}

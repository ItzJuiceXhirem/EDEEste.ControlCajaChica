using System;

namespace EDEEste.ControlCajaChica.Application.Features.Reposiciones
{
    /// <summary>
    /// Finanzas devuelve al Gerente una solicitud ya aprobada, en vez de pagarla.
    /// </summary>
    public sealed class DevolverReposicionCommand
    {
        public Guid ReposicionId { get; set; }

        // Obligatorio: es lo que lee el Gerente para decidir si aprueba de nuevo o rechaza.
        public string Motivo { get; set; } = string.Empty;
    }
}

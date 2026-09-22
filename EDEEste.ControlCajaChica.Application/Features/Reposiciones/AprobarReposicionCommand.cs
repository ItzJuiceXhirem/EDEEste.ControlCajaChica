using System;

namespace EDEEste.ControlCajaChica.Application.Features.Reposiciones
{
    /// <summary>
    /// Aprueba o rechaza una solicitud de reposición pendiente de aprobación. Es un
    /// solo comando y no dos porque son la misma decisión del mismo rol (Gerente)
    /// sobre el mismo expediente.
    /// </summary>
    public sealed class AprobarReposicionCommand
    {
        public Guid ReposicionId { get; set; }

        // true aprueba, false rechaza
        public bool Aprobar { get; set; }
    }
}

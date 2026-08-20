using System;

namespace EDEEste.ControlCajaChica.Application.Features.Reposiciones
{
    /// <summary>
    /// Aprueba o rechaza una solicitud de reposicion pendiente de aprobacion. Es un
    /// solo comando y no dos porque son la misma decision del mismo rol (Gerente)
    /// sobre el mismo expediente.
    /// </summary>
    public sealed class AprobarReposicionCommand
    {
        public Guid ReposicionId { get; set; }

        /// <summary>true aprueba, false rechaza.</summary>
        public bool Aprobar { get; set; }
    }
}

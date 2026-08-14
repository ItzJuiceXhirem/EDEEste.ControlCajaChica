using System;
using System.Collections.Generic;
using System.Linq;

namespace EDEEste.ControlCajaChica.Application.Common.Models
{
    /// <summary>
    /// Resultado de una operacion de identidad, sin filtrar tipos de ASP.NET Identity
    /// (IdentityResult / IdentityError) hacia las capas de arriba.
    /// </summary>
    public sealed record ResultadoIdentidad(bool Exitoso, string? UsuarioId, IReadOnlyList<string> Errores)
    {
        public static ResultadoIdentidad Ok(string usuarioId) =>
            new(true, usuarioId, Array.Empty<string>());

        public static ResultadoIdentidad Fallo(IEnumerable<string> errores) =>
            new(false, null, errores.ToArray());

        public static ResultadoIdentidad Fallo(string error) =>
            new(false, null, new[] { error });
    }
}

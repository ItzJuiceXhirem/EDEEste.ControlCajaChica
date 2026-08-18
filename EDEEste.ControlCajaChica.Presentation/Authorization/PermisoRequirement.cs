using Microsoft.AspNetCore.Authorization;

namespace EDEEste.ControlCajaChica.Presentation.Authorization
{
    /// <summary>
    /// Exige un permiso concreto del catalogo <see cref="Domain.Constants.Permisos"/>.
    /// Se registra una politica por permiso en Program.cs, de modo que las paginas
    /// escriben [Authorize(Policy = Permisos.RegistrarGasto)] y nunca nombran un rol.
    /// </summary>
    public sealed class PermisoRequirement : IAuthorizationRequirement
    {
        public PermisoRequirement(string permiso) => Permiso = permiso;

        public string Permiso { get; }
    }
}

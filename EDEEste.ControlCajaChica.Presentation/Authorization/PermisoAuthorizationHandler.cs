using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace EDEEste.ControlCajaChica.Presentation.Authorization
{
    /// <summary>
    /// Resuelve un permiso a partir del rol que trae la cookie, consultando el mapa
    /// <see cref="PermisosPorRol"/>.
    ///
    /// Los permisos no se guardan como claims a propósito: si se hornearan en la
    /// cookie, cambiar la matriz de accesos obligaría a que todos vuelvan a iniciar
    /// sesión para que surta efecto. Leyendo el mapa en cada comprobación, el cambio
    /// aplica al instante y la matriz sigue viviendo en un solo lugar.
    /// </summary>
    public sealed class PermisoAuthorizationHandler : AuthorizationHandler<PermisoRequirement>
    {
        private readonly string _tipoClaimRol;

        public PermisoAuthorizationHandler(IOptions<IdentityOptions> opciones) =>
            _tipoClaimRol = opciones.Value.ClaimsIdentity.RoleClaimType;

        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context, PermisoRequirement requirement)
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                return Task.CompletedTask;
            }

            // Hoy cada cuenta tiene un solo rol, pero se recorren todos por si en el
            // futuro se permite más de uno: basta con que alguno otorgue el permiso.
            var roles = context.User.FindAll(_tipoClaimRol)
                .Concat(context.User.FindAll(ClaimTypes.Role))
                .Select(c => c.Value);

            if (roles.Any(rol => PermisosPorRol.RolTienePermiso(rol, requirement.Permiso)))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}

using EDEEste.ControlCajaChica.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Microsoft.AspNetCore.Routing
{
    /// <summary>
    /// Endpoints que necesitan las páginas de /Components/Account/Pages.
    ///
    /// Del scaffold original solo queda el cierre de sesión. Se eliminaron los de
    /// inicio de sesión externo, passkeys y descarga de datos personales junto con
    /// sus páginas: este sistema autentica contra cuentas propias (y en el futuro
    /// contra el Active Directory de la empresa), así que esas rutas eran superficie
    /// expuesta sin nada detrás.
    /// </summary>
    internal static class IdentityComponentsEndpointRouteBuilderExtensions
    {
        public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            var accountGroup = endpoints.MapGroup("/Account");

            accountGroup.MapPost("/Logout", async (
                ClaimsPrincipal user,
                [FromServices] SignInManager<Usuario> signInManager,
                [FromForm] string returnUrl) =>
            {
                await signInManager.SignOutAsync();
                return TypedResults.LocalRedirect($"~/{returnUrl}");
            });

            return accountGroup;
        }
    }
}

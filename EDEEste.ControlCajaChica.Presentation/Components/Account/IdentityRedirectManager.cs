using EDEEste.ControlCajaChica.Infrastructure.Identity;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace EDEEste.ControlCajaChica.Presentation.Components.Account
{
    internal sealed class IdentityRedirectManager(NavigationManager navigationManager)
    {
        public const string StatusCookieName = "Identity.StatusMessage";

        private static readonly CookieBuilder StatusCookieBuilder = new()
        {
            SameSite = SameSiteMode.Strict,
            HttpOnly = true,
            IsEssential = true,
            MaxAge = TimeSpan.FromSeconds(5),
        };

        public void RedirectTo(string? uri)
        {
            uri ??= "";

            // "//evil.com" o "/\evil.com" (network-path reference, RFC 3986) pasan la
            // comprobacion de mas abajo: Uri.IsWellFormedUriString los acepta como
            // relativos, asi que NavigateTo los recibia intactos. El navegador si los
            // resuelve como cambio de host -- era el hueco real: ReturnUrl en /Account/Login
            // llega tal cual del query string hasta aqui.
            if (EmpiezaComoUriExterno(uri))
            {
                uri = "";
            }

            // Prevent open redirects.
            if (!Uri.IsWellFormedUriString(uri, UriKind.Relative))
            {
                uri = navigationManager.ToBaseRelativePath(uri);
            }

            navigationManager.NavigateTo(uri);
        }

        private static bool EmpiezaComoUriExterno(string uri) =>
            uri.StartsWith("//", StringComparison.Ordinal) || uri.StartsWith("/\\", StringComparison.Ordinal);

        public void RedirectTo(string uri, Dictionary<string, object?> queryParameters)
        {
            var uriWithoutQuery = navigationManager.ToAbsoluteUri(uri).GetLeftPart(UriPartial.Path);
            var newUri = navigationManager.GetUriWithQueryParameters(uriWithoutQuery, queryParameters);
            RedirectTo(newUri);
        }

        public void RedirectToWithStatus(string uri, string message, HttpContext context)
        {
            context.Response.Cookies.Append(StatusCookieName, message, StatusCookieBuilder.Build(context));
            RedirectTo(uri);
        }

        private string CurrentPath => navigationManager.ToAbsoluteUri(navigationManager.Uri).GetLeftPart(UriPartial.Path);

        public void RedirectToCurrentPage() => RedirectTo(CurrentPath);

        public void RedirectToCurrentPageWithStatus(string message, HttpContext context)
            => RedirectToWithStatus(CurrentPath, message, context);

        public void RedirectToInvalidUser(UserManager<Usuario> userManager, HttpContext context)
            => RedirectToWithStatus(
                "Account/InvalidUser",
                $"Error: Unable to load user with ID '{userManager.GetUserId(context.User)}'.",
                context);
    }
}

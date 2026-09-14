using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using Microsoft.AspNetCore.Components.Authorization;

namespace EDEEste.ControlCajaChica.Presentation.Services
{
    /// <summary>
    /// Resuelve el usuario autenticado. Vive en Presentation porque depende del
    /// HttpContext y del AuthenticationStateProvider de Blazor, que son detalles de
    /// la capa web; Infrastructure no debería (ni podía) referenciarlos.
    ///
    /// Se consultan las dos fuentes porque ninguna sirve sola:
    ///  - Durante el render estático y en los endpoints de /Account hay HttpContext.
    ///  - Dentro de un circuito interactivo el HttpContext ya no existe y el estado
    ///    solo está en el AuthenticationStateProvider.
    /// </summary>
    public sealed class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AuthenticationStateProvider _authenticationStateProvider;

        public CurrentUserService(
            IHttpContextAccessor httpContextAccessor,
            AuthenticationStateProvider authenticationStateProvider)
        {
            _httpContextAccessor = httpContextAccessor;
            _authenticationStateProvider = authenticationStateProvider;
        }

        public async Task<UsuarioActual> ObtenerAsync(CancellationToken cancellationToken = default)
        {
            var principal = _httpContextAccessor.HttpContext?.User;

            if (!EstaAutenticado(principal))
            {
                principal = await ObtenerDesdeProveedorAsync();
            }

            if (principal?.Identity is not { IsAuthenticated: true } identidad)
            {
                return UsuarioActual.Anonimo;
            }

            var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            return new UsuarioActual(id, identidad.Name, EstaAutenticado: true);
        }

        private async Task<ClaimsPrincipal?> ObtenerDesdeProveedorAsync()
        {
            try
            {
                var estado = await _authenticationStateProvider.GetAuthenticationStateAsync();
                return estado.User;
            }
            catch (InvalidOperationException)
            {
                // El provider lanza si se le pide el estado fuera de un circuito y sin
                // que nadie lo haya inicializado (por ejemplo, durante el registro de
                // un usuario). En ese caso simplemente no hay usuario que atribuir.
                return null;
            }
        }

        private static bool EstaAutenticado(ClaimsPrincipal? principal) =>
            principal?.Identity?.IsAuthenticated == true;
    }
}

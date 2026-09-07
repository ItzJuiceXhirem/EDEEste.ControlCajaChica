using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace EDEEste.ControlCajaChica.Presentation.Services
{
    /// <summary>
    /// Mismo patron de doble fuente que CurrentUserService (ver ese archivo): fuera de
    /// un circuito interactivo hay HttpContext; dentro de uno, el estado solo vive en
    /// el AuthenticationStateProvider.
    /// </summary>
    public sealed class AutorizacionService : IAutorizacionService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AuthenticationStateProvider _authenticationStateProvider;
        private readonly IAuthorizationService _authorizationService;

        public AutorizacionService(
            IHttpContextAccessor httpContextAccessor,
            AuthenticationStateProvider authenticationStateProvider,
            IAuthorizationService authorizationService)
        {
            _httpContextAccessor = httpContextAccessor;
            _authenticationStateProvider = authenticationStateProvider;
            _authorizationService = authorizationService;
        }

        public async Task<bool> TienePermisoAsync(string permiso, CancellationToken cancellationToken = default)
        {
            var principal = _httpContextAccessor.HttpContext?.User;

            if (principal?.Identity?.IsAuthenticated != true)
            {
                principal = await ObtenerDesdeProveedorAsync();
            }

            if (principal?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            var resultado = await _authorizationService.AuthorizeAsync(principal, permiso);
            return resultado.Succeeded;
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
                // Mismo caso que CurrentUserService: pedir el estado fuera de un
                // circuito y sin que nadie lo haya inicializado. Sin usuario, no hay
                // permiso que otorgar.
                return null;
            }
        }
    }
}

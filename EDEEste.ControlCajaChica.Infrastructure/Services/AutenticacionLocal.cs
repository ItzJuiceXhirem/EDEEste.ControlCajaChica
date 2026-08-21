using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace EDEEste.ControlCajaChica.Infrastructure.Services
{
    /// <summary>
    /// V2: la contrasena es propia de esta aplicacion y la verifica ASP.NET Identity.
    /// Es el modo activo mientras no llegue la API Key del APICommon.
    /// </summary>
    public sealed class AutenticacionLocal : IAutenticadorCredenciales
    {
        private readonly UserManager<Usuario> _userManager;

        public AutenticacionLocal(UserManager<Usuario> userManager) => _userManager = userManager;

        public async Task<ResultadoAutenticacion> ValidarAsync(
            string usuario,
            string password,
            CancellationToken cancellationToken = default)
        {
            var entidad = await _userManager.FindByNameAsync(usuario);

            // CheckPasswordAsync no mira el estado de la cuenta (aprobada, denegada,
            // bloqueada): eso lo decide quien llama, despues. Aqui solo interesa si la
            // contrasena es la correcta.
            return entidad is not null && await _userManager.CheckPasswordAsync(entidad, password)
                ? ResultadoAutenticacion.Ok()
                : ResultadoAutenticacion.Fallo();
        }
    }
}

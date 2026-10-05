using System;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Models;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    /* Resuelve quién esta autenticado. La implementación vive en Presentation
       porque depende del HttpContext / del circuito de Blazor.*/
    public interface ICurrentUserService
    {
        /// <summary>
        /// Es asíncrono porque en un circuito interactivo de Blazor Server ya no hay
        /// HttpContext y el estado hay que pedírselo al AuthenticationStateProvider.
        /// Nunca lanza: si no hay nadie autenticado devuelve <see cref="UsuarioActual.Anonimo"/>.
        /// </summary>
        Task<UsuarioActual> ObtenerAsync(CancellationToken cancellationToken = default);
    }
}

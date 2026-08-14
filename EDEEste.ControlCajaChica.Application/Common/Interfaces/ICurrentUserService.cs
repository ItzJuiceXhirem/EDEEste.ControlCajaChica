using System;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Models;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    /// <summary>
    /// Resuelve quien esta autenticado. La implementacion vive en Presentation
    /// porque depende del HttpContext / del circuito de Blazor.
    /// </summary>
    public interface ICurrentUserService
    {
        /// <summary>
        /// Es asincrono porque en un circuito interactivo de Blazor Server ya no hay
        /// HttpContext y el estado hay que pedirselo al AuthenticationStateProvider.
        /// Nunca lanza: si no hay nadie autenticado devuelve <see cref="UsuarioActual.Anonimo"/>.
        /// </summary>
        Task<UsuarioActual> ObtenerAsync(CancellationToken cancellationToken = default);
    }
}

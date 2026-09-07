using System.Threading;
using System.Threading.Tasks;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    /// <summary>
    /// Comprueba si quien ejecuta la operacion actual tiene un permiso del catalogo
    /// (ver Domain.Constants.Permisos). Es la MISMA comprobacion que ya hace la
    /// pantalla via [Authorize(Policy = ...)], repetida aqui como defensa en
    /// profundidad: los handlers de dinero no deben depender solo de que la UI los
    /// haya protegido bien, para el dia que se llamen desde otro sitio (una API, una
    /// tarea programada, un test que se equivoca de handler).
    ///
    /// La implementacion vive en Presentation porque depende de ClaimsPrincipal /
    /// IAuthorizationService de ASP.NET Core, que son detalles de la capa web -- igual
    /// que ICurrentUserService.
    /// </summary>
    public interface IAutorizacionService
    {
        Task<bool> TienePermisoAsync(string permiso, CancellationToken cancellationToken = default);
    }
}

using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Models;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    /// <summary>
    /// Verifica que un usuario y contraseña sean correctos. Es el único punto del
    /// sistema que sabe DÓNDE viven las contrasenas.
    ///
    /// Existe para poder cambiar entre las dos versiones de autenticación sin tocar
    /// la pantalla de login:
    ///
    /// - V2 (hoy): las contraseñas son propias de esta aplicación y las valida
    ///   ASP.NET Identity.
    /// - V1 (cuando llegue la API Key): las valida el Active Directory de la empresa
    ///   a traves del APICommon, y aquí las contraseñas dejan de existir.
    ///
    /// Se elige con la clave de configuración <c>Autenticacion:Modo</c>.
    ///
    /// Comprobar la contraseña es lo único que hace: si la cuenta está aprobada, si
    /// tiene rol, etc. son decisiones de la aplicación que se resuelven aparte y que
    /// valen igual en cualquiera de los dos modos.
    /// </summary>
    public interface IAutenticadorCredenciales
    {
        Task<ResultadoAutenticacion> ValidarAsync(
            string usuario,
            string password,
            CancellationToken cancellationToken = default);
    }
}

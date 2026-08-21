using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Models;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    /// <summary>
    /// Verifica que un usuario y contrasena sean correctos. Es el unico punto del
    /// sistema que sabe DONDE viven las contrasenas.
    ///
    /// Existe para poder cambiar entre las dos versiones de autenticacion sin tocar
    /// la pantalla de login:
    ///
    /// - V2 (hoy): las contrasenas son propias de esta aplicacion y las valida
    ///   ASP.NET Identity.
    /// - V1 (cuando llegue la API Key): las valida el Active Directory de la empresa
    ///   a traves del APICommon, y aqui las contrasenas dejan de existir.
    ///
    /// Se elige con la clave de configuracion <c>Autenticacion:Modo</c>.
    ///
    /// Comprobar la contrasena es lo unico que hace: si la cuenta esta aprobada, si
    /// tiene rol, etc. son decisiones de la aplicacion que se resuelven aparte y que
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

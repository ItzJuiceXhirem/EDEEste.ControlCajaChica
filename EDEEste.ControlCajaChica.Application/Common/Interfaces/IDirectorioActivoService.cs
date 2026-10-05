using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.DTOs;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    /// <summary>
    /// Consulta el Active Directory de la empresa a través del APICommon.
    ///
    /// Es solo de lectura: sirve para traer la ficha de una persona (nombre real,
    /// departamento, correo, cédula) y no tiene nada que ver con validar
    /// contraseñas, que es responsabilidad de <see cref="IAutenticadorCredenciales"/>.
    /// Están separados a propósito porque son dos operaciones distintas del API y
    /// pueden usarse por separado: consultar la ficha es útil incluso en modo Local,
    /// por ejemplo para prellenar el nombre de quien solicita acceso.
    /// </summary>
    public interface IDirectorioActivoService
    {
        // Devuelve null si el usuario no existe en el directorio.
        Task<UsuarioDirectorioDto?> ObtenerUsuarioAsync(
            string userName,
            CancellationToken cancellationToken = default);
    }
}

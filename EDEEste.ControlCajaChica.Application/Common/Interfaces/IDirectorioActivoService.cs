using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.DTOs;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    /// <summary>
    /// Consulta el Active Directory de la empresa a traves del APICommon.
    ///
    /// Es solo de lectura: sirve para traer la ficha de una persona (nombre real,
    /// departamento, correo, cedula) y no tiene nada que ver con validar
    /// contrasenas, que es responsabilidad de <see cref="IAutenticadorCredenciales"/>.
    /// Estan separados a proposito porque son dos operaciones distintas del API y
    /// pueden usarse por separado: consultar la ficha es util incluso en modo Local,
    /// por ejemplo para prellenar el nombre de quien solicita acceso.
    /// </summary>
    public interface IDirectorioActivoService
    {
        /// <summary>Devuelve null si el usuario no existe en el directorio.</summary>
        Task<UsuarioDirectorioDto?> ObtenerUsuarioAsync(
            string userName,
            CancellationToken cancellationToken = default);
    }
}

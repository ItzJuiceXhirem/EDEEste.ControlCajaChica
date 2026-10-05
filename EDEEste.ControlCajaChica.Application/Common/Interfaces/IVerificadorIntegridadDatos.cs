using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.DTOs;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    /// <summary>
    /// Lo que la revisión de integridad necesita leer de las tablas de negocio. Se
    /// implementa en Infrastructure porque recorre varias tablas a la vez, ignora el
    /// filtro de borrado lógico (un registro eliminado también tiene que estar
    /// íntegro) y toma los nombres de tabla de los metadatos de EF. Las reglas de qué
    /// cuenta como problema viven en los handlers de Features/Auditoria.
    /// </summary>
    public interface IVerificadorIntegridadDatos
    {
        // Los registros de las entidades firmadas cuya firma no coincide con sus datos
        Task<IReadOnlyList<RegistroConFirmaInvalidaDto>> ListarFirmasInvalidasAsync(
            CancellationToken cancellationToken = default);

      /* Las claves que existen hoy en cada tabla con borrado lógico, por nombre de
         tabla. Solo esas tablas: en ellas un registro nunca se borra de verdad, así
         que si la bitácora lo menciona y ya no está, alguien lo borró por fuera de
         la aplicación. En otras (por ejemplo AspNetUserRoles) borrar es legítimo. */
        Task<IReadOnlyDictionary<string, IReadOnlySet<Guid>>> ObtenerIdsExistentesAsync(
            CancellationToken cancellationToken = default);
    }
}

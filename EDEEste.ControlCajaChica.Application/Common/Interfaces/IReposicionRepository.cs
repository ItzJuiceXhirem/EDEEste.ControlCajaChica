using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    public interface IReposicionRepository
    {
        // Trae la solicitud con sus gastos y los comprobantes de cada uno
        Task<SolicitudReposicion?> ObtenerConDetalleAsync(Guid id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SolicitudReposicion>> ListarPorFondoAsync(Guid fondoId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SolicitudReposicion>> ListarAsync(CancellationToken cancellationToken = default);

      /* Solicitudes en un estado dado, de todos los fondos. Es la bandeja del
         Gerente (pendientes de aprobación) y la de Finanzas (aprobadas por pagar):
         ninguno de los dos trabaja por fondo, trabajan por cola. */
        Task<IReadOnlyList<SolicitudReposicion>> ListarPorEstadoAsync(EstadoReposicion estado, CancellationToken cancellationToken = default);

        Task AgregarAsync(SolicitudReposicion solicitud, CancellationToken cancellationToken = default);
    }
}

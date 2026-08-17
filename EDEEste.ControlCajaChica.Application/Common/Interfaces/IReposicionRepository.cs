using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Domain.Entities;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    public interface IReposicionRepository
    {
        /// <summary>Trae la solicitud con sus gastos y los comprobantes de cada uno.</summary>
        Task<SolicitudReposicion?> ObtenerConDetalleAsync(Guid id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SolicitudReposicion>> ListarPorFondoAsync(Guid fondoId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SolicitudReposicion>> ListarAsync(CancellationToken cancellationToken = default);

        Task AgregarAsync(SolicitudReposicion solicitud, CancellationToken cancellationToken = default);
    }
}

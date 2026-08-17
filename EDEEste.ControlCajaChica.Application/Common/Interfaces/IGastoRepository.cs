using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Domain.Entities;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    public interface IGastoRepository
    {
        Task<Gasto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Gasto>> ListarPorFondoAsync(Guid fondoId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gastos que todavia no entraron en ninguna reposicion. Trae los comprobantes
        /// incluidos porque son justo lo que necesita el PDF consolidado.
        /// </summary>
        Task<IReadOnlyList<Gasto>> ListarPendientesDeReposicionAsync(Guid fondoId, CancellationToken cancellationToken = default);

        Task AgregarAsync(Gasto gasto, CancellationToken cancellationToken = default);
    }
}

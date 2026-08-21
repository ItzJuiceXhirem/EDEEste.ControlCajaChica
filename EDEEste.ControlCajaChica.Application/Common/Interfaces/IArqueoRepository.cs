using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Domain.Entities;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    public interface IArqueoRepository
    {
        Task<ArqueoCaja?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ArqueoCaja>> ListarPorFondoAsync(Guid fondoId, CancellationToken cancellationToken = default);

        /// <summary>
        /// El arqueo mas reciente del fondo, si existe. Alimenta el aviso -- no
        /// bloqueo -- de que ya se conto este fondo en el mes en curso.
        /// </summary>
        Task<ArqueoCaja?> ObtenerUltimoDelFondoAsync(Guid fondoId, CancellationToken cancellationToken = default);

        Task AgregarAsync(ArqueoCaja arqueo, CancellationToken cancellationToken = default);
    }
}

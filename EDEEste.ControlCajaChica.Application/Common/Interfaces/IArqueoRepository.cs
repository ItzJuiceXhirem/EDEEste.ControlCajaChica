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

      /* El arqueo más reciente del fondo, si existe. Alimenta el aviso -- no
         bloqueo -- de que ya se contó este fondo en el mes en curso. */
        Task<ArqueoCaja?> ObtenerUltimoDelFondoAsync(Guid fondoId, CancellationToken cancellationToken = default);

        Task AgregarAsync(ArqueoCaja arqueo, CancellationToken cancellationToken = default);
    }
}

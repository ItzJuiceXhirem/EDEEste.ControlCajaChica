using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Domain.Entities;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    /* Los repositorios solo consultan y marcan entidades para persistir; NO guardan.
       El commit lo hace el caso de uso con IApplicationDbContext.SaveChangesAsync,
       para que todo lo que toca una operacion (gasto + comprobantes + balance del
       fondo + bitacora de auditoria) entre en una sola transaccion. */
    public interface IFondoRepository
    {
        Task<FondoCajaChica?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<FondoCajaChica>> ListarAsync(CancellationToken cancellationToken = default);

        Task AgregarAsync(FondoCajaChica fondo, CancellationToken cancellationToken = default);
    }
}

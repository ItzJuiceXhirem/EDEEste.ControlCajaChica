using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Domain.Entities;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    /* Los repositorios solo consultan y marcan entidades para persistir; NO guardan.
       El commit lo hace el caso de uso con IApplicationDbContext.SaveChangesAsync,
       para que todo lo que toca una operación (gasto + comprobantes + balance del
       fondo + bitácora de auditoría) entre en una sola transacción. */
    public interface IFondoRepository
    {
        Task<FondoCajaChica?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<FondoCajaChica>> ListarAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Si ese custodio ya tiene un fondo asignado. La relación es uno a uno: un
        /// custodio responde por una sola caja, y una caja tiene un solo responsable.
        /// Con dos fondos a nombre de la misma persona, un arqueo dejaría de poder
        /// decir de cual caja es el efectivo que se contó.
        ///
        /// <paramref name="excluirFondoId"/> deja fuera un fondo de la comprobacion:
        /// al editar, el propio fondo no cuenta como conflicto consigo mismo.
        /// </summary>
        Task<bool> ExisteFondoParaCustodioAsync(
            string custodioId,
            Guid? excluirFondoId = null,
            CancellationToken cancellationToken = default);

        Task AgregarAsync(FondoCajaChica fondo, CancellationToken cancellationToken = default);
    }
}

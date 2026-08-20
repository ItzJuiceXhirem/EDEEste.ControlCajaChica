using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;

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

        /// <summary>
        /// Gastos del fondo en un estado dado. Alimenta la bandeja del Gerente
        /// (anulaciones por confirmar).
        /// </summary>
        Task<IReadOnlyList<Gasto>> ListarPorEstadoAsync(Guid fondoId, EstadoGasto estado, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gastos anulados del fondo. Con <paramref name="desde"/> en null trae el
        /// historial completo: los anulados no se purgan nunca, solo se acotan en
        /// pantalla.
        /// </summary>
        Task<IReadOnlyList<Gasto>> ListarAnuladosAsync(Guid fondoId, DateTime? desde, CancellationToken cancellationToken = default);

        Task AgregarAsync(Gasto gasto, CancellationToken cancellationToken = default);
    }
}

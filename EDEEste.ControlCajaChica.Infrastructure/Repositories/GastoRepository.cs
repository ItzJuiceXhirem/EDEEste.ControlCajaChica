using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
using EDEEste.ControlCajaChica.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EDEEste.ControlCajaChica.Infrastructure.Repositories
{
    public sealed class GastoRepository : IGastoRepository
    {
        private readonly ApplicationDbContext _context;

        public GastoRepository(ApplicationDbContext context) => _context = context;

        public Task<Gasto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            _context.Gastos
                .Include(g => g.Comprobantes)
                .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        public async Task<IReadOnlyList<Gasto>> ListarPorFondoAsync(Guid fondoId, CancellationToken cancellationToken = default) =>
            await _context.Gastos
                .Include(g => g.CategoriaGasto)
                .Include(g => g.Comprobantes)
                .Where(g => g.FondoCajaChicaId == fondoId)
                .OrderByDescending(g => g.FechaGasto)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<Gasto>> ListarPendientesDeReposicionAsync(Guid fondoId, CancellationToken cancellationToken = default) =>
            await _context.Gastos
                .Include(g => g.Comprobantes)
                // Se piden las dos condiciones (sin reposicion asignada Y en estado
                // pendiente) en vez de confiar en una sola: son dos formas distintas de
                // decir lo mismo y si alguna vez se desincronizan, preferimos dejar el
                // gasto fuera del expediente antes que cobrarlo dos veces.
                .Where(g => g.FondoCajaChicaId == fondoId
                            && g.ReposicionId == null
                            && g.Estado == EstadoGasto.PendienteReposicion)
                .OrderBy(g => g.FechaGasto)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<Gasto>> ListarPorEstadoAsync(Guid fondoId, EstadoGasto estado, CancellationToken cancellationToken = default) =>
            await _context.Gastos
                .Include(g => g.CategoriaGasto)
                .Where(g => g.FondoCajaChicaId == fondoId && g.Estado == estado)
                .OrderBy(g => g.FechaGasto)
                .ToListAsync(cancellationToken);

        // Se filtra por FechaModificacion porque es cuando el interceptor sella la
        // anulacion. No hay columna FechaAnulacion a proposito: seria una segunda
        // columna nueva y nada vuelve a tocar un gasto ya anulado, asi que la fecha de
        // modificacion ES la de la anulacion. FechaModificacion no entra en la firma
        // HMAC (esta en AuditableEntity), asi que filtrar por ella no tiene efecto
        // sobre el sello.
        public async Task<IReadOnlyList<Gasto>> ListarAnuladosAsync(Guid fondoId, DateTime? desde, CancellationToken cancellationToken = default) =>
            await _context.Gastos
                .Include(g => g.CategoriaGasto)
                .Where(g => g.FondoCajaChicaId == fondoId
                            && g.Estado == EstadoGasto.Anulado
                            && (desde == null || g.FechaModificacion >= desde))
                .OrderByDescending(g => g.FechaModificacion)
                .ToListAsync(cancellationToken);

        public async Task AgregarAsync(Gasto gasto, CancellationToken cancellationToken = default) =>
            await _context.Gastos.AddAsync(gasto, cancellationToken);
    }
}

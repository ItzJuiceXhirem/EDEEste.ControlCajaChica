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

        public async Task AgregarAsync(Gasto gasto, CancellationToken cancellationToken = default) =>
            await _context.Gastos.AddAsync(gasto, cancellationToken);
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EDEEste.ControlCajaChica.Infrastructure.Repositories
{
    public sealed class ArqueoRepository : IArqueoRepository
    {
        private readonly ApplicationDbContext _context;

        public ArqueoRepository(ApplicationDbContext context) => _context = context;

        public Task<ArqueoCaja?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            _context.Arqueos
                .Include(a => a.DetallesDenominacion)
                .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        public async Task<IReadOnlyList<ArqueoCaja>> ListarPorFondoAsync(Guid fondoId, CancellationToken cancellationToken = default) =>
            await _context.Arqueos
                .Include(a => a.DetallesDenominacion)
                .Where(a => a.FondoCajaChicaId == fondoId)
                .OrderByDescending(a => a.FechaArqueo)
                .ToListAsync(cancellationToken);

        public Task<ArqueoCaja?> ObtenerUltimoDelFondoAsync(Guid fondoId, CancellationToken cancellationToken = default) =>
            _context.Arqueos
                .Where(a => a.FondoCajaChicaId == fondoId)
                .OrderByDescending(a => a.FechaArqueo)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task AgregarAsync(ArqueoCaja arqueo, CancellationToken cancellationToken = default) =>
            await _context.Arqueos.AddAsync(arqueo, cancellationToken);
    }
}

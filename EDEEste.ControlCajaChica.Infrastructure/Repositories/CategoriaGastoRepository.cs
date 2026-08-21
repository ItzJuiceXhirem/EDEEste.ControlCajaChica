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
    public sealed class CategoriaGastoRepository : ICategoriaGastoRepository
    {
        private readonly ApplicationDbContext _context;

        public CategoriaGastoRepository(ApplicationDbContext context) => _context = context;

        public Task<CategoriaGasto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            _context.CategoriasGasto.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        public async Task<IReadOnlyList<CategoriaGasto>> ListarAsync(CancellationToken cancellationToken = default) =>
            await _context.CategoriasGasto
                .OrderBy(c => c.Nombre)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<CategoriaGasto>> ListarActivasAsync(CancellationToken cancellationToken = default) =>
            await _context.CategoriasGasto
                .Where(c => c.Activo)
                .OrderBy(c => c.Nombre)
                .ToListAsync(cancellationToken);

        public async Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId = null, CancellationToken cancellationToken = default) =>
            await _context.CategoriasGasto
                .AnyAsync(c => c.Nombre == nombre && (excluirId == null || c.Id != excluirId), cancellationToken);

        public async Task AgregarAsync(CategoriaGasto categoria, CancellationToken cancellationToken = default) =>
            await _context.CategoriasGasto.AddAsync(categoria, cancellationToken);
    }
}

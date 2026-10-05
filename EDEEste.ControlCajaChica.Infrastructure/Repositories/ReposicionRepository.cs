using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
using EDEEste.ControlCajaChica.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EDEEste.ControlCajaChica.Infrastructure.Repositories
{
    public sealed class ReposicionRepository : IReposicionRepository
    {
        private readonly ApplicationDbContext _context;

        public ReposicionRepository(ApplicationDbContext context) => _context = context;

        public Task<SolicitudReposicion?> ObtenerConDetalleAsync(Guid id, CancellationToken cancellationToken = default) =>
            _context.Reposiciones
                .Include(r => r.FondoCajaChica)
                .Include(r => r.Gastos)
                    .ThenInclude(g => g.Comprobantes)
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        public async Task<IReadOnlyList<SolicitudReposicion>> ListarPorFondoAsync(Guid fondoId, CancellationToken cancellationToken = default) =>
            await _context.Reposiciones
                .Include(r => r.Gastos)
                .Where(r => r.FondoCajaChicaId == fondoId)
                .OrderByDescending(r => r.FechaSolicitud)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<SolicitudReposicion>> ListarAsync(CancellationToken cancellationToken = default) =>
            await _context.Reposiciones
                .Include(r => r.Gastos)
                .OrderByDescending(r => r.FechaSolicitud)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<SolicitudReposicion>> ListarPorEstadoAsync(EstadoReposicion estado, CancellationToken cancellationToken = default) =>
            await _context.Reposiciones
                .Include(r => r.FondoCajaChica)
                .Include(r => r.Gastos)
                .Where(r => r.Estado == estado)
                .OrderBy(r => r.FechaSolicitud)
                .ToListAsync(cancellationToken);

        /* Arreglo local y no la lista de ReglasEstado directamente: Contains sobre un
           arreglo es lo que EF Core traduce a SQL sin ambiguedad. */
        private static readonly EstadoReposicion[] EstadosEnCurso = ReglasEstado.ReposicionEnCurso.ToArray();

        public Task<bool> ExisteSolicitudEnCursoAsync(Guid fondoId, CancellationToken cancellationToken = default) =>
            _context.Reposiciones.AnyAsync(
                r => r.FondoCajaChicaId == fondoId && EstadosEnCurso.Contains(r.Estado),
                cancellationToken);

        public async Task<IReadOnlyList<SolicitudReposicion>> ListarEnCursoPorFondoAsync(Guid fondoId, CancellationToken cancellationToken = default) =>
            await _context.Reposiciones
                .Include(r => r.Gastos)
                .Where(r => r.FondoCajaChicaId == fondoId && EstadosEnCurso.Contains(r.Estado))
                .ToListAsync(cancellationToken);

        public async Task AgregarAsync(SolicitudReposicion solicitud, CancellationToken cancellationToken = default) =>
            await _context.Reposiciones.AddAsync(solicitud, cancellationToken);
    }
}

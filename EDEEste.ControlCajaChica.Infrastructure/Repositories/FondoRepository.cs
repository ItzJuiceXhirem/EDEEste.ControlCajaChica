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
    public sealed class FondoRepository : IFondoRepository
    {
        private readonly ApplicationDbContext _context;

        public FondoRepository(ApplicationDbContext context) => _context = context;

        public Task<FondoCajaChica?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            _context.Fondos.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        public async Task<IReadOnlyList<FondoCajaChica>> ListarAsync(CancellationToken cancellationToken = default) =>
            await _context.Fondos
                .OrderBy(f => f.FechaCreacion)
                .ToListAsync(cancellationToken);

        // Un fondo Inactivo no cuenta para la regla "un custodio, un fondo": si se
        // desactiva el fondo de un custodio, debe poder asignarsele uno nuevo sin
        // toparse con el que ya quedo fuera de servicio. EnReposicion/BloqueadaPorArqueo
        // si cuentan -- son estados de trabajo, el fondo sigue siendo el suyo.
        public Task<bool> ExisteFondoParaCustodioAsync(
            string custodioId,
            Guid? excluirFondoId = null,
            CancellationToken cancellationToken = default) =>
            _context.Fondos.AnyAsync(
                f => f.CustodioId == custodioId
                     && f.Estado != EstadoFondo.Inactivo
                     && (excluirFondoId == null || f.Id != excluirFondoId),
                cancellationToken);

        public async Task AgregarAsync(FondoCajaChica fondo, CancellationToken cancellationToken = default) =>
            await _context.Fondos.AddAsync(fondo, cancellationToken);
    }
}

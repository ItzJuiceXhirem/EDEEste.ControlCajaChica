using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Entities;

namespace EDEEste.ControlCajaChica.Application.Tests.TestDoubles
{
    public sealed class FakeArqueoRepository : IArqueoRepository
    {
        private readonly Dictionary<Guid, ArqueoCaja> _arqueos = new();

        public void Agregar(ArqueoCaja arqueo) => _arqueos[arqueo.Id] = arqueo;

        public Task<ArqueoCaja?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_arqueos.GetValueOrDefault(id));

        public Task<IReadOnlyList<ArqueoCaja>> ListarPorFondoAsync(Guid fondoId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ArqueoCaja>>(
                _arqueos.Values.Where(a => a.FondoCajaChicaId == fondoId).OrderByDescending(a => a.FechaArqueo).ToList());

        public Task<ArqueoCaja?> ObtenerUltimoDelFondoAsync(Guid fondoId, CancellationToken cancellationToken = default) =>
            Task.FromResult(
                _arqueos.Values
                    .Where(a => a.FondoCajaChicaId == fondoId)
                    .OrderByDescending(a => a.FechaArqueo)
                    .FirstOrDefault());

        public Task AgregarAsync(ArqueoCaja arqueo, CancellationToken cancellationToken = default)
        {
            Agregar(arqueo);
            return Task.CompletedTask;
        }
    }
}

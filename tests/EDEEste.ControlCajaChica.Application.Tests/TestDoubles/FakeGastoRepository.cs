using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;

namespace EDEEste.ControlCajaChica.Application.Tests.TestDoubles
{
    public sealed class FakeGastoRepository : IGastoRepository
    {
        private readonly Dictionary<Guid, Gasto> _gastos = new();

        public void Agregar(Gasto gasto) => _gastos[gasto.Id] = gasto;

        public Task<Gasto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_gastos.GetValueOrDefault(id));

        public Task<IReadOnlyList<Gasto>> ListarPorFondoAsync(Guid fondoId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Gasto>>(_gastos.Values.Where(g => g.FondoCajaChicaId == fondoId).ToList());

        public Task<IReadOnlyList<Gasto>> ListarPendientesDeReposicionAsync(Guid fondoId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Gasto>>(
                _gastos.Values
                    .Where(g => g.FondoCajaChicaId == fondoId
                                && g.ReposicionId == null
                                && g.Estado == EstadoGasto.PendienteReposicion)
                    .ToList());

        public Task<IReadOnlyList<Gasto>> ListarPorEstadoAsync(Guid fondoId, EstadoGasto estado, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Gasto>>(
                _gastos.Values.Where(g => g.FondoCajaChicaId == fondoId && g.Estado == estado).ToList());

        public Task<IReadOnlyList<Gasto>> ListarAnuladosAsync(Guid fondoId, DateTime? desde, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Gasto>>(
                _gastos.Values
                    .Where(g => g.FondoCajaChicaId == fondoId
                                && g.Estado == EstadoGasto.Anulado
                                && (desde == null || g.FechaModificacion >= desde))
                    .ToList());

        public Task<IReadOnlyList<Gasto>> ListarNoRepuestosAsync(Guid fondoId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Gasto>>(
                _gastos.Values
                    .Where(g => g.FondoCajaChicaId == fondoId
                                && (g.Estado == EstadoGasto.PendienteReposicion
                                    || g.Estado == EstadoGasto.EnProcesoReposicion
                                    || g.Estado == EstadoGasto.AnulacionPendiente))
                    .ToList());

        // Los comprobantes viven colgados de su gasto, asi que el doble los busca
        // recorriendolos en vez de mantener una segunda coleccion.
        public Task<ComprobanteAdjunto?> ObtenerComprobanteAsync(Guid comprobanteId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_gastos.Values
                .SelectMany(g => g.Comprobantes)
                .FirstOrDefault(c => c.Id == comprobanteId));

        public Task AgregarAsync(Gasto gasto, CancellationToken cancellationToken = default)
        {
            Agregar(gasto);
            return Task.CompletedTask;
        }

        public Task<int> ContarPorCategoriaYAnioAsync(Guid categoriaGastoId, int anio, CancellationToken cancellationToken = default) =>
            Task.FromResult(_gastos.Values.Count(g => g.CategoriaGastoId == categoriaGastoId && g.FechaGasto.Year == anio));
    }
}

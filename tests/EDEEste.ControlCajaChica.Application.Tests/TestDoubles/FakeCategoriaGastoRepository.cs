using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Entities;

namespace EDEEste.ControlCajaChica.Application.Tests.TestDoubles
{
    public sealed class FakeCategoriaGastoRepository : ICategoriaGastoRepository
    {
        private readonly Dictionary<Guid, CategoriaGasto> _categorias = new();

        public void Agregar(CategoriaGasto categoria) => _categorias[categoria.Id] = categoria;

        public Task<CategoriaGasto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_categorias.GetValueOrDefault(id));

        public Task<IReadOnlyList<CategoriaGasto>> ListarAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CategoriaGasto>>(_categorias.Values.ToList());

        public Task<IReadOnlyList<CategoriaGasto>> ListarActivasAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CategoriaGasto>>(_categorias.Values.Where(c => c.Activo).ToList());

        public Task AgregarAsync(CategoriaGasto categoria, CancellationToken cancellationToken = default)
        {
            Agregar(categoria);
            return Task.CompletedTask;
        }
    }
}

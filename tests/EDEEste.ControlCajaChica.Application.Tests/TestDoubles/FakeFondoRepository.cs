using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Entities;

namespace EDEEste.ControlCajaChica.Application.Tests.TestDoubles
{
    public sealed class FakeFondoRepository : IFondoRepository
    {
        private readonly Dictionary<Guid, FondoCajaChica> _fondos = new();

        public void Agregar(FondoCajaChica fondo) => _fondos[fondo.Id] = fondo;

        public Task<FondoCajaChica?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_fondos.GetValueOrDefault(id));

        public Task<IReadOnlyList<FondoCajaChica>> ListarAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FondoCajaChica>>(_fondos.Values.ToList());

        public Task<bool> ExisteFondoParaCustodioAsync(
            string custodioId,
            Guid? excluirFondoId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_fondos.Values.Any(
                f => f.CustodioId == custodioId && (excluirFondoId == null || f.Id != excluirFondoId)));

        public Task AgregarAsync(FondoCajaChica fondo, CancellationToken cancellationToken = default)
        {
            Agregar(fondo);
            return Task.CompletedTask;
        }
    }
}

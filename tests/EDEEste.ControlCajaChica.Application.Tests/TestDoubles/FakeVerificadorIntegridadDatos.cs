using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.DTOs;

namespace EDEEste.ControlCajaChica.Application.Tests.TestDoubles
{
    // Devuelve lo que la prueba cargue: qué registros tienen la firma rota y qué claves existen por tabla.
    public sealed class FakeVerificadorIntegridadDatos : IVerificadorIntegridadDatos
    {
        public List<RegistroConFirmaInvalidaDto> FirmasInvalidas { get; } = new();

        public Dictionary<string, HashSet<Guid>> IdsExistentes { get; } = new(StringComparer.Ordinal);

        public Task<IReadOnlyList<RegistroConFirmaInvalidaDto>> ListarFirmasInvalidasAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RegistroConFirmaInvalidaDto>>(FirmasInvalidas.ToList());

        public Task<IReadOnlyDictionary<string, IReadOnlySet<Guid>>> ObtenerIdsExistentesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, IReadOnlySet<Guid>>>(
                IdsExistentes.ToDictionary(par => par.Key, par => (IReadOnlySet<Guid>)par.Value, StringComparer.Ordinal));
    }
}

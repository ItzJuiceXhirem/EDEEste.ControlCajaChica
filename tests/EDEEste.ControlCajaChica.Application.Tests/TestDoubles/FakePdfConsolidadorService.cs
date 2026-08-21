using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Entities;

namespace EDEEste.ControlCajaChica.Application.Tests.TestDoubles
{
    public sealed class FakePdfConsolidadorService : IPdfConsolidadorService
    {
        public Task<byte[]> ConsolidarComprobantesAsync(SolicitudReposicion solicitud, CancellationToken cancellationToken = default) =>
            Task.FromResult<byte[]>([1, 2, 3]);
    }
}

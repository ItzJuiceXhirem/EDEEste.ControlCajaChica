using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Features.Gastos;
using EDEEste.ControlCajaChica.Application.Tests.TestDoubles;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
using Xunit;

namespace EDEEste.ControlCajaChica.Application.Tests.Features.Gastos
{
    public class RevertirAnulacionGastoHandlerTests
    {
        [Fact]
        public async Task Revertir_DesdeAnulacionPendiente_VuelveAPendienteYLimpiaElMotivo()
        {
            var gasto = new Gasto
            {
                MontoTotal = 500m,
                NCF = "B0100000001",
                Estado = EstadoGasto.AnulacionPendiente,
                MotivoAnulacion = "Factura duplicada"
            };

            var repo = new FakeGastoRepository();
            repo.Agregar(gasto);

            var contexto = new FakeApplicationDbContext();
            var handler = new RevertirAnulacionGastoHandler(repo, contexto);

            var resultado = await handler.EjecutarAsync(new RevertirAnulacionGastoCommand { GastoId = gasto.Id });

            Assert.True(resultado.Exitoso);
            Assert.Equal(EstadoGasto.PendienteReposicion, gasto.Estado);
            Assert.Null(gasto.MotivoAnulacion);
            Assert.Equal(1, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Revertir_DesdeEstadoQueNoEsAnulacionPendiente_Falla()
        {
            var gasto = new Gasto
            {
                MontoTotal = 500m,
                NCF = "B0100000001",
                Estado = EstadoGasto.PendienteReposicion
            };

            var repo = new FakeGastoRepository();
            repo.Agregar(gasto);

            var contexto = new FakeApplicationDbContext();
            var handler = new RevertirAnulacionGastoHandler(repo, contexto);

            var resultado = await handler.EjecutarAsync(new RevertirAnulacionGastoCommand { GastoId = gasto.Id });

            Assert.False(resultado.Exitoso);
            Assert.Equal(0, contexto.VecesGuardado);
        }
    }
}

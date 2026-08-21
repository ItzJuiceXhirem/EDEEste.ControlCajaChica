using System;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Features.Gastos;
using EDEEste.ControlCajaChica.Application.Tests.TestDoubles;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
using Xunit;

namespace EDEEste.ControlCajaChica.Application.Tests.Features.Gastos
{
    public class SolicitarAnulacionGastoHandlerTests
    {
        private static (Gasto gasto, FakeGastoRepository repo, FakeApplicationDbContext contexto, SolicitarAnulacionGastoHandler handler)
            CrearEscenario()
        {
            var gasto = new Gasto
            {
                MontoTotal = 500m,
                Subtotal = 500m,
                NCF = "B0100000001",
                Estado = EstadoGasto.PendienteReposicion
            };

            var repo = new FakeGastoRepository();
            repo.Agregar(gasto);

            var contexto = new FakeApplicationDbContext();
            var handler = new SolicitarAnulacionGastoHandler(repo, contexto);

            return (gasto, repo, contexto, handler);
        }

        [Fact]
        public async Task Solicitar_ConMotivo_DejaElGastoEnAnulacionPendienteYNoTocaNadaMas()
        {
            var (gasto, _, contexto, handler) = CrearEscenario();

            var resultado = await handler.EjecutarAsync(new SolicitarAnulacionGastoCommand
            {
                GastoId = gasto.Id,
                Motivo = "Factura duplicada"
            });

            Assert.True(resultado.Exitoso);
            Assert.Equal(EstadoGasto.AnulacionPendiente, gasto.Estado);
            Assert.Equal("Factura duplicada", gasto.MotivoAnulacion);
            Assert.Equal(1, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Solicitar_SinMotivo_Falla()
        {
            var (gasto, _, contexto, handler) = CrearEscenario();

            var resultado = await handler.EjecutarAsync(new SolicitarAnulacionGastoCommand
            {
                GastoId = gasto.Id,
                Motivo = "   "
            });

            Assert.False(resultado.Exitoso);
            Assert.Equal(EstadoGasto.PendienteReposicion, gasto.Estado);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Solicitar_ConGastoYaEnUnaReposicion_Falla()
        {
            var (gasto, _, contexto, handler) = CrearEscenario();
            gasto.ReposicionId = Guid.NewGuid();
            gasto.Estado = EstadoGasto.EnProcesoReposicion;

            var resultado = await handler.EjecutarAsync(new SolicitarAnulacionGastoCommand
            {
                GastoId = gasto.Id,
                Motivo = "Intento indebido"
            });

            Assert.False(resultado.Exitoso);
            Assert.Equal(0, contexto.VecesGuardado);
        }
    }
}

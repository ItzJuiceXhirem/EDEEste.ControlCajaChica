using System;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Features.Gastos;
using EDEEste.ControlCajaChica.Application.Tests.TestDoubles;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
using Xunit;

namespace EDEEste.ControlCajaChica.Application.Tests.Features.Gastos
{
    public class AnularGastoHandlerTests
    {
        private static (FondoCajaChica fondo, Gasto gasto, FakeGastoRepository gastos, FakeApplicationDbContext contexto, AnularGastoHandler handler)
            CrearEscenario(EstadoGasto estadoInicial, decimal balanceActual = 7500m, decimal montoFijo = 10000m, decimal montoGasto = 500m)
        {
            var fondo = new FondoCajaChica { MontoFijo = montoFijo, BalanceActual = balanceActual };

            var gasto = new Gasto
            {
                FondoCajaChicaId = fondo.Id,
                MontoTotal = montoGasto,
                Subtotal = montoGasto,
                NCF = "B0100000001",
                Estado = estadoInicial,
                MotivoAnulacion = estadoInicial == EstadoGasto.AnulacionPendiente ? "Motivo original del custodio" : null
            };

            var fondos = new FakeFondoRepository();
            fondos.Agregar(fondo);

            var gastos = new FakeGastoRepository();
            gastos.Agregar(gasto);

            var contexto = new FakeApplicationDbContext();
            var handler = new AnularGastoHandler(gastos, fondos, contexto);

            return (fondo, gasto, gastos, contexto, handler);
        }

        [Fact]
        public async Task AnularDirecto_DesdePendienteReposicion_SubeElBalanceExactoUnaVez()
        {
            var (fondo, gasto, _, contexto, handler) = CrearEscenario(EstadoGasto.PendienteReposicion, balanceActual: 7500m, montoGasto: 500m);
            var balanceOriginal = fondo.BalanceActual;

            var resultado = await handler.EjecutarAsync(new AnularGastoCommand
            {
                GastoId = gasto.Id,
                Motivo = "Gasto duplicado"
            });

            Assert.True(resultado.Exitoso);
            Assert.Equal(EstadoGasto.Anulado, gasto.Estado);
            Assert.Equal("Gasto duplicado", gasto.MotivoAnulacion);
            Assert.Equal(balanceOriginal + 500m, fondo.BalanceActual);
            Assert.Equal(1, contexto.VecesGuardado);
        }

        [Fact]
        public async Task AnularDirecto_SinMotivo_Falla()
        {
            var (fondo, gasto, _, contexto, handler) = CrearEscenario(EstadoGasto.PendienteReposicion);
            var balanceOriginal = fondo.BalanceActual;

            var resultado = await handler.EjecutarAsync(new AnularGastoCommand { GastoId = gasto.Id });

            Assert.False(resultado.Exitoso);
            Assert.Equal(balanceOriginal, fondo.BalanceActual);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task ConfirmarAnulacion_DesdeAnulacionPendienteSinMotivoNuevo_ConservaElMotivoOriginalYAbona()
        {
            var (fondo, gasto, _, contexto, handler) = CrearEscenario(EstadoGasto.AnulacionPendiente, balanceActual: 7500m, montoGasto: 500m);
            var balanceOriginal = fondo.BalanceActual;

            // Sin motivo: la confirmacion conserva el que dejo el Custodio.
            var resultado = await handler.EjecutarAsync(new AnularGastoCommand { GastoId = gasto.Id });

            Assert.True(resultado.Exitoso);
            Assert.Equal(EstadoGasto.Anulado, gasto.Estado);
            Assert.Equal("Motivo original del custodio", gasto.MotivoAnulacion);
            Assert.Equal(balanceOriginal + 500m, fondo.BalanceActual);
            Assert.Equal(1, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Anular_ConGastoEnUnaReposicion_Falla()
        {
            var (fondo, gasto, _, contexto, handler) = CrearEscenario(EstadoGasto.PendienteReposicion);
            gasto.ReposicionId = Guid.NewGuid();
            var balanceOriginal = fondo.BalanceActual;

            var resultado = await handler.EjecutarAsync(new AnularGastoCommand { GastoId = gasto.Id, Motivo = "Intento indebido" });

            Assert.False(resultado.Exitoso);
            Assert.Equal(balanceOriginal, fondo.BalanceActual);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Anular_GuardaDeTecho_DisparaSiElBalanceResultanteSuperaElFondoFijo()
        {
            var (fondo, gasto, _, contexto, handler) = CrearEscenario(
                EstadoGasto.PendienteReposicion, balanceActual: 9800m, montoFijo: 10000m, montoGasto: 500m);

            var resultado = await handler.EjecutarAsync(new AnularGastoCommand { GastoId = gasto.Id, Motivo = "Prueba de guarda" });

            Assert.False(resultado.Exitoso);
            Assert.Equal(9800m, fondo.BalanceActual);
            Assert.Equal(0, contexto.VecesGuardado);
        }
    }
}

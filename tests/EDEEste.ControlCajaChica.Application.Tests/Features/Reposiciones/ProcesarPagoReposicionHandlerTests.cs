using System;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Features.Reposiciones;
using EDEEste.ControlCajaChica.Application.Tests.TestDoubles;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
using Xunit;

namespace EDEEste.ControlCajaChica.Application.Tests.Features.Reposiciones
{
    public class ProcesarPagoReposicionHandlerTests
    {
        private static (FondoCajaChica fondo, SolicitudReposicion solicitud, Gasto gasto, FakeReposicionRepository repo, FakeApplicationDbContext contexto, ProcesarPagoReposicionHandler handler)
            CrearEscenario(decimal balanceActual = 7500m, decimal montoFijo = 10000m, decimal montoReclamado = 2500m)
        {
            var fondo = new FondoCajaChica { MontoFijo = montoFijo, BalanceActual = balanceActual };

            var gasto = new Gasto
            {
                FondoCajaChicaId = fondo.Id,
                MontoTotal = montoReclamado,
                Subtotal = montoReclamado,
                NCF = "B0100000001",
                Estado = EstadoGasto.EnProcesoReposicion
            };

            var solicitud = new SolicitudReposicion
            {
                FondoCajaChicaId = fondo.Id,
                FondoCajaChica = fondo,
                MontoReclamado = montoReclamado,
                Estado = EstadoReposicion.Aprobada
            };
            solicitud.Gastos.Add(gasto);
            gasto.ReposicionId = solicitud.Id;

            var repo = new FakeReposicionRepository();
            repo.Agregar(solicitud);

            var contexto = new FakeApplicationDbContext();
            var handler = new ProcesarPagoReposicionHandler(repo, new FakeCurrentUserService(), new FakeAutorizacionService(), contexto);

            return (fondo, solicitud, gasto, repo, contexto, handler);
        }

        [Fact]
        public async Task Pagar_SubeElBalanceExactoUnaVezYMarcaLosGastosRepuestos()
        {
            var (fondo, solicitud, gasto, _, contexto, handler) = CrearEscenario(balanceActual: 7500m, montoFijo: 10000m, montoReclamado: 2500m);

            var resultado = await handler.EjecutarAsync(new ProcesarPagoReposicionCommand
            {
                ReposicionId = solicitud.Id,
                ReferenciaPago = "TRF-0001"
            });

            Assert.True(resultado.Exitoso);
            Assert.Equal(10000m, fondo.BalanceActual);
            Assert.Equal(EstadoReposicion.Pagada, solicitud.Estado);
            Assert.Equal(EstadoGasto.Repuesto, gasto.Estado);
            Assert.Equal("TRF-0001", solicitud.ReferenciaPago);
            Assert.Equal(1, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Pagar_DesdeEstadoQueNoEsAprobada_Falla()
        {
            var (fondo, solicitud, _, _, contexto, handler) = CrearEscenario();
            solicitud.Estado = EstadoReposicion.PendienteAprobacion;
            var balanceOriginal = fondo.BalanceActual;

            var resultado = await handler.EjecutarAsync(new ProcesarPagoReposicionCommand
            {
                ReposicionId = solicitud.Id,
                ReferenciaPago = "TRF-0001"
            });

            Assert.False(resultado.Exitoso);
            Assert.Equal(balanceOriginal, fondo.BalanceActual);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Pagar_SinReferencia_Falla()
        {
            var (fondo, solicitud, _, _, contexto, handler) = CrearEscenario();
            var balanceOriginal = fondo.BalanceActual;

            var resultado = await handler.EjecutarAsync(new ProcesarPagoReposicionCommand
            {
                ReposicionId = solicitud.Id,
                ReferenciaPago = "   "
            });

            Assert.False(resultado.Exitoso);
            Assert.Equal(balanceOriginal, fondo.BalanceActual);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Pagar_GuardaDeTecho_DisparaSiElBalanceResultanteSuperaElFondoFijo()
        {
            // Escenario imposible en un flujo normal (el balance ya deberia haber
            // bajado al generar la solicitud), pero es justo la situacion que la
            // guarda de techo tiene que atrapar: algo se conto dos veces.
            var (fondo, solicitud, _, _, contexto, handler) = CrearEscenario(balanceActual: 9000m, montoFijo: 10000m, montoReclamado: 2500m);

            var resultado = await handler.EjecutarAsync(new ProcesarPagoReposicionCommand
            {
                ReposicionId = solicitud.Id,
                ReferenciaPago = "TRF-0002"
            });

            Assert.False(resultado.Exitoso);
            Assert.Equal(9000m, fondo.BalanceActual);
            Assert.Equal(0, contexto.VecesGuardado);
        }
    }
}

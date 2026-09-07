using System;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Features.Reposiciones;
using EDEEste.ControlCajaChica.Application.Tests.TestDoubles;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
using Xunit;

namespace EDEEste.ControlCajaChica.Application.Tests.Features.Reposiciones
{
    public class AprobarReposicionHandlerTests
    {
        private static (FondoCajaChica fondo, SolicitudReposicion solicitud, Gasto gasto, FakeReposicionRepository repo, FakeApplicationDbContext contexto, AprobarReposicionHandler handler)
            CrearEscenario(decimal balanceActual = 7500m, decimal montoFijo = 10000m, decimal montoGasto = 2500m)
        {
            var fondo = new FondoCajaChica { MontoFijo = montoFijo, BalanceActual = balanceActual };

            var gasto = new Gasto
            {
                FondoCajaChicaId = fondo.Id,
                MontoTotal = montoGasto,
                Subtotal = montoGasto,
                NCF = "B0100000001",
                Estado = EstadoGasto.EnProcesoReposicion
            };

            var solicitud = new SolicitudReposicion
            {
                FondoCajaChicaId = fondo.Id,
                FondoCajaChica = fondo,
                MontoReclamado = montoGasto,
                Estado = EstadoReposicion.PendienteAprobacion
            };
            solicitud.Gastos.Add(gasto);
            gasto.ReposicionId = solicitud.Id;

            var repo = new FakeReposicionRepository();
            repo.Agregar(solicitud);

            var contexto = new FakeApplicationDbContext();
            var handler = new AprobarReposicionHandler(repo, new FakeCurrentUserService(), new FakeAutorizacionService(), contexto);

            return (fondo, solicitud, gasto, repo, contexto, handler);
        }

        [Fact]
        public async Task Aprobar_NoTocaElBalanceYCambiaElEstado()
        {
            var (fondo, solicitud, gasto, _, contexto, handler) = CrearEscenario();
            var balanceOriginal = fondo.BalanceActual;

            var resultado = await handler.EjecutarAsync(new AprobarReposicionCommand { ReposicionId = solicitud.Id, Aprobar = true });

            Assert.True(resultado.Exitoso);
            Assert.Equal(EstadoReposicion.Aprobada, solicitud.Estado);
            Assert.Equal(balanceOriginal, fondo.BalanceActual);
            Assert.Equal(EstadoGasto.EnProcesoReposicion, gasto.Estado);
            Assert.Equal(1, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Rechazar_DevuelveGastosAPendienteYNoTocaElBalance()
        {
            var (fondo, solicitud, gasto, _, contexto, handler) = CrearEscenario();
            var balanceOriginal = fondo.BalanceActual;

            var resultado = await handler.EjecutarAsync(new AprobarReposicionCommand { ReposicionId = solicitud.Id, Aprobar = false });

            Assert.True(resultado.Exitoso);
            Assert.Equal(EstadoReposicion.Rechazada, solicitud.Estado);
            Assert.Equal(EstadoGasto.PendienteReposicion, gasto.Estado);
            Assert.Null(gasto.ReposicionId);
            Assert.Equal(balanceOriginal, fondo.BalanceActual);
            Assert.Equal(1, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Aprobar_DesdeEstadoQueNoEsPendienteAprobacion_Falla()
        {
            var (_, solicitud, _, _, contexto, handler) = CrearEscenario();
            solicitud.Estado = EstadoReposicion.Aprobada;

            var resultado = await handler.EjecutarAsync(new AprobarReposicionCommand { ReposicionId = solicitud.Id, Aprobar = true });

            Assert.False(resultado.Exitoso);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Aprobar_SolicitudInexistente_Falla()
        {
            var (_, _, _, repo, contexto, handler) = CrearEscenario();

            var resultado = await handler.EjecutarAsync(new AprobarReposicionCommand { ReposicionId = Guid.NewGuid(), Aprobar = true });

            Assert.False(resultado.Exitoso);
            Assert.Equal(0, contexto.VecesGuardado);
        }
    }
}

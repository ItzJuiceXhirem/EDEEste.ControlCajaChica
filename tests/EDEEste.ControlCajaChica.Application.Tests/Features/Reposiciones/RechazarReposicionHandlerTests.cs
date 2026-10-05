using System;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Features.Reposiciones;
using EDEEste.ControlCajaChica.Application.Tests.TestDoubles;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
using Xunit;

namespace EDEEste.ControlCajaChica.Application.Tests.Features.Reposiciones
{
    public class RechazarReposicionHandlerTests
    {
        private const string MotivoDeFinanzas = "Falta el comprobante del gasto B0100000001.";

        private static (FondoCajaChica fondo, SolicitudReposicion solicitud, Gasto gasto, FakeApplicationDbContext contexto, RechazarReposicionHandler handler)
            CrearEscenario(
                EstadoReposicion estadoSolicitud = EstadoReposicion.PendienteAprobacion,
                EstadoFondo estadoFondo = EstadoFondo.Activo,
                bool tienePermiso = true)
        {
            var fondo = new FondoCajaChica { MontoFijo = 10000m, BalanceActual = 7500m, Estado = estadoFondo };

            var gasto = new Gasto
            {
                FondoCajaChicaId = fondo.Id,
                MontoTotal = 2500m,
                Subtotal = 2500m,
                NCF = "B0100000001",
                Estado = EstadoGasto.EnProcesoReposicion
            };

            var solicitud = new SolicitudReposicion
            {
                FondoCajaChicaId = fondo.Id,
                FondoCajaChica = fondo,
                MontoReclamado = 2500m,
                Estado = estadoSolicitud,
                MotivoDevolucion = estadoSolicitud == EstadoReposicion.DevueltaPorFinanzas ? MotivoDeFinanzas : null
            };
            solicitud.Gastos.Add(gasto);
            gasto.ReposicionId = solicitud.Id;

            var repo = new FakeReposicionRepository();
            repo.Agregar(solicitud);

            var contexto = new FakeApplicationDbContext();
            var handler = new RechazarReposicionHandler(
                repo, new FakeCurrentUserService(), new FakeAutorizacionService(tienePermiso), contexto);

            return (fondo, solicitud, gasto, contexto, handler);
        }

        [Fact]
        public async Task Rechazar_Pendiente_SinMotivo_Falla()
        {
            var (_, solicitud, gasto, contexto, handler) = CrearEscenario();

            var resultado = await handler.EjecutarAsync(new RechazarReposicionCommand { ReposicionId = solicitud.Id, Motivo = "  " });

            Assert.False(resultado.Exitoso);
            Assert.Equal(EstadoReposicion.PendienteAprobacion, solicitud.Estado);
            Assert.Equal(EstadoGasto.EnProcesoReposicion, gasto.Estado);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Rechazar_Pendiente_ConMotivo_GuardaElMotivoDevuelveLosGastosYNoTocaElBalance()
        {
            var (fondo, solicitud, gasto, contexto, handler) = CrearEscenario();
            var balanceOriginal = fondo.BalanceActual;

            var resultado = await handler.EjecutarAsync(
                new RechazarReposicionCommand { ReposicionId = solicitud.Id, Motivo = "  El NCF no coincide con la factura.  " });

            Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores));
            Assert.Equal(EstadoReposicion.Rechazada, solicitud.Estado);
            Assert.Equal("El NCF no coincide con la factura.", solicitud.MotivoRechazo);
            Assert.Equal(EstadoGasto.PendienteReposicion, gasto.Estado);
            Assert.Null(gasto.ReposicionId);
            Assert.Equal(balanceOriginal, fondo.BalanceActual);
            Assert.Equal(EstadoFondo.Activo, fondo.Estado);
            Assert.Equal(1, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Rechazar_Pendiente_ConMotivoDemasiadoLargo_Falla()
        {
            var (_, solicitud, _, contexto, handler) = CrearEscenario();

            var resultado = await handler.EjecutarAsync(new RechazarReposicionCommand
            {
                ReposicionId = solicitud.Id,
                Motivo = new string('x', LimitesReposicion.LongitudMaximaMotivo + 1)
            });

            Assert.False(resultado.Exitoso);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Rechazar_Devuelta_UsaElMotivoDeFinanzasYLiberaElFondo()
        {
            var (fondo, solicitud, gasto, contexto, handler) = CrearEscenario(
                EstadoReposicion.DevueltaPorFinanzas, EstadoFondo.EnReposicion);

            // El motivo que llegue por el comando se ignora: el Gerente no inventa uno
            // distinto al que originó la devolución.
            var resultado = await handler.EjecutarAsync(
                new RechazarReposicionCommand { ReposicionId = solicitud.Id, Motivo = "otro motivo" });

            Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores));
            Assert.Equal(EstadoReposicion.Rechazada, solicitud.Estado);
            Assert.Equal(MotivoDeFinanzas, solicitud.MotivoRechazo);
            Assert.Equal(EstadoFondo.Activo, fondo.Estado);
            Assert.Equal(EstadoGasto.PendienteReposicion, gasto.Estado);
            Assert.Null(gasto.ReposicionId);
            Assert.Equal(1, contexto.VecesGuardado);
        }

        [Theory]
        [InlineData(EstadoReposicion.Aprobada)]
        [InlineData(EstadoReposicion.Pagada)]
        [InlineData(EstadoReposicion.Rechazada)]
        public async Task Rechazar_DesdeEstadoQueNoSePuedeRechazar_Falla(EstadoReposicion estado)
        {
            var (_, solicitud, _, contexto, handler) = CrearEscenario(estado);

            var resultado = await handler.EjecutarAsync(new RechazarReposicionCommand { ReposicionId = solicitud.Id, Motivo = "x" });

            Assert.False(resultado.Exitoso);
            Assert.Equal(estado, solicitud.Estado);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Rechazar_SinPermiso_Falla()
        {
            var (_, solicitud, _, contexto, handler) = CrearEscenario(tienePermiso: false);

            var resultado = await handler.EjecutarAsync(new RechazarReposicionCommand { ReposicionId = solicitud.Id, Motivo = "x" });

            Assert.False(resultado.Exitoso);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Rechazar_SolicitudInexistente_Falla()
        {
            var (_, _, _, contexto, handler) = CrearEscenario();

            var resultado = await handler.EjecutarAsync(new RechazarReposicionCommand { ReposicionId = Guid.NewGuid(), Motivo = "x" });

            Assert.False(resultado.Exitoso);
            Assert.Equal(0, contexto.VecesGuardado);
        }
    }
}

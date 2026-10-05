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
    public class AprobarReposicionHandlerTests
    {
        private static (FondoCajaChica fondo, SolicitudReposicion solicitud, Gasto gasto, FakeApplicationDbContext contexto, AprobarReposicionHandler handler)
            CrearEscenario(
                EstadoReposicion estadoSolicitud = EstadoReposicion.PendienteAprobacion,
                EstadoFondo estadoFondo = EstadoFondo.Activo,
                bool tienePermiso = true,
                decimal balanceActual = 7500m,
                decimal montoFijo = 10000m,
                decimal montoGasto = 2500m)
        {
            var fondo = new FondoCajaChica { MontoFijo = montoFijo, BalanceActual = balanceActual, Estado = estadoFondo };

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
                Estado = estadoSolicitud
            };
            solicitud.Gastos.Add(gasto);
            gasto.ReposicionId = solicitud.Id;

            var repo = new FakeReposicionRepository();
            repo.Agregar(solicitud);

            var contexto = new FakeApplicationDbContext();
            var handler = new AprobarReposicionHandler(
                repo, new FakeCurrentUserService(), new FakeAutorizacionService(tienePermiso), contexto);

            return (fondo, solicitud, gasto, contexto, handler);
        }

        [Fact]
        public async Task Aprobar_Pendiente_NoTocaElBalance_PasaElFondoAEnReposicion()
        {
            var (fondo, solicitud, gasto, contexto, handler) = CrearEscenario();
            var balanceOriginal = fondo.BalanceActual;

            var resultado = await handler.EjecutarAsync(new AprobarReposicionCommand { ReposicionId = solicitud.Id });

            Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores));
            Assert.Equal(EstadoReposicion.Aprobada, solicitud.Estado);
            Assert.Equal(EstadoFondo.EnReposicion, fondo.Estado);
            Assert.Equal(balanceOriginal, fondo.BalanceActual);
            Assert.Equal(EstadoGasto.EnProcesoReposicion, gasto.Estado);
            Assert.Equal(1, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Aprobar_Pendiente_NoGuardaMotivoAunqueLleguePorElComando()
        {
            // La primera aprobacion no lleva motivo: solo la nueva aprobacion de una devuelta.
            var (_, solicitud, _, _, handler) = CrearEscenario();

            var resultado = await handler.EjecutarAsync(
                new AprobarReposicionCommand { ReposicionId = solicitud.Id, Motivo = "irrelevante" });

            Assert.True(resultado.Exitoso);
            Assert.Null(solicitud.MotivoReaprobacion);
        }

        [Fact]
        public async Task Aprobar_Devuelta_SinMotivo_Falla()
        {
            var (fondo, solicitud, _, contexto, handler) = CrearEscenario(
                EstadoReposicion.DevueltaPorFinanzas, EstadoFondo.EnReposicion);

            var resultado = await handler.EjecutarAsync(new AprobarReposicionCommand { ReposicionId = solicitud.Id, Motivo = "   " });

            Assert.False(resultado.Exitoso);
            Assert.Equal(EstadoReposicion.DevueltaPorFinanzas, solicitud.Estado);
            Assert.Equal(EstadoFondo.EnReposicion, fondo.Estado);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Aprobar_Devuelta_ConMotivo_GuardaElMotivoYDejaElFondoEnReposicion()
        {
            var (fondo, solicitud, _, contexto, handler) = CrearEscenario(
                EstadoReposicion.DevueltaPorFinanzas, EstadoFondo.EnReposicion);

            var resultado = await handler.EjecutarAsync(
                new AprobarReposicionCommand { ReposicionId = solicitud.Id, Motivo = "  Se adjuntó el comprobante faltante.  " });

            Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores));
            Assert.Equal(EstadoReposicion.Aprobada, solicitud.Estado);
            Assert.Equal("Se adjuntó el comprobante faltante.", solicitud.MotivoReaprobacion);
            Assert.Equal(EstadoFondo.EnReposicion, fondo.Estado);
            Assert.Equal(1, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Aprobar_Devuelta_ConMotivoDemasiadoLargo_Falla()
        {
            var (_, solicitud, _, contexto, handler) = CrearEscenario(
                EstadoReposicion.DevueltaPorFinanzas, EstadoFondo.EnReposicion);

            var resultado = await handler.EjecutarAsync(new AprobarReposicionCommand
            {
                ReposicionId = solicitud.Id,
                Motivo = new string('x', LimitesReposicion.LongitudMaximaMotivo + 1)
            });

            Assert.False(resultado.Exitoso);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Theory]
        [InlineData(EstadoReposicion.Aprobada)]
        [InlineData(EstadoReposicion.Pagada)]
        [InlineData(EstadoReposicion.Rechazada)]
        public async Task Aprobar_DesdeEstadoQueNoSePuedeAprobar_Falla(EstadoReposicion estado)
        {
            var (_, solicitud, _, contexto, handler) = CrearEscenario(estado);

            var resultado = await handler.EjecutarAsync(new AprobarReposicionCommand { ReposicionId = solicitud.Id, Motivo = "x" });

            Assert.False(resultado.Exitoso);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Aprobar_ConElFondoConLaFirmaComprometida_Falla()
        {
            var (fondo, solicitud, _, contexto, handler) = CrearEscenario();
            fondo.IntegridadVerificada = false;

            var resultado = await handler.EjecutarAsync(new AprobarReposicionCommand { ReposicionId = solicitud.Id });

            Assert.False(resultado.Exitoso);
            Assert.Equal(EstadoReposicion.PendienteAprobacion, solicitud.Estado);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Aprobar_SinPermiso_Falla()
        {
            var (_, solicitud, _, contexto, handler) = CrearEscenario(tienePermiso: false);

            var resultado = await handler.EjecutarAsync(new AprobarReposicionCommand { ReposicionId = solicitud.Id });

            Assert.False(resultado.Exitoso);
            Assert.Equal(EstadoReposicion.PendienteAprobacion, solicitud.Estado);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Aprobar_SolicitudInexistente_Falla()
        {
            var (_, _, _, contexto, handler) = CrearEscenario();

            var resultado = await handler.EjecutarAsync(new AprobarReposicionCommand { ReposicionId = Guid.NewGuid() });

            Assert.False(resultado.Exitoso);
            Assert.Equal(0, contexto.VecesGuardado);
        }
    }
}

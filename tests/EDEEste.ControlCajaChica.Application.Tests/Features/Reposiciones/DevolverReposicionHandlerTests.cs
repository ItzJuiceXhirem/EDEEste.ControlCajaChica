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
    public class DevolverReposicionHandlerTests
    {
        private static (FondoCajaChica fondo, SolicitudReposicion solicitud, Gasto gasto, FakeApplicationDbContext contexto, DevolverReposicionHandler handler)
            CrearEscenario(EstadoReposicion estadoSolicitud = EstadoReposicion.Aprobada, bool tienePermiso = true)
        {
            // Una solicitud aprobada deja el fondo EnReposicion: es el estado en que Finanzas la recibe.
            var fondo = new FondoCajaChica { MontoFijo = 10000m, BalanceActual = 7500m, Estado = EstadoFondo.EnReposicion };

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
                Estado = estadoSolicitud
            };
            solicitud.Gastos.Add(gasto);
            gasto.ReposicionId = solicitud.Id;

            var repo = new FakeReposicionRepository();
            repo.Agregar(solicitud);

            var contexto = new FakeApplicationDbContext();
            var handler = new DevolverReposicionHandler(repo, new FakeAutorizacionService(tienePermiso), contexto);

            return (fondo, solicitud, gasto, contexto, handler);
        }

        [Fact]
        public async Task Devolver_Aprobada_PasaADevueltaYNoTocaElFondoNiLosGastos()
        {
            var (fondo, solicitud, gasto, contexto, handler) = CrearEscenario();
            var balanceOriginal = fondo.BalanceActual;

            var resultado = await handler.EjecutarAsync(
                new DevolverReposicionCommand { ReposicionId = solicitud.Id, Motivo = "  La referencia del gasto no es legible.  " });

            Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores));
            Assert.Equal(EstadoReposicion.DevueltaPorFinanzas, solicitud.Estado);
            Assert.Equal("La referencia del gasto no es legible.", solicitud.MotivoDevolucion);
            Assert.Equal(EstadoFondo.EnReposicion, fondo.Estado);
            Assert.Equal(balanceOriginal, fondo.BalanceActual);
            Assert.Equal(EstadoGasto.EnProcesoReposicion, gasto.Estado);
            Assert.Equal(solicitud.Id, gasto.ReposicionId);
            Assert.Equal(1, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Devolver_NoRegistraAFinanzasComoQuienPago()
        {
            // FinanzasUsuarioId significa "quien pago": devolver no lo escribe.
            var (_, solicitud, _, _, handler) = CrearEscenario();

            await handler.EjecutarAsync(new DevolverReposicionCommand { ReposicionId = solicitud.Id, Motivo = "x" });

            Assert.Null(solicitud.FinanzasUsuarioId);
            Assert.Null(solicitud.FechaPago);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Devolver_SinMotivo_Falla(string motivo)
        {
            var (_, solicitud, _, contexto, handler) = CrearEscenario();

            var resultado = await handler.EjecutarAsync(new DevolverReposicionCommand { ReposicionId = solicitud.Id, Motivo = motivo });

            Assert.False(resultado.Exitoso);
            Assert.Equal(EstadoReposicion.Aprobada, solicitud.Estado);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Devolver_ConMotivoDemasiadoLargo_Falla()
        {
            var (_, solicitud, _, contexto, handler) = CrearEscenario();

            var resultado = await handler.EjecutarAsync(new DevolverReposicionCommand
            {
                ReposicionId = solicitud.Id,
                Motivo = new string('x', LimitesReposicion.LongitudMaximaMotivo + 1)
            });

            Assert.False(resultado.Exitoso);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Theory]
        [InlineData(EstadoReposicion.PendienteAprobacion)]
        [InlineData(EstadoReposicion.DevueltaPorFinanzas)]
        [InlineData(EstadoReposicion.Pagada)]
        [InlineData(EstadoReposicion.Rechazada)]
        public async Task Devolver_DesdeEstadoQueNoEsAprobada_Falla(EstadoReposicion estado)
        {
            var (_, solicitud, _, contexto, handler) = CrearEscenario(estado);

            var resultado = await handler.EjecutarAsync(new DevolverReposicionCommand { ReposicionId = solicitud.Id, Motivo = "x" });

            Assert.False(resultado.Exitoso);
            Assert.Equal(estado, solicitud.Estado);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Devolver_SinPermiso_Falla()
        {
            var (_, solicitud, _, contexto, handler) = CrearEscenario(tienePermiso: false);

            var resultado = await handler.EjecutarAsync(new DevolverReposicionCommand { ReposicionId = solicitud.Id, Motivo = "x" });

            Assert.False(resultado.Exitoso);
            Assert.Equal(EstadoReposicion.Aprobada, solicitud.Estado);
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Devolver_ConConflictoDeConcurrencia_FallaYNoCuentaComoGuardado()
        {
            var (_, solicitud, _, contexto, handler) = CrearEscenario();
            contexto.FallarPorConcurrencia = true;

            var resultado = await handler.EjecutarAsync(new DevolverReposicionCommand { ReposicionId = solicitud.Id, Motivo = "x" });

            Assert.False(resultado.Exitoso);
            Assert.Contains(resultado.Errores, e => e.Contains("Otro usuario modificó"));
            Assert.Equal(0, contexto.VecesGuardado);
        }

        [Fact]
        public async Task Devolver_SolicitudInexistente_Falla()
        {
            var (_, _, _, contexto, handler) = CrearEscenario();

            var resultado = await handler.EjecutarAsync(new DevolverReposicionCommand { ReposicionId = Guid.NewGuid(), Motivo = "x" });

            Assert.False(resultado.Exitoso);
            Assert.Equal(0, contexto.VecesGuardado);
        }
    }
}

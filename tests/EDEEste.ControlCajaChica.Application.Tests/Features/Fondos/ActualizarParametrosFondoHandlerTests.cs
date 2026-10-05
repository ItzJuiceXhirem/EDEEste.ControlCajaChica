using System;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Application.Features.Fondos;
using EDEEste.ControlCajaChica.Application.Features.Reposiciones;
using EDEEste.ControlCajaChica.Application.Tests.TestDoubles;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
using Xunit;

namespace EDEEste.ControlCajaChica.Application.Tests.Features.Fondos
{
    /// <summary>
    /// Cubre lo que la reposicion agrega a este handler: el Administrador puede cerrar un
    /// fondo en cualquier momento (la solicitud en curso se rechaza sola), y el estado
    /// EnReposicion lo maneja el sistema, nunca se elige a mano.
    /// </summary>
    public class ActualizarParametrosFondoHandlerTests
    {
        private const string CustodioId = "custodio-1";

        private sealed class Escenario
        {
            public FondoCajaChica Fondo { get; }
            public FakeReposicionRepository Reposiciones { get; } = new();
            public FakeApplicationDbContext Contexto { get; } = new();
            private readonly ActualizarParametrosFondoHandler _handler;

            public Escenario(EstadoFondo estadoFondo = EstadoFondo.Activo)
            {
                Fondo = new FondoCajaChica
                {
                    MontoFijo = 10000m,
                    BalanceActual = 7500m,
                    CustodioId = CustodioId,
                    Estado = estadoFondo
                };

                var fondos = new FakeFondoRepository();
                fondos.Agregar(Fondo);

                _handler = new ActualizarParametrosFondoHandler(
                    fondos,
                    Reposiciones,
                    new FakeIdentityService(CustodioId, RolesApp.Custodio),
                    new FakeAutorizacionService(),
                    Contexto);
            }

            /// <summary>Una solicitud del fondo con un gasto en proceso, en el estado dado.</summary>
            public (SolicitudReposicion solicitud, Gasto gasto) AgregarSolicitud(EstadoReposicion estado)
            {
                var gasto = new Gasto
                {
                    FondoCajaChicaId = Fondo.Id,
                    MontoTotal = 2500m,
                    Subtotal = 2500m,
                    NCF = "B0100000001",
                    Estado = EstadoGasto.EnProcesoReposicion
                };

                var solicitud = new SolicitudReposicion
                {
                    FondoCajaChicaId = Fondo.Id,
                    FondoCajaChica = Fondo,
                    MontoReclamado = 2500m,
                    Estado = estado
                };
                solicitud.Gastos.Add(gasto);
                gasto.ReposicionId = solicitud.Id;

                Reposiciones.Agregar(solicitud);
                return (solicitud, gasto);
            }

            public Task<ResultadoOperacion<Guid>> CambiarEstadoAsync(EstadoFondo nuevoEstado) =>
                _handler.EjecutarAsync(new ActualizarParametrosFondoCommand
                {
                    FondoCajaChicaId = Fondo.Id,
                    LimitePorGasto = 0m,
                    PorcentajeMaximoPorGasto = LimitesFondo.TopePorGastoPorDefecto,
                    PorcentajeAlertaReposicion = LimitesFondo.AlertaReposicionPorDefecto,
                    CustodioId = CustodioId,
                    Estado = nuevoEstado
                });
        }

        [Theory]
        [InlineData(EstadoReposicion.PendienteAprobacion, EstadoFondo.Activo)]
        [InlineData(EstadoReposicion.Aprobada, EstadoFondo.EnReposicion)]
        [InlineData(EstadoReposicion.DevueltaPorFinanzas, EstadoFondo.EnReposicion)]
        public async Task Cerrar_ConSolicitudEnCurso_LaRechazaConElMotivoFijoYDevuelveLosGastos(
            EstadoReposicion estadoSolicitud, EstadoFondo estadoFondo)
        {
            var escenario = new Escenario(estadoFondo);
            var (solicitud, gasto) = escenario.AgregarSolicitud(estadoSolicitud);
            var balanceOriginal = escenario.Fondo.BalanceActual;

            var resultado = await escenario.CambiarEstadoAsync(EstadoFondo.Inactivo);

            Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores));
            Assert.Equal(EstadoFondo.Inactivo, escenario.Fondo.Estado);
            Assert.Equal(EstadoReposicion.Rechazada, solicitud.Estado);
            Assert.Equal(RechazoDeReposicion.MotivoCierreDeFondo, solicitud.MotivoRechazo);
            Assert.Equal(EstadoGasto.PendienteReposicion, gasto.Estado);
            Assert.Null(gasto.ReposicionId);
            // Rechazar no mueve dinero: el Administrador sigue sin tocar el balance.
            Assert.Equal(balanceOriginal, escenario.Fondo.BalanceActual);
            Assert.Equal(1, escenario.Contexto.VecesGuardado);
        }

        [Fact]
        public async Task Cerrar_SinSolicitudEnCurso_SoloCierraElFondoYNoTocaLasSolicitudesYaTerminadas()
        {
            var escenario = new Escenario();
            var (pagada, _) = escenario.AgregarSolicitud(EstadoReposicion.Pagada);
            var (rechazada, _) = escenario.AgregarSolicitud(EstadoReposicion.Rechazada);

            var resultado = await escenario.CambiarEstadoAsync(EstadoFondo.Inactivo);

            Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores));
            Assert.Equal(EstadoFondo.Inactivo, escenario.Fondo.Estado);
            Assert.Equal(EstadoReposicion.Pagada, pagada.Estado);
            Assert.Equal(EstadoReposicion.Rechazada, rechazada.Estado);
            Assert.Null(rechazada.MotivoRechazo);
        }

        [Fact]
        public async Task Cerrar_ConUnaSolicitudDeFirmaComprometida_Falla()
        {
            var escenario = new Escenario(EstadoFondo.EnReposicion);
            var (solicitud, _) = escenario.AgregarSolicitud(EstadoReposicion.Aprobada);
            solicitud.IntegridadVerificada = false;

            var resultado = await escenario.CambiarEstadoAsync(EstadoFondo.Inactivo);

            Assert.False(resultado.Exitoso);
            Assert.Equal(EstadoFondo.EnReposicion, escenario.Fondo.Estado);
            Assert.Equal(EstadoReposicion.Aprobada, solicitud.Estado);
            Assert.Equal(0, escenario.Contexto.VecesGuardado);
        }

        [Fact]
        public async Task FondoEnReposicion_NoSePuedePonerActivoAMano()
        {
            var escenario = new Escenario(EstadoFondo.EnReposicion);
            escenario.AgregarSolicitud(EstadoReposicion.Aprobada);

            var resultado = await escenario.CambiarEstadoAsync(EstadoFondo.Activo);

            Assert.False(resultado.Exitoso);
            Assert.Equal(EstadoFondo.EnReposicion, escenario.Fondo.Estado);
            Assert.Equal(0, escenario.Contexto.VecesGuardado);
        }

        [Fact]
        public async Task FondoEnReposicion_PuedeGuardarSusDemasParametrosSinCambiarDeEstado()
        {
            var escenario = new Escenario(EstadoFondo.EnReposicion);
            var (solicitud, _) = escenario.AgregarSolicitud(EstadoReposicion.Aprobada);

            var resultado = await escenario.CambiarEstadoAsync(EstadoFondo.EnReposicion);

            Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores));
            Assert.Equal(EstadoFondo.EnReposicion, escenario.Fondo.Estado);
            Assert.Equal(EstadoReposicion.Aprobada, solicitud.Estado);
        }

        [Fact]
        public async Task FondoActivo_NoSePuedePonerEnReposicionAMano()
        {
            var escenario = new Escenario();

            var resultado = await escenario.CambiarEstadoAsync(EstadoFondo.EnReposicion);

            Assert.False(resultado.Exitoso);
            Assert.Equal(EstadoFondo.Activo, escenario.Fondo.Estado);
            Assert.Equal(0, escenario.Contexto.VecesGuardado);
        }

        [Fact]
        public async Task FondoInactivo_SePuedeReactivar()
        {
            var escenario = new Escenario(EstadoFondo.Inactivo);

            var resultado = await escenario.CambiarEstadoAsync(EstadoFondo.Activo);

            Assert.True(resultado.Exitoso, string.Join("; ", resultado.Errores));
            Assert.Equal(EstadoFondo.Activo, escenario.Fondo.Estado);
        }
    }
}

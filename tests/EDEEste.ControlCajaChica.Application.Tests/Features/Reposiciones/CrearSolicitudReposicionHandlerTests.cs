using System;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Models;
using EDEEste.ControlCajaChica.Application.Features.Reposiciones;
using EDEEste.ControlCajaChica.Application.Tests.TestDoubles;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
using Xunit;

namespace EDEEste.ControlCajaChica.Application.Tests.Features.Reposiciones
{
    /// <summary>
    /// Cubre puntualmente el manejo de conflictos de concurrencia: Gasto.Estado es
    /// token de concurrencia (ver ApplicationDbContext), y este handler es uno de los
    /// dos que hasta hace poco llamaban SaveChangesAsync directo en vez de
    /// IntentarGuardarCambiosAsync, dejando la excepcion de EF Core sin traducir y el
    /// ChangeTracker sucio para el resto del circuito de Blazor Server.
    /// </summary>
    public class CrearSolicitudReposicionHandlerTests
    {
        [Fact]
        public async Task Guardado_ConConflictoDeConcurrencia_FallaYNoCuentaComoGuardado()
        {
            var escenario = new Escenario();
            escenario.Contexto.FallarPorConcurrencia = true;

            var resultado = await escenario.EjecutarAsync();

            Assert.False(resultado.Exitoso);
            Assert.Contains(resultado.Errores, e => e.Contains("Otro usuario modificó"));
            Assert.Equal(0, escenario.Contexto.VecesGuardado);
        }

        [Fact]
        public async Task Guardado_Exitoso_RegistraElHashDelExpediente()
        {
            var escenario = new Escenario();

            var resultado = await escenario.EjecutarAsync();

            Assert.True(resultado.Exitoso);
            var solicitud = await escenario.Reposiciones.ObtenerConDetalleAsync(resultado.Valor);
            Assert.NotNull(solicitud);
            Assert.Equal(FakeFileStorageService.HashDePrueba, solicitud.HashPdfConsolidado);
        }

        /// <summary>Un fondo con un gasto pendiente, listo para pedir su reposicion.</summary>
        private sealed class Escenario
        {
            private readonly FondoCajaChica _fondo = new() { MontoFijo = 10000m, BalanceActual = 2000m };
            private readonly FakeFondoRepository _fondos = new();
            private readonly FakeGastoRepository _gastos = new();

            public FakeReposicionRepository Reposiciones { get; } = new();
            public FakeApplicationDbContext Contexto { get; } = new();

            public Escenario()
            {
                _fondos.Agregar(_fondo);
                _gastos.Agregar(new Gasto
                {
                    FondoCajaChicaId = _fondo.Id,
                    MontoTotal = 500m,
                    NCF = "B0100000001",
                    Estado = EstadoGasto.PendienteReposicion
                });
            }

            public Task<ResultadoOperacion<Guid>> EjecutarAsync()
            {
                var handler = new CrearSolicitudReposicionHandler(
                    _fondos, _gastos, Reposiciones, new FakePdfConsolidadorService(), new FakeFileStorageService(),
                    new FakeCurrentUserService(), new FakeIdentityService(), new FakeAutorizacionService(), Contexto);

                return handler.EjecutarAsync(new CrearSolicitudReposicionCommand { FondoCajaChicaId = _fondo.Id });
            }
        }
    }
}

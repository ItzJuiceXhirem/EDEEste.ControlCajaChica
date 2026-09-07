using System.Threading.Tasks;
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
            var fondo = new FondoCajaChica { MontoFijo = 10000m, BalanceActual = 2000m };

            var fondos = new FakeFondoRepository();
            fondos.Agregar(fondo);

            var gasto = new Gasto
            {
                FondoCajaChicaId = fondo.Id,
                MontoTotal = 500m,
                NCF = "B0100000001",
                Estado = EstadoGasto.PendienteReposicion
            };

            var gastos = new FakeGastoRepository();
            gastos.Agregar(gasto);

            var reposiciones = new FakeReposicionRepository();
            var contexto = new FakeApplicationDbContext();
            contexto.FallarPorConcurrencia = true;

            var handler = new CrearSolicitudReposicionHandler(
                fondos, gastos, reposiciones, new FakePdfConsolidadorService(), new FakeFileStorageService(),
                new FakeCurrentUserService(), new FakeIdentityService(), new FakeAutorizacionService(), contexto);

            var resultado = await handler.EjecutarAsync(new CrearSolicitudReposicionCommand
            {
                FondoCajaChicaId = fondo.Id
            });

            Assert.False(resultado.Exitoso);
            Assert.Contains(resultado.Errores, e => e.Contains("Otro usuario modificó"));
            Assert.Equal(0, contexto.VecesGuardado);
        }
    }
}

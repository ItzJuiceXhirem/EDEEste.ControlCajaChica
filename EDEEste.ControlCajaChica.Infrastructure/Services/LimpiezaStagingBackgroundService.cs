using System;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EDEEste.ControlCajaChica.Infrastructure.Services
{
    /// <summary>
    /// Respaldo global del barrido de staging abandonado.
    ///
    /// El mecanismo real es el barrido oportunista que corre en cada subida (ver
    /// GastoEndpoints.MapPost "/gastos/comprobantes/staging"): bajo IIS el app pool
    /// se duerme (20 minutos por defecto), y en una app interna de bajo trafico este
    /// servicio de fondo puede simplemente no llegar a ejecutarse nunca. Este es el
    /// respaldo para la persona que sube un comprobante, abandona el formulario, y
    /// no vuelve a subir nada mas que dispare el barrido oportunista.
    /// </summary>
    public sealed class LimpiezaStagingBackgroundService : BackgroundService
    {
        private static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LimpiezaStagingBackgroundService> _logger;

        public LimpiezaStagingBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<LimpiezaStagingBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var temporizador = new PeriodicTimer(Intervalo);

            while (await temporizador.WaitForNextTickAsync(stoppingToken))
            {
                // Todo el cuerpo en try/catch a proposito: desde .NET 6, una
                // excepcion sin capturar en un BackgroundService detiene el HOST
                // ENTERO (BackgroundServiceExceptionBehavior.StopHost es el default).
                // Un solo archivo bloqueado por el antivirus de red -- el mismo que
                // motivo sacar la subida del circuito -- no debe poder tumbar toda
                // la aplicacion.
                try
                {
                    // IFileStorageService es Scoped; este servicio es Singleton (como
                    // todo IHostedService), asi que cada pasada abre su propio scope
                    // en vez de recibirlo por constructor.
                    using var alcance = _scopeFactory.CreateScope();
                    var almacenamiento = alcance.ServiceProvider.GetRequiredService<IFileStorageService>();

                    var borrados = await almacenamiento.LimpiarStagingAbandonadoAsync(
                        OpcionesAlmacenamiento.RetencionStaging, stoppingToken);

                    if (borrados > 0)
                    {
                        _logger.LogInformation(
                            "Barrido de staging: se borraron {Cantidad} archivos abandonados.", borrados);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Apagado normal de la aplicacion, no un fallo del barrido.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fallo el barrido de staging abandonado; se reintenta en la proxima pasada.");
                }
            }
        }
    }
}

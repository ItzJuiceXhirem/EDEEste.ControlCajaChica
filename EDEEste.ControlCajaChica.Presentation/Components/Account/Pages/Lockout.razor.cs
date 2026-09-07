using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace EDEEste.ControlCajaChica.Presentation.Components.Account.Pages
{
    /// <summary>
    /// Cuenta regresiva hasta que Identity levanta el bloqueo. Recibe la hora exacta
    /// (UTC) por query string desde Login.razor.cs -- no hace ninguna consulta propia
    /// a la base de datos, el fin del bloqueo ya lo sabe quien redirigio para aca.
    /// </summary>
    public partial class Lockout : IDisposable
    {
        [SupplyParameterFromQuery(Name = "hasta")]
        private string? HastaQuery { get; set; }

        private DateTimeOffset? hasta;
        private PeriodicTimer? temporizador;
        private CancellationTokenSource? cts;

        // Sin "hasta" (se entro directo a la URL, sin pasar por el redirect de
        // Login) no hay como saber cuanto falta -- se muestra el aviso generico de
        // siempre en vez de fingir un countdown o un "ya puede reintentar" que
        // podria no ser cierto.
        private bool TieneCuentaRegresiva => hasta is not null;

        private TimeSpan Restante
        {
            get
            {
                if (hasta is not { } fecha)
                {
                    return TimeSpan.Zero;
                }

                var restante = fecha - DateTimeOffset.UtcNow;
                return restante > TimeSpan.Zero ? restante : TimeSpan.Zero;
            }
        }

        private bool Terminado => TieneCuentaRegresiva && Restante <= TimeSpan.Zero;

        private bool Deshabilitado => TieneCuentaRegresiva && !Terminado;

        private string TiempoFormateado =>
            $"{(int)Restante.TotalMinutes:00}:{Restante.Seconds:00}";

        protected override void OnInitialized()
        {
            if (DateTimeOffset.TryParse(
                    HastaQuery,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var fecha))
            {
                hasta = fecha;
            }

            if (TieneCuentaRegresiva && !Terminado)
            {
                cts = new CancellationTokenSource();
                _ = ContarAsync(cts.Token);
            }
        }

        private async Task ContarAsync(CancellationToken cancellationToken)
        {
            temporizador = new PeriodicTimer(TimeSpan.FromSeconds(1));

            try
            {
                while (await temporizador.WaitForNextTickAsync(cancellationToken))
                {
                    var yaTerminado = Terminado;
                    await InvokeAsync(StateHasChanged);

                    if (yaTerminado)
                    {
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // El usuario salió de la pantalla; Dispose ya canceló el token.
            }
        }

        public void Dispose()
        {
            cts?.Cancel();
            cts?.Dispose();
            temporizador?.Dispose();
        }
    }
}

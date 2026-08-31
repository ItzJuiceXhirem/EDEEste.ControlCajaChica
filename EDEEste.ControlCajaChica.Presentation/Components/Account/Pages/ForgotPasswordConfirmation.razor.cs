using System;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace EDEEste.ControlCajaChica.Presentation.Components.Account.Pages
{
    /// <summary>
    /// Pantalla de espera tras pedir un restablecimiento de contraseña. Sondea el
    /// estado de la solicitud cada 60 segundos solo para reflejar si se aprobo o se
    /// ignoro -- YA NO redirige sola al formulario de la nueva contraseña: el enlace
    /// real lleva un secreto de un solo uso que solo conoce el Administrador (ver
    /// PasswordResetService.AceptarAsync), asi que aunque esta pantalla supiera que
    /// la solicitud fue aprobada, no tiene forma de completar el cambio por su
    /// cuenta. Quien la pidio sigue teniendo que esperar el enlace por Teams.
    /// </summary>
    public partial class ForgotPasswordConfirmation : IDisposable
    {
        private static readonly TimeSpan IntervaloSondeo = TimeSpan.FromSeconds(60);

        [Inject] private IPasswordResetService PasswordResetService { get; set; } = default!;
        [Inject] private NavigationManager NavigationManager { get; set; } = default!;

        [SupplyParameterFromQuery(Name = "solicitud")]
        private string? SolicitudId { get; set; }

        private bool ignorada;
        private bool aprobada;
        private CancellationTokenSource? _cts;

        protected override void OnInitialized()
        {
            if (string.IsNullOrWhiteSpace(SolicitudId))
            {
                NavigationManager.NavigateTo("Account/ForgotPassword");
                return;
            }

            _cts = new CancellationTokenSource();
            _ = SondearAsync(SolicitudId, _cts.Token);
        }

        private async Task SondearAsync(string solicitudId, CancellationToken cancellationToken)
        {
            using var temporizador = new PeriodicTimer(IntervaloSondeo);

            try
            {
                while (await temporizador.WaitForNextTickAsync(cancellationToken))
                {
                    var estado = await PasswordResetService.ObtenerEstadoAsync(solicitudId);

                    if (estado == EstadoSolicitudPasswordReset.Aprobada)
                    {
                        aprobada = true;
                        await InvokeAsync(StateHasChanged);
                        return;
                    }

                    if (estado == EstadoSolicitudPasswordReset.Ignorada)
                    {
                        ignorada = true;
                        await InvokeAsync(StateHasChanged);
                        return;
                    }

                    // Pendiente (o la solicitud ya no existe): se sigue esperando al
                    // siguiente tick, no hay nada nuevo que mostrar todavía.
                }
            }
            catch (OperationCanceledException)
            {
                // El usuario salió de la pantalla; Dispose ya canceló el token.
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}

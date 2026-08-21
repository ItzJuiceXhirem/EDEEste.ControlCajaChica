using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using Microsoft.AspNetCore.Components;

namespace EDEEste.ControlCajaChica.Presentation.Components.Account.Pages
{
    public partial class ForgotPassword
    {
        [Inject] private IPasswordResetService PasswordResetService { get; set; } = default!;
        [Inject] private IdentityRedirectManager RedirectManager { get; set; } = default!;

        private bool cuentaNoExiste;

        [SupplyParameterFromForm]
        private InputModel Input { get; set; } = default!;

        protected override void OnInitialized() => Input ??= new();

        private async Task OnValidSubmitAsync()
        {
            cuentaNoExiste = false;

            var solicitudId = await PasswordResetService.SolicitarAsync(Input.Usuario.Trim());
            if (solicitudId is null)
            {
                // A diferencia del login, aquí SÍ se avisa que la cuenta no existe: en
                // un sistema interno el formato nombre.apellido ya es predecible para
                // cualquiera de la empresa, así que callarlo no protege nada real y
                // solo deja a la persona atascada sin saber por qué.
                cuentaNoExiste = true;
                return;
            }

            RedirectManager.RedirectTo(
                "Account/ForgotPasswordConfirmation",
                new Dictionary<string, object?> { ["solicitud"] = solicitudId });
        }

        private sealed class InputModel
        {
            [Required(ErrorMessage = "Indique su usuario.")]
            [Display(Name = "Usuario")]
            public string Usuario { get; set; } = "";
        }
    }
}

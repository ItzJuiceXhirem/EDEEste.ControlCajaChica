using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using Microsoft.AspNetCore.Components;

namespace EDEEste.ControlCajaChica.Presentation.Components.Account.Pages
{
    public partial class ResetPassword
    {
        [Inject] private IPasswordResetService PasswordResetService { get; set; } = default!;
        [Inject] private IdentityRedirectManager RedirectManager { get; set; } = default!;

        [Parameter] public Guid Id { get; set; }

        [CascadingParameter] private HttpContext HttpContext { get; set; } = default!;

        [SupplyParameterFromForm]
        private InputModel Input { get; set; } = default!;

        private string? errorMessage;
        private bool invalida;

        private string? Message => errorMessage is null ? null : $"Error: {errorMessage}";

        protected override async Task OnInitializedAsync()
        {
            Input ??= new();

            // Vale la pena repetir esta comprobación aquí aunque ResetPasswordAsync la
            // vuelva a hacer al guardar: sin esto, un enlace ya usado o nunca aprobado
            // mostraría el formulario igual y el usuario no sabría por qué falla hasta
            // que lo intenta.
            if (!await PasswordResetService.PuedeRestablecerAsync(Id.ToString()))
            {
                invalida = true;
                RedirectManager.RedirectTo("Account/InvalidPasswordReset");
            }
        }

        private async Task OnValidSubmitAsync()
        {
            var resultado = await PasswordResetService.RestablecerAsync(Id.ToString(), Input.Password);

            if (!resultado.Exitoso)
            {
                errorMessage = string.Join(" ", resultado.Errores);
                return;
            }

            RedirectManager.RedirectToWithStatus(
                "Account/Login",
                "Su contraseña fue actualizada. Ya puede iniciar sesión.",
                HttpContext);
        }

        private sealed class InputModel
        {
            [Required(ErrorMessage = "Indique la contraseña nueva.")]
            [StringLength(100, ErrorMessage = "La {0} debe tener entre {2} y {1} caracteres.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Contraseña nueva")]
            public string Password { get; set; } = "";

            [DataType(DataType.Password)]
            [Display(Name = "Confirmar contraseña")]
            [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
            public string ConfirmPassword { get; set; } = "";
        }
    }
}

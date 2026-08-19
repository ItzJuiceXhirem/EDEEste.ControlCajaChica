using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Infrastructure.Identity;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace EDEEste.ControlCajaChica.Presentation.Components.Account.Pages.Manage
{
    public partial class Index
    {
        [Inject] private UserManager<Usuario> UserManager { get; set; } = default!;
        [Inject] private SignInManager<Usuario> SignInManager { get; set; } = default!;
        [Inject] private IdentityRedirectManager RedirectManager { get; set; } = default!;

        private Usuario? cuenta;
        private string? usuario;
        private string? nombre;
        private string? telefono;

        [CascadingParameter]
        private HttpContext HttpContext { get; set; } = default!;

        [SupplyParameterFromForm]
        private InputModel Input { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            Input ??= new();

            cuenta = await UserManager.GetUserAsync(HttpContext.User);
            if (cuenta is null)
            {
                RedirectManager.RedirectToInvalidUser(UserManager, HttpContext);
                return;
            }

            usuario = await UserManager.GetUserNameAsync(cuenta);
            nombre = cuenta.Nombre;
            telefono = await UserManager.GetPhoneNumberAsync(cuenta);

            Input.Telefono ??= telefono;
        }

        private async Task OnValidSubmitAsync()
        {
            if (cuenta is null)
            {
                RedirectManager.RedirectToInvalidUser(UserManager, HttpContext);
                return;
            }

            // El usuario y el nombre no se editan aquí a propósito: el usuario es el
            // de la empresa (y en modo ActiveDirectory lo manda el directorio), y el
            // nombre lo fija quien crea la cuenta. El teléfono sí es dato propio.
            if (Input.Telefono != telefono)
            {
                var resultado = await UserManager.SetPhoneNumberAsync(cuenta, Input.Telefono);
                if (!resultado.Succeeded)
                {
                    RedirectManager.RedirectToCurrentPageWithStatus(
                        "Error: no se pudo guardar el teléfono.", HttpContext);
                    return;
                }
            }

            await SignInManager.RefreshSignInAsync(cuenta);
            RedirectManager.RedirectToCurrentPageWithStatus("Su perfil fue actualizado.", HttpContext);
        }

        private sealed class InputModel
        {
            [Phone(ErrorMessage = "El teléfono no tiene un formato válido.")]
            [Display(Name = "Teléfono")]
            public string? Telefono { get; set; }
        }
    }
}

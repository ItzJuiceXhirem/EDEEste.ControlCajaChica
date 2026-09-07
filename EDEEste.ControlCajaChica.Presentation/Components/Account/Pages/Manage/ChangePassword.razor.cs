using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Infrastructure.Identity;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace EDEEste.ControlCajaChica.Presentation.Components.Account.Pages.Manage
{
    public partial class ChangePassword
    {
        [Inject] private UserManager<Usuario> UserManager { get; set; } = default!;
        [Inject] private SignInManager<Usuario> SignInManager { get; set; } = default!;
        [Inject] private IdentityRedirectManager RedirectManager { get; set; } = default!;
        [Inject] private ILogger<ChangePassword> Logger { get; set; } = default!;

        private string? message;
        private Usuario? cuenta;
        private bool tienePassword = true;

        [CascadingParameter]
        private HttpContext HttpContext { get; set; } = default!;

        // La carga la sirve ManageLayout (ver su comentario): llamar GetUserAsync
        // aqui tambien competiria por el mismo DbContext con scope de la peticion.
        [CascadingParameter]
        private Task<Usuario?> CuentaTask { get; set; } = default!;

        [SupplyParameterFromForm]
        private InputModel Input { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            Input ??= new();

            cuenta = await CuentaTask;
            if (cuenta is null)
            {
                RedirectManager.RedirectToInvalidUser(UserManager, HttpContext);
                return;
            }

            // Hoy toda cuenta se crea con contraseña, así que esto no debería ocurrir;
            // se contempla porque en modo ActiveDirectory la contraseña vivirá en el
            // directorio y no aquí. Antes esto redirigía a una página SetPassword del
            // scaffold que ya no existe, así que ahora se explica en la misma pantalla.
            tienePassword = await UserManager.HasPasswordAsync(cuenta);
        }

        private async Task OnValidSubmitAsync()
        {
            if (cuenta is null)
            {
                RedirectManager.RedirectToInvalidUser(UserManager, HttpContext);
                return;
            }

            var resultado = await UserManager.ChangePasswordAsync(
                cuenta, Input.PasswordActual, Input.PasswordNueva);

            if (!resultado.Succeeded)
            {
                message = $"Error: {string.Join(" ", resultado.Errors.Select(e => e.Description))}";
                return;
            }

            await SignInManager.RefreshSignInAsync(cuenta);
            Logger.LogInformation("El usuario {Usuario} cambió su contraseña.", cuenta.UserName);

            RedirectManager.RedirectToCurrentPageWithStatus("Su contraseña fue actualizada.", HttpContext);
        }

        private sealed class InputModel
        {
            [Required(ErrorMessage = "Indique su contraseña actual.")]
            [DataType(DataType.Password)]
            [Display(Name = "Contraseña actual")]
            public string PasswordActual { get; set; } = "";

            [Required(ErrorMessage = "Indique la contraseña nueva.")]
            [StringLength(LimitesContrasena.LongitudMaxima, ErrorMessage = "La {0} debe tener entre {2} y {1} caracteres.", MinimumLength = LimitesContrasena.LongitudMinima)]
            [DataType(DataType.Password)]
            [Display(Name = "Contraseña nueva")]
            public string PasswordNueva { get; set; } = "";

            [DataType(DataType.Password)]
            [Display(Name = "Confirmar contraseña")]
            [Compare(nameof(PasswordNueva), ErrorMessage = "Las contraseñas no coinciden.")]
            public string ConfirmarPassword { get; set; } = "";
        }
    }
}

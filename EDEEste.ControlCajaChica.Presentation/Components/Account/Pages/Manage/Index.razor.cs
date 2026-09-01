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
        private string? extension;

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

            usuario = await UserManager.GetUserNameAsync(cuenta);
            nombre = cuenta.Nombre;
            // La extension vive en la misma columna que antes guardaba el telefono
            // (PhoneNumber de Identity): sigue siendo "un numero de contacto propio",
            // solo cambio el significado y el formato que se le exige.
            extension = await UserManager.GetPhoneNumberAsync(cuenta);

            Input.Extension ??= extension;
        }

        private async Task OnValidSubmitAsync()
        {
            if (cuenta is null)
            {
                RedirectManager.RedirectToInvalidUser(UserManager, HttpContext);
                return;
            }

            // El usuario y el nombre no se editan aqui a proposito: el usuario es el
            // de la empresa (y en modo ActiveDirectory lo manda el directorio), y el
            // nombre lo fija quien crea la cuenta -- todavia no existe el flujo de
            // solicitar-y-aprobar un cambio de nombre. La extension si es dato propio.
            if (Input.Extension != extension)
            {
                var resultado = await UserManager.SetPhoneNumberAsync(cuenta, Input.Extension);
                if (!resultado.Succeeded)
                {
                    RedirectManager.RedirectToCurrentPageWithStatus(
                        "Error: no se pudo guardar la extensión.", HttpContext);
                    return;
                }
            }

            await SignInManager.RefreshSignInAsync(cuenta);
            RedirectManager.RedirectToCurrentPageWithStatus("Su perfil fue actualizado.", HttpContext);
        }

        private sealed class InputModel
        {
            [Required(ErrorMessage = "La extensión es obligatoria.")]
            [RegularExpression("^[0-9]{4}$", ErrorMessage = "La extensión debe tener exactamente 4 dígitos numéricos.")]
            [Display(Name = "Extensión")]
            public string? Extension { get; set; }
        }
    }
}

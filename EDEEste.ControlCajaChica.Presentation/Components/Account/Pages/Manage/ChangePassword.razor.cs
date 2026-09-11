using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
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

        // La carga la sirve ManageLayout (ver su comentario). Es una copia de solo
        // lectura: para cambiar la contraseña se carga una propia (ver OnValidSubmitAsync).
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

            // La copia de ManageLayout viene de su propio ambito de DI, ya desprendida.
            // ChangePasswordAsync termina en un Attach sobre el DbContext de esta
            // peticion, y ese Attach revienta si el DbContext ya rastrea al usuario --
            // pasa cada vez que la validacion del sello de la cookie corre en esta misma
            // peticion. Por eso se carga una copia propia justo antes de cambiarla.
            var fresca = await UserManager.FindByIdAsync(cuenta.Id);
            if (fresca is null)
            {
                RedirectManager.RedirectToInvalidUser(UserManager, HttpContext);
                return;
            }

            var resultado = await UserManager.ChangePasswordAsync(
                fresca, Input.PasswordActual, Input.PasswordNueva);

            if (!resultado.Succeeded)
            {
                message = $"Error: {string.Join(" ", resultado.Errors.Select(e => e.Description))}";
                return;
            }

            await ReemitirSesionAsync(fresca);
            Logger.LogInformation("El usuario {Usuario} cambió su contraseña.", fresca.UserName);

            RedirectManager.RedirectToCurrentPageWithStatus("Su contraseña fue actualizada.", HttpContext);
        }

        /// <summary>
        /// ChangePasswordAsync rota el sello de seguridad: sin reemitir la cookie, la
        /// validacion del sello terminaria cerrando tambien esta sesion (las de otros
        /// dispositivos si deben cerrarse, y se cierran). Hace lo mismo que
        /// SignInManager.RefreshSignInAsync -- conserva los claims amr/metodo de
        /// autenticacion y las propiedades de la sesion, "Recordarme" incluido -- pero
        /// ademas conserva UltimoAccesoAnterior, que RefreshSignInAsync descarta: sin
        /// esto, "Ultima sesion" desaparecia del perfil hasta el proximo login.
        /// </summary>
        private async Task ReemitirSesionAsync(Usuario usuario)
        {
            var sesion = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            var conservar = sesion.Principal?.Claims
                .Where(c => c.Type is ClaimsApp.UltimoAccesoAnterior or ClaimTypes.AuthenticationMethod or "amr")
                .ToList() ?? [];

            await SignInManager.SignInWithClaimsAsync(usuario, sesion.Properties, conservar);
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

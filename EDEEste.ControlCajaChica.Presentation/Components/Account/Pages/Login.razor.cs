using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Enums;
using EDEEste.ControlCajaChica.Infrastructure.Identity;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;

namespace EDEEste.ControlCajaChica.Presentation.Components.Account.Pages
{
    public partial class Login
    {
        [Inject] private UserManager<Usuario> UserManager { get; set; } = default!;
        [Inject] private SignInManager<Usuario> SignInManager { get; set; } = default!;
        [Inject] private IIdentityService IdentityService { get; set; } = default!;
        [Inject] private ILogger<Login> Logger { get; set; } = default!;
        [Inject] private NavigationManager NavigationManager { get; set; } = default!;
        [Inject] private IdentityRedirectManager RedirectManager { get; set; } = default!;

        private string? errorMessage;
        private bool sinAdministrador;
        private EditContext editContext = default!;

        [SupplyParameterFromForm]
        private InputModel Input { get; set; } = default!;

        [SupplyParameterFromQuery]
        private string? ReturnUrl { get; set; }

        protected override async Task OnInitializedAsync()
        {
            Input ??= new();
            editContext = new EditContext(Input);

            // El aviso de "falta configurar el Administrador" va aqui y no en Home
            // porque a Home no llega nadie sin sesion: el sitio entero redirige a
            // esta pantalla, asi que es el unico lugar donde se puede ver.
            sinAdministrador = !await IdentityService.ExisteAdministradorAsync();
        }

        /// <summary>
        /// El orden importa por seguridad. Primero se comprueba la contrasena y solo
        /// despues el estado de la cuenta.
        ///
        /// No se usa PasswordSignInAsync porque internamente llama a CanSignInAsync
        /// ANTES de verificar la contrasena: con eso, cualquiera que escribiera un
        /// usuario existente veria si esa cuenta esta denegada o pendiente sin
        /// conocer la contrasena. Verificando primero, el estado solo se le revela a
        /// quien ya demostro ser el dueno de la cuenta.
        /// </summary>
        public async Task LoginUser()
        {
            if (!editContext.Validate())
            {
                return;
            }

            var usuario = await UserManager.FindByNameAsync(Input.Usuario);

            if (usuario is null || !await UserManager.CheckPasswordAsync(usuario, Input.Password))
            {
                // Mismo mensaje para "no existe" y "contrasena incorrecta": no hay
                // razon para ayudar a alguien a averiguar que cuentas existen.
                errorMessage = "Error: usuario o contrasena invalidos.";
                return;
            }

            switch (usuario.EstadoAcceso)
            {
                case EstadoAccesoUsuario.Pendiente:
                    RedirectManager.RedirectTo("acceso-pendiente");
                    return;

                case EstadoAccesoUsuario.Denegado:
                    RedirectManager.RedirectTo("acceso-denegado");
                    return;
            }

            await SignInManager.SignInAsync(usuario, Input.RememberMe);
            Logger.LogInformation("El usuario {Usuario} inicio sesion.", usuario.UserName);
            RedirectManager.RedirectTo(ReturnUrl);
        }

        private sealed class InputModel
        {
            [Required(ErrorMessage = "Indique su usuario.")]
            [Display(Name = "Usuario")]
            public string Usuario { get; set; } = "";

            [Required(ErrorMessage = "Indique su contrasena.")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = "";

            [Display(Name = "Recordarme")]
            public bool RememberMe { get; set; }
        }
    }
}

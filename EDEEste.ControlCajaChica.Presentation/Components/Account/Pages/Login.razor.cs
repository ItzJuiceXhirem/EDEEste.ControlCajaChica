using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.Common.Models;
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
        [Inject] private IAutenticadorCredenciales Autenticador { get; set; } = default!;
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

            // El aviso de "falta configurar el Administrador" va aquí y no en Home
            // porque a Home no llega nadie sin sesión: el sitio entero redirige a
            // esta pantalla, así que es el único lugar donde se puede ver.
            sinAdministrador = !await IdentityService.ExisteAdministradorAsync();
        }

        /// <summary>
        /// El orden importa por seguridad. Primero se comprueba la contraseña y solo
        /// después el estado de la cuenta.
        ///
        /// No se usa PasswordSignInAsync porque internamente llama a CanSignInAsync
        /// ANTES de verificar la contraseña: con eso, cualquiera que escribiera un
        /// usuario existente vería si esa cuenta está denegada o pendiente sin
        /// conocer la contraseña. Verificando primero, el estado solo se le revela a
        /// quien ya demostró ser el dueño de la cuenta.
        ///
        /// La contraseña la verifica IAutenticadorCredenciales y no UserManager
        /// directamente: así, cuando se active el modo ActiveDirectory, esta pantalla
        /// no cambia. El registro local (Usuario) se sigue necesitando en los dos
        /// modos, porque es donde viven el estado de acceso y el rol.
        /// </summary>
        public async Task LoginUser()
        {
            if (!editContext.Validate())
            {
                return;
            }

            var usuario = await UserManager.FindByNameAsync(Input.Usuario);

            ResultadoAutenticacion credenciales;
            try
            {
                credenciales = await Autenticador.ValidarAsync(Input.Usuario, Input.Password);
            }
            catch (NotSupportedException ex)
            {
                // Modo ActiveDirectory sin implementar: es un fallo de configuración,
                // no de credenciales, y se muestra tal cual en vez de disfrazarlo de
                // "contraseña inválida", que mandaría a buscar el problema al lugar
                // equivocado.
                Logger.LogError(ex, "Modo de autenticación no soportado al intentar iniciar sesión.");
                errorMessage = $"Error: {ex.Message}";
                return;
            }

            if (usuario is null || !credenciales.Exitoso)
            {
                // Mismo mensaje para "no existe" y "contraseña incorrecta": no hay
                // razón para ayudar a alguien a averiguar qué cuentas existen.
                errorMessage = "Error: usuario o contraseña inválidos.";
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
            Logger.LogInformation("El usuario {Usuario} inició sesión.", usuario.UserName);
            RedirectManager.RedirectTo(ReturnUrl);
        }

        private sealed class InputModel
        {
            [Required(ErrorMessage = "Indique su usuario.")]
            [Display(Name = "Usuario")]
            public string Usuario { get; set; } = "";

            [Required(ErrorMessage = "Indique su contraseña.")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = "";

            [Display(Name = "Recordarme")]
            public bool RememberMe { get; set; }
        }
    }
}

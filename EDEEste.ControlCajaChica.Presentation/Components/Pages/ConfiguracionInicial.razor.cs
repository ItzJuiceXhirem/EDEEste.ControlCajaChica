using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Presentation.Components.Account;
using EDEEste.ControlCajaChica.Presentation.Components.Account.Pages;
using Microsoft.AspNetCore.Components;

namespace EDEEste.ControlCajaChica.Presentation.Components.Pages
{
    /// <summary>
    /// Arranque del sistema: crea el primer Administrador.
    ///
    /// Es la unica pantalla sin [Authorize], y tiene que serlo: nadie puede iniciar
    /// sesion todavia cuando se usa. Lo que la protege no es la autenticacion sino
    /// que se autoinvalida — en cuanto existe un Administrador deja de mostrar el
    /// formulario y redirige, asi que la ventana en la que es util se cierra sola.
    /// </summary>
    public partial class ConfiguracionInicial
    {
        [Inject] private IIdentityService IdentityService { get; set; } = default!;
        [Inject] private IdentityRedirectManager RedirectManager { get; set; } = default!;
        [Inject] private ILogger<ConfiguracionInicial> Logger { get; set; } = default!;

        private readonly List<string> errores = new();
        private bool yaHayAdministrador;
        private bool creando;

        [SupplyParameterFromForm]
        private InputModel Input { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            Input ??= new();

            yaHayAdministrador = await IdentityService.ExisteAdministradorAsync();
            if (yaHayAdministrador)
            {
                RedirectManager.RedirectTo("");
            }
        }

        private async Task CrearAdministradorAsync()
        {
            errores.Clear();

            // Se vuelve a comprobar justo antes de crear, no solo al cargar: entre
            // que se abrio la pantalla y se envio el formulario pudo crearse un
            // Administrador desde otra pestana o dispositivo.
            if (await IdentityService.ExisteAdministradorAsync())
            {
                yaHayAdministrador = true;
                RedirectManager.RedirectTo("");
                return;
            }

            creando = true;
            try
            {
                var resultado = await IdentityService.CrearUsuarioAsync(
                    Input.Usuario.Trim(),
                    Input.Password,
                    Input.Nombre.Trim(),
                    RolesApp.Administrador);

                if (!resultado.Exitoso)
                {
                    errores.AddRange(resultado.Errores);
                    return;
                }

                Logger.LogInformation(
                    "Se creo el Administrador inicial del sistema: {Usuario}.", Input.Usuario);

                // Se manda al login en vez de iniciar sesion aqui: emitir la cookie
                // requiere el HttpContext de una peticion, y esta pagina ya esta en
                // un circuito interactivo cuando se envia el formulario.
                RedirectManager.RedirectTo("Account/Login");
            }
            finally
            {
                creando = false;
            }
        }

        private sealed class InputModel
        {
            [Required(ErrorMessage = "Indique el usuario.")]
            [StringLength(100, MinimumLength = 3)]
            [RegularExpression(
                Register.PatronUsuario,
                ErrorMessage = "El usuario debe tener el formato nombre.apellido: un solo punto, y no puede ir al principio ni al final.")]
            [Display(Name = "Usuario")]
            public string Usuario { get; set; } = "";

            [Required(ErrorMessage = "Indique el nombre completo.")]
            [StringLength(150, MinimumLength = 3)]
            [Display(Name = "Nombre completo")]
            public string Nombre { get; set; } = "";

            [Required(ErrorMessage = "Indique una contrasena.")]
            [StringLength(100, ErrorMessage = "La {0} debe tener entre {2} y {1} caracteres.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Contrasena")]
            public string Password { get; set; } = "";

            [DataType(DataType.Password)]
            [Display(Name = "Confirmar contrasena")]
            [Compare(nameof(Password), ErrorMessage = "Las contrasenas no coinciden.")]
            public string ConfirmPassword { get; set; } = "";
        }
    }
}

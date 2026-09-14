using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Infrastructure.Configuration;
using EDEEste.ControlCajaChica.Presentation.Components.Account;
using EDEEste.ControlCajaChica.Presentation.Components.Account.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace EDEEste.ControlCajaChica.Presentation.Components.Pages
{
    /// <summary>
    /// Arranque del sistema: crea el primer Administrador.
    ///
    /// Es la única pantalla sin [Authorize], y tiene que serlo: nadie puede iniciar
    /// sesión todavía cuando se usa. Lo que la protege no es la autenticación sino
    /// que se autoinvalida — en cuanto existe un Administrador deja de mostrar el
    /// formulario y redirige, así que la ventana en la que es útil se cierra sola.
    /// </summary>
    public partial class ConfiguracionInicial
    {
        [Inject] private IIdentityService IdentityService { get; set; } = default!;
        [Inject] private IdentityRedirectManager RedirectManager { get; set; } = default!;
        [Inject] private ILogger<ConfiguracionInicial> Logger { get; set; } = default!;
        [Inject] private IOptions<OpcionesConfiguracionInicial> OpcionesArranque { get; set; } = default!;

        private readonly List<string> errores = new();
        private bool yaHayAdministrador;
        private bool creando;

        /// <summary>
        /// Sin un token configurado, la pantalla no muestra el formulario en
        /// absoluto: es preferible que un despliegue nuevo se quede sin poder crear
        /// el primer Administrador (y que quien lo despliega lo note de inmediato) a
        /// dejar la puerta abierta a que cualquiera en la red llegue primero.
        /// </summary>
        private bool tokenNoConfigurado;

        [SupplyParameterFromForm]
        private InputModel Input { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            Input ??= new();

            tokenNoConfigurado = string.IsNullOrWhiteSpace(OpcionesArranque.Value.TokenArranque);

            yaHayAdministrador = await IdentityService.ExisteAdministradorAsync();
            if (yaHayAdministrador)
            {
                RedirectManager.RedirectTo("");
            }
        }

        private async Task CrearAdministradorAsync()
        {
            errores.Clear();

            if (tokenNoConfigurado)
            {
                return;
            }

            // Comparacion en tiempo constante: este token decide quien se queda con
            // el sistema completo, asi que se trata como cualquier otro secreto
            // criptografico de la aplicacion.
            var tokenEsperado = OpcionesArranque.Value.TokenArranque;
            var tokenCoincide = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(Input.Token ?? string.Empty),
                Encoding.UTF8.GetBytes(tokenEsperado));

            if (!tokenCoincide)
            {
                errores.Add("El token de arranque no es correcto.");
                return;
            }

            // Se vuelve a comprobar justo antes de crear, no solo al cargar: entre
            // que se abrió la pantalla y se envió el formulario pudo crearse un
            // Administrador desde otra pestaña o dispositivo.
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
                    "Se creó el Administrador inicial del sistema: {Usuario}.", Input.Usuario);

                // Se manda al login en vez de iniciar sesión aquí: emitir la cookie
                // requiere el HttpContext de una petición, y esta página ya está en
                // un circuito interactivo cuando se envía el formulario.
                RedirectManager.RedirectTo("Account/Login");
            }
            finally
            {
                creando = false;
            }
        }

        private sealed class InputModel
        {
            [Required(ErrorMessage = "Indique el token de arranque.")]
            [Display(Name = "Token de arranque")]
            public string Token { get; set; } = "";

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

            [Required(ErrorMessage = "Indique una contraseña.")]
            [StringLength(LimitesContrasena.LongitudMaxima, ErrorMessage = "La {0} debe tener entre {2} y {1} caracteres.", MinimumLength = LimitesContrasena.LongitudMinima)]
            [DataType(DataType.Password)]
            [Display(Name = "Contraseña")]
            public string Password { get; set; } = "";

            [DataType(DataType.Password)]
            [Display(Name = "Confirmar contraseña")]
            [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
            public string ConfirmPassword { get; set; } = "";
        }
    }
}

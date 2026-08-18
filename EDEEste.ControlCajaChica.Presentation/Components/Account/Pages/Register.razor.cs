using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using Microsoft.AspNetCore.Components;

namespace EDEEste.ControlCajaChica.Presentation.Components.Account.Pages
{
    public partial class Register
    {
        /// <summary>
        /// Formato de usuario de la empresa: exactamente un punto, y ni al principio
        /// ni al final. Al exigir que no haya puntos a cada lado, un solo patron
        /// cubre las tres reglas a la vez.
        /// </summary>
        internal const string PatronUsuario = @"^[^.\s]+\.[^.\s]+$";

        [Inject] private IIdentityService IdentityService { get; set; } = default!;
        [Inject] private ILogger<Register> Logger { get; set; } = default!;
        [Inject] private IdentityRedirectManager RedirectManager { get; set; } = default!;

        private IEnumerable<string>? errores;

        [SupplyParameterFromForm]
        private InputModel Input { get; set; } = default!;

        [SupplyParameterFromQuery]
        private string? ReturnUrl { get; set; }

        private string? Message => errores is null ? null : $"Error: {string.Join(", ", errores)}";

        protected override void OnInitialized() => Input ??= new();

        public async Task RegisterUser()
        {
            // Se crea SIN rol y en estado Pendiente: registrarse solo pone la
            // solicitud en la cola del Administrador, no habilita nada.
            var resultado = await IdentityService.CrearUsuarioPendienteAsync(
                Input.Usuario.Trim(),
                Input.Password,
                Input.Nombre.Trim());

            if (!resultado.Exitoso)
            {
                errores = resultado.Errores;
                return;
            }

            Logger.LogInformation("Nueva solicitud de acceso para el usuario {Usuario}.", Input.Usuario);
            RedirectManager.RedirectTo("acceso-pendiente");
        }

        private sealed class InputModel
        {
            [Required(ErrorMessage = "Indique su usuario.")]
            [StringLength(100, MinimumLength = 3)]
            [RegularExpression(
                PatronUsuario,
                ErrorMessage = "El usuario debe tener el formato nombre.apellido: un solo punto, y no puede ir al principio ni al final.")]
            [Display(Name = "Usuario")]
            public string Usuario { get; set; } = "";

            [Required(ErrorMessage = "Indique su nombre completo.")]
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

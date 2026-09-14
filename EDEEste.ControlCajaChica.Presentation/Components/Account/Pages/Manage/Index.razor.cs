using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Infrastructure.Identity;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace EDEEste.ControlCajaChica.Presentation.Components.Account.Pages.Manage
{
    public partial class Index
    {
        [Inject] private IServiceScopeFactory AmbitoServicios { get; set; } = default!;
        [Inject] private NavigationManager Navegacion { get; set; } = default!;

        private Usuario? cuenta;
        private string? usuario;
        private string? nombre;
        private string? extension;
        private string? mensaje;
        private bool guardando;

        private InputModel Input { get; set; } = new();

        // Esta pagina es interactiva: HttpContext solo existe durante el prerender, y
        // se usa unicamente para leer la cookie de estado (ver MensajeDeEndpoint).
        [CascadingParameter]
        private HttpContext? HttpContext { get; set; }

        [CascadingParameter]
        private Task<AuthenticationState> EstadoAutenticacion { get; set; } = default!;

        // La carga la sirve ManageLayout (ver su comentario). Es una copia de solo
        // lectura: para guardar se carga una propia (ver GuardarAsync).
        [CascadingParameter]
        private Task<Usuario?> CuentaTask { get; set; } = default!;

        /// <summary>
        /// El resultado de subir o quitar la foto. PerfilEndpoints lo deja en la cookie
        /// de estado y redirige aqui; esa cookie solo se puede leer en el prerender (el
        /// unico momento con HttpContext), y [PersistentState] lleva el texto hasta la
        /// pasada interactiva, que es la que queda en pantalla. Sin esto, el mensaje se
        /// veria un instante y desapareceria al conectar el circuito.
        /// </summary>
        [PersistentState]
        public string? MensajeDeEndpoint { get; set; }

        protected override async Task OnInitializedAsync()
        {
            if (HttpContext?.Request.Cookies[IdentityRedirectManager.StatusCookieName] is { } mensajeCookie)
            {
                MensajeDeEndpoint = mensajeCookie;
                HttpContext.Response.Cookies.Delete(IdentityRedirectManager.StatusCookieName);
            }

            cuenta = await CuentaTask;
            if (cuenta is null)
            {
                Navegacion.NavigateTo("Account/InvalidUser", forceLoad: true);
                return;
            }

            usuario = cuenta.UserName;
            nombre = cuenta.Nombre;
            // La extension vive en la misma columna que antes guardaba el telefono
            // (PhoneNumber de Identity): sigue siendo "un numero de contacto propio",
            // solo cambio el significado y el formato que se le exige.
            extension = cuenta.PhoneNumber;

            Input.Extension ??= extension;
        }

        private async Task GuardarAsync()
        {
            if (cuenta is null)
            {
                Navegacion.NavigateTo("Account/InvalidUser", forceLoad: true);
                return;
            }

            // El usuario y el nombre no se editan aqui a proposito: el usuario es el
            // de la empresa (y en modo ActiveDirectory lo manda el directorio), y el
            // nombre lo fija quien crea la cuenta -- todavia no existe el flujo de
            // solicitar-y-aprobar un cambio de nombre. La extension si es dato propio.
            if (Input.Extension == extension)
            {
                mensaje = "Su perfil fue actualizado.";
                return;
            }

            guardando = true;
            mensaje = null;

            // Ambito de DI propio y copia recien leida, a proposito:
            //  - UserStore.UpdateAsync reescribe la fila COMPLETA (Attach + Update) desde
            //    la copia en memoria. La de ManageLayout puede tener minutos, y pisaria
            //    columnas que otros flujos escriben sin cambiar el ConcurrencyStamp
            //    (TemaPreferido, RutaFotoPerfil, UltimoAccesoUtc).
            //  - El DbContext del circuito puede tener ya rastreado a este usuario
            //    (EstaEnRolAsync lo deja rastreado), y el Attach reventaria.
            // Y UpdateAsync, no SetPhoneNumberAsync: este ultimo rota el sello de
            // seguridad y la sesion se cerraria sola en la siguiente revalidacion. Ni
            // ExecuteUpdateAsync: se saltaria la bitacora de auditoria.
            using var ambito = AmbitoServicios.CreateScope();

            // Sin esto la bitacora registraria "Sistema": el AuthenticationStateProvider
            // de un ambito nuevo no esta inicializado (ver
            // ApplicationDbContext.ObtenerUsuarioAuditoriaAsync).
            if (ambito.ServiceProvider.GetRequiredService<AuthenticationStateProvider>()
                is IHostEnvironmentAuthenticationStateProvider proveedor)
            {
                proveedor.SetAuthenticationState(EstadoAutenticacion);
            }

            var userManager = ambito.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            var fresca = await userManager.FindByIdAsync(cuenta.Id);
            if (fresca is null)
            {
                Navegacion.NavigateTo("Account/InvalidUser", forceLoad: true);
                return;
            }

            fresca.PhoneNumber = Input.Extension;
            var resultado = await userManager.UpdateAsync(fresca);

            guardando = false;

            if (!resultado.Succeeded)
            {
                mensaje = "Error: no se pudo guardar la extensión.";
                return;
            }

            // Sin RefreshSignInAsync: UpdateAsync no rota el sello y la extension no es
            // un claim, asi que la cookie sigue valida tal cual -- y refrescarla
            // borraria el claim de "Ultima sesion".
            extension = Input.Extension;
            mensaje = "Su perfil fue actualizado.";
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

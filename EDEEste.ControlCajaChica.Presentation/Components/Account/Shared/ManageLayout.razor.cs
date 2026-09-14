using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Infrastructure.Identity;
using EDEEste.ControlCajaChica.Presentation.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace EDEEste.ControlCajaChica.Presentation.Components.Account.Shared
{
    public partial class ManageLayout
    {
        [Inject] private IServiceScopeFactory AmbitoServicios { get; set; } = default!;

        // Estado de autenticacion y no HttpContext: este layout lo usan Perfil
        // (interactiva, sin HttpContext dentro del circuito) y Contraseña (SSR estatico).
        [CascadingParameter]
        private Task<AuthenticationState> EstadoAutenticacion { get; set; } = default!;

        private string usuario = string.Empty;
        private string nombre = string.Empty;
        private string rol = "Sin rol";
        private string iniciales = "?";
        private DateTime? ultimaSesionAnterior;
        private Task<Usuario?> cuentaTask = default!;
        private string? urlFoto;

        private bool TieneFoto => urlFoto is not null;

        /// <summary>
        /// Perfil es interactiva y se prerenderiza: en esa primera pasada el
        /// &lt;input type="file"&gt; ya existe, pero Blazor lo reemplaza al conectar el
        /// circuito, y un archivo elegido justo en ese momento se perderia sin aviso.
        /// Contraseña es SSR estatico (sin modo asignado y sin re-render), asi que ahi el
        /// avatar funciona desde el principio. No sirve una bandera puesta en
        /// OnAfterRenderAsync, como en RegistrarGasto: en SSR estatico nunca corre, y el
        /// avatar quedaria deshabilitado para siempre en Contraseña.
        /// </summary>
        private bool FotoDisponible => AssignedRenderMode is null || RendererInfo.IsInteractive;

        /// <summary>
        /// La foto pisa el degradado dorado del avatar. Va como estilo en linea y no
        /// como clase porque la URL depende del usuario; el recorte al circulo lo hace
        /// el CSS (background-size: cover), sin reprocesar la imagen en el servidor.
        /// </summary>
        private string? EstiloAvatar => urlFoto is null ? null : $"background-image:url('{urlFoto}')";

        protected override async Task OnInitializedAsync()
        {
            // Se guarda la Task (no solo se espera) y se cascadea tal cual a Index y
            // ChangePassword: la consulta ocurre una sola vez aunque la esperen varios.
            cuentaTask = CargarCuentaAsync();
            await cuentaTask;
        }

        /// <summary>
        /// En un ambito de DI propio (mismo patron que App.razor) y no con el DbContext
        /// de la pagina: en Perfil, que es interactiva, ese DbContext dura todo el
        /// circuito y devolveria la copia que ya tenga rastreada (con un rol o una foto
        /// viejos); y en los dos modos, compartirlo con la pagina hija tumbaba el render
        /// con ConcurrencyDetector ("A second operation was started..."). La copia que se
        /// cascadea queda desprendida: las paginas solo la leen, y si necesitan guardar
        /// cargan una propia.
        /// </summary>
        private async Task<Usuario?> CargarCuentaAsync()
        {
            var estado = await EstadoAutenticacion;

            using var ambito = AmbitoServicios.CreateScope();
            var userManager = ambito.ServiceProvider.GetRequiredService<UserManager<Usuario>>();

            // Si la cuenta ya no existe, la propia pagina hija (Index/ChangePassword)
            // redirige a InvalidUser -- este layout solo se queda sin datos que
            // mostrar, no le corresponde a el decidir la redireccion.
            var cuenta = await userManager.GetUserAsync(estado.User);
            if (cuenta is null)
            {
                return null;
            }

            usuario = cuenta.UserName ?? string.Empty;
            nombre = cuenta.Nombre;
            iniciales = AvataresDefault.Iniciales(cuenta.Nombre);

            // La ruta guardada no se usa como URL: el archivo vive fuera de wwwroot y
            // solo se sirve por el endpoint, que ademas comprueba quien lo pide.
            if (!string.IsNullOrWhiteSpace(cuenta.RutaFotoPerfil))
            {
                urlFoto = $"/usuarios/{Uri.EscapeDataString(cuenta.Id)}/foto";
            }

            var roles = await userManager.GetRolesAsync(cuenta);
            rol = roles.FirstOrDefault() ?? "Sin rol";

            var claim = estado.User.FindFirst(ClaimsApp.UltimoAccesoAnterior);
            if (claim is not null
                && DateTime.TryParse(claim.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var fecha))
            {
                ultimaSesionAnterior = fecha;
            }

            return cuenta;
        }

        /// <summary>
        /// "Hoy, h:mm a.m./p.m.", "Ayer, ..." o "dd/MM/yyyy, ..." -- el mismo formato
        /// de hora acordado en Reposiciones/Usuarios, con el día relativo que hace que
        /// un acceso reciente se note de un vistazo.
        /// </summary>
        private static string FormatoUltimaSesion(DateTime fechaUtc)
        {
            var local = fechaUtc.ToLocalTime();
            var dia = local.Date == DateTime.Today ? "Hoy"
                : local.Date == DateTime.Today.AddDays(-1) ? "Ayer"
                : local.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

            return $"{dia}, {FormatoHora.HoraCorta(local)}";
        }
    }
}

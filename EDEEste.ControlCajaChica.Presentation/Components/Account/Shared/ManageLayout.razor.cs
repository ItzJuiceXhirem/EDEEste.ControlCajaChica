using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Infrastructure.Identity;
using EDEEste.ControlCajaChica.Presentation.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;

namespace EDEEste.ControlCajaChica.Presentation.Components.Account.Shared
{
    public partial class ManageLayout
    {
        [Inject] private UserManager<Usuario> UserManager { get; set; } = default!;

        [CascadingParameter]
        private HttpContext HttpContext { get; set; } = default!;

        private string usuario = string.Empty;
        private string nombre = string.Empty;
        private string rol = "Sin rol";
        private string iniciales = "?";
        private DateTime? ultimaSesionAnterior;
        private Task<Usuario?> cuentaTask = default!;
        private string? urlFoto;

        private bool TieneFoto => urlFoto is not null;

        /// <summary>
        /// La foto pisa el degradado dorado del avatar. Va como estilo en linea y no
        /// como clase porque la URL depende del usuario; el recorte al circulo lo hace
        /// el CSS (background-size: cover), sin reprocesar la imagen en el servidor.
        /// </summary>
        private string? EstiloAvatar => urlFoto is null ? null : $"background-image:url('{urlFoto}')";

        protected override async Task OnInitializedAsync()
        {
            // Se guarda la Task (no solo se espera) y se cascadea tal cual a Index y
            // ChangePassword: si cada pagina llamara GetUserAsync por su cuenta, Blazor
            // arranca la inicializacion del hijo sin esperar a que termine la de este
            // layout, y dos consultas a la vez sobre el mismo DbContext con scope de la
            // peticion tumban el render con ConcurrencyDetector ("A second operation
            // was started on this context instance..."). Esperar la MISMA Task desde
            // varios sitios es seguro -- la consulta real a la BDD ocurre una sola vez.
            cuentaTask = UserManager.GetUserAsync(HttpContext.User);

            // Si la cuenta ya no existe, la propia pagina hija (Index/ChangePassword)
            // redirige a InvalidUser -- este layout solo se queda sin datos que
            // mostrar, no le corresponde a el decidir la redireccion.
            var cuenta = await cuentaTask;
            if (cuenta is null)
            {
                return;
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

            var roles = await UserManager.GetRolesAsync(cuenta);
            rol = roles.FirstOrDefault() ?? "Sin rol";

            var claim = HttpContext.User.FindFirst(ClaimsApp.UltimoAccesoAnterior);
            if (claim is not null
                && DateTime.TryParse(claim.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var fecha))
            {
                ultimaSesionAnterior = fecha;
            }
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

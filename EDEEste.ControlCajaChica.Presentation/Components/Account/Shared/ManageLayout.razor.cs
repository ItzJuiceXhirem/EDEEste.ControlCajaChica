using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Infrastructure.Identity;
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

        protected override async Task OnInitializedAsync()
        {
            // Si la cuenta ya no existe, la propia pagina hija (Index/ChangePassword)
            // redirige a InvalidUser -- este layout solo se queda sin datos que
            // mostrar, no le corresponde a el decidir la redireccion.
            var cuenta = await UserManager.GetUserAsync(HttpContext.User);
            if (cuenta is null)
            {
                return;
            }

            usuario = cuenta.UserName ?? string.Empty;
            nombre = cuenta.Nombre;
            iniciales = Iniciales(cuenta.Nombre);

            var roles = await UserManager.GetRolesAsync(cuenta);
            rol = roles.FirstOrDefault() ?? "Sin rol";

            var claim = HttpContext.User.FindFirst(ClaimsApp.UltimoAccesoAnterior);
            if (claim is not null
                && DateTime.TryParse(claim.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var fecha))
            {
                ultimaSesionAnterior = fecha;
            }
        }

        private static string Iniciales(string nombreCompleto)
        {
            var partes = nombreCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return partes.Length switch
            {
                0 => "?",
                1 => partes[0][..1].ToUpperInvariant(),
                _ => (partes[0][..1] + partes[^1][..1]).ToUpperInvariant()
            };
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

            var sufijo = local.Hour < 12 ? "a.m." : "p.m.";
            return $"{dia}, {local.ToString("h:mm", CultureInfo.InvariantCulture)} {sufijo}";
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Entities;
using Microsoft.AspNetCore.Components;

namespace EDEEste.ControlCajaChica.Presentation.Components.Pages
{
    public partial class Home
    {
        private const decimal PorcentajeAlertaPorDefecto = 30m;

        [Inject]
        private IFondoRepository Fondos { get; set; } = default!;

        [Inject]
        private IIdentityService Identidad { get; set; } = default!;

        private IReadOnlyList<FondoCajaChica>? fondos;

        // CustodioId guarda el Id de Identity (un GUID), no un nombre: sin este mapa,
        // la tarjeta de cada fondo mostraria el GUID crudo en vez del nombre de usuario.
        private Dictionary<string, string> nombresDeCustodio = new();

        protected override async Task OnInitializedAsync()
        {
            fondos = await Fondos.ListarAsync();
            await ResolverNombresDeCustodioAsync();
        }

        private async Task ResolverNombresDeCustodioAsync()
        {
            var mapa = new Dictionary<string, string>();
            foreach (var id in fondos!.Select(f => f.CustodioId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct())
            {
                mapa[id] = await Identidad.ObtenerNombreUsuarioAsync(id) ?? id;
            }

            nombresDeCustodio = mapa;
        }

        private string NombreCustodio(string custodioId) =>
            string.IsNullOrWhiteSpace(custodioId) ? "(sin asignar)" : nombresDeCustodio.GetValueOrDefault(custodioId, custodioId);

        private static decimal PorcentajeAlerta(FondoCajaChica fondo) =>
            fondo.PorcentajeAlertaReposicion > 0 ? fondo.PorcentajeAlertaReposicion : PorcentajeAlertaPorDefecto;

        /// <summary>
        /// Semaforo del efectivo disponible: verde desde 50%, amarillo entre 30% y 49%,
        /// rojo en 29% o menos.
        ///
        /// Es a proposito independiente de PorcentajeAlertaReposicion: el color da una
        /// lectura uniforme de todos los fondos de un vistazo, mientras que el aviso de
        /// "toca solicitar reposicion" si respeta el umbral configurado de cada fondo.
        /// </summary>
        private static string ClaseBarra(decimal porcentaje) => porcentaje switch
        {
            >= 50m => "bg-success",
            >= 30m => "bg-warning",
            _ => "bg-danger"
        };
    }
}

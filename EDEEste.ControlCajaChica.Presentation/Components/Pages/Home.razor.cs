using System.Collections.Generic;
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

        private IReadOnlyList<FondoCajaChica>? fondos;

        protected override async Task OnInitializedAsync() => fondos = await Fondos.ListarAsync();

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

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Constants;
using EDEEste.ControlCajaChica.Domain.Entities;
using EDEEste.ControlCajaChica.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace EDEEste.ControlCajaChica.Presentation.Components.Pages
{
    public partial class Home
    {
        private const decimal PorcentajeAlertaPorDefecto = 30m;

        /// <summary>Cuántos arqueos entran en el anillo de resultados.</summary>
        private const int ArqueosEnElAnillo = 3;

        /// <summary>Separación en grados entre segmentos del anillo.</summary>
        private const double SeparacionSegmentos = 8d;

        private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-DO");

        // El degradado cónico se escribe dentro de un atributo style, así que sus
        // números tienen que llevar punto decimal pase lo que pase con la cultura
        // del servidor: con "93,4%" el navegador descarta la regla entera.
        private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

        [Inject]
        private IFondoRepository Fondos { get; set; } = default!;

        [Inject]
        private IIdentityService Identidad { get; set; } = default!;

        [Inject]
        private IArqueoRepository Arqueos { get; set; } = default!;

        [Inject]
        private ICurrentUserService UsuarioActual { get; set; } = default!;

        private IReadOnlyList<FondoCajaChica>? fondos;

        private IReadOnlyList<ArqueoCaja>? ultimosArqueos;

        // CustodioId guarda el Id de Identity (un GUID), no un nombre: sin este mapa,
        // la tarjeta de cada fondo mostraria el GUID crudo en vez del nombre de usuario.
        private Dictionary<string, string> nombresDeCustodio = new();

        /// <summary>
        /// El anillo de arqueos solo acompaña a un fondo único. Con varios fondos
        /// mezclaría en un mismo anillo arqueos de cajas distintas, que no se comparan
        /// entre sí.
        /// </summary>
        private bool EsPanelDeUnFondo => fondos is { Count: 1 };

        private string Lede => EsPanelDeUnFondo
            ? "Su fondo y el resultado de sus últimos arqueos."
            : "Fondos bajo su supervisión.";

        protected override async Task OnInitializedAsync()
        {
            // Un fondo inactivo ya no opera: no debe aparecer en el panel de nadie,
            // aunque el Administrador siga viéndolo (y pudiendo reactivarlo) en /fondos.
            var visibles = (await Fondos.ListarAsync()).Where(f => f.Estado != EstadoFondo.Inactivo);

            var usuario = await UsuarioActual.ObtenerAsync();
            if (usuario.Id is { } usuarioId && await Identidad.EstaEnRolAsync(usuarioId, RolesApp.Custodio))
            {
                // Un Custodio solo debe ver su propio fondo en el panel, no el balance
                // y los arqueos de los fondos de otros custodios.
                visibles = visibles.Where(f => f.CustodioId == usuarioId);
            }

            fondos = visibles.ToList();
            await ResolverNombresDeCustodioAsync();

            if (EsPanelDeUnFondo)
            {
                var arqueos = await Arqueos.ListarPorFondoAsync(fondos![0].Id);
                ultimosArqueos = arqueos
                    .OrderByDescending(a => a.FechaArqueo)
                    .Take(ArqueosEnElAnillo)
                    .ToList();
            }
        }

        private async Task ResolverNombresDeCustodioAsync()
        {
            var ids = fondos!.Select(f => f.CustodioId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            var resueltos = await Identidad.ObtenerNombresUsuarioAsync(ids);

            var mapa = new Dictionary<string, string>();
            foreach (var id in ids)
            {
                mapa[id] = resueltos.GetValueOrDefault(id, id);
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
        private static string ColorAnillo(decimal porcentaje) => porcentaje switch
        {
            >= 50m => "#198754",
            >= 30m => "#FFC107",
            _ => "#DC3545"
        };

        private static string ColorResultado(ResultadoArqueo resultado) => resultado switch
        {
            ResultadoArqueo.Cuadrado => "#198754",
            ResultadoArqueo.Sobrante => "#0DCAF0",
            _ => "#DC3545"
        };

        private static string EtiquetaResultado(ResultadoArqueo resultado) => resultado switch
        {
            ResultadoArqueo.Cuadrado => "Cuadrado",
            ResultadoArqueo.Sobrante => "Sobrante",
            _ => "Faltante"
        };

        /// <summary>
        /// Anillo de un segmento por arqueo, con una rendija del color del fondo entre
        /// segmentos para que dos resultados iguales seguidos no se lean como uno solo.
        /// Con un único arqueo no hay rendija: no habría nada que separar.
        /// </summary>
        private string GradienteArqueos()
        {
            var arqueos = ultimosArqueos!;
            var porcion = 360d / arqueos.Count;
            var separacion = arqueos.Count > 1 ? SeparacionSegmentos : 0d;

            var tramos = new StringBuilder("conic-gradient(");
            for (var i = 0; i < arqueos.Count; i++)
            {
                var inicio = i * porcion;
                var finColor = inicio + porcion - separacion;
                var finTramo = inicio + porcion;

                if (i > 0)
                {
                    tramos.Append(", ");
                }

                tramos.Append(Invariante, $"{ColorResultado(arqueos[i].Resultado)} {inicio:0.##}deg {finColor:0.##}deg");

                if (separacion > 0d)
                {
                    tramos.Append(Invariante, $", #FFFFFF {finColor:0.##}deg {finTramo:0.##}deg");
                }
            }

            return tramos.Append(')').ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace EDEEste.ControlCajaChica.Presentation.Components.Shared
{
    public partial class BarraPaginacion
    {
        // Cuántos números caben en una línea sin que la barra se parta.
        private const int MaximoBotones = 7;

        // Para el lector de pantalla: qué lista navega esta barra.
        [Parameter, EditorRequired]
        public string Etiqueta { get; set; } = string.Empty;

        // Base 1.
        [Parameter, EditorRequired]
        public int PaginaActual { get; set; }

        [Parameter, EditorRequired]
        public int TotalPaginas { get; set; }

        [Parameter, EditorRequired]
        public EventCallback<int> OnCambiarPagina { get; set; }

        /// <summary>
        /// Con un historial largo, pintar todos los números no cabe en una línea: se
        /// muestra una ventana centrada en la página actual, que se pega a los
        /// extremos cuando está cerca del principio o del final.
        /// </summary>
        private IEnumerable<int> Ventana
        {
            get
            {
                var total = Math.Max(1, TotalPaginas);
                var inicio = Math.Max(1, PaginaActual - MaximoBotones / 2);
                var fin = Math.Min(total, inicio + MaximoBotones - 1);
                inicio = Math.Max(1, fin - MaximoBotones + 1);

                return Enumerable.Range(inicio, fin - inicio + 1);
            }
        }

        private Task IrAAsync(int pagina) =>
            OnCambiarPagina.InvokeAsync(Math.Clamp(pagina, 1, Math.Max(1, TotalPaginas)));
    }
}

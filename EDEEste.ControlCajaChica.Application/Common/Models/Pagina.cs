using System;
using System.Collections.Generic;

namespace EDEEste.ControlCajaChica.Application.Common.Models
{
    /// <summary>
    /// Una página de resultados paginados en el servidor: los elementos de la página
    /// pedida y el total de todo el resultado, para que la pantalla pueda armar la
    /// barra de páginas sin traer todas las filas.
    /// </summary>
    public sealed class Pagina<T>
    {
        public IReadOnlyList<T> Elementos { get; init; } = Array.Empty<T>();
        public int Total { get; init; }
        public int NumeroPagina { get; init; } = 1;
        public int TamanoPagina { get; init; } = 1;

        // Al menos 1: un resultado vacío se muestra como una página vacía, no como ninguna.
        public int TotalPaginas => Math.Max(1, (int)Math.Ceiling(Total / (double)Math.Max(1, TamanoPagina)));
    }
}

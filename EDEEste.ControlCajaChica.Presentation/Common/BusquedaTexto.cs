using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace EDEEste.ControlCajaChica.Presentation.Common
{
    /// <summary>
    /// Comparacion de texto para filtros de busqueda que ignoran acentos --
    /// compartida por Categorias, Usuarios, Gastos y Reposiciones, que la tenian
    /// copiada identica cada una. Fondos.razor.cs NO usa este helper: su version
    /// tiene una guarda adicional para origen vacio que cambia el resultado, asi
    /// que no es la misma comparacion.
    /// </summary>
    public static class BusquedaTexto
    {
        public static bool ContieneSinAcentos(string texto, string busqueda) =>
            NormalizarParaBusqueda(texto).Contains(NormalizarParaBusqueda(busqueda), StringComparison.OrdinalIgnoreCase);

        private static string NormalizarParaBusqueda(string valor)
        {
            var normalizado = valor.Normalize(NormalizationForm.FormD);
            var sinDiacriticos = normalizado.Where(c =>
                CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);

            return new string(sinDiacriticos.ToArray()).Normalize(NormalizationForm.FormC);
        }
    }
}

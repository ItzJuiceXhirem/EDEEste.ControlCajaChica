using System;
using System.Collections.Generic;
using System.Linq;

namespace EDEEste.ControlCajaChica.Application.Common.Models
{
    /* Resultado de un caso de uso. Las reglas de negocio incumplidas (por ejemplo
       "el gasto supera el 2.5% del fondo") no son excepciones: son respuestas
       esperadas que la UI tiene que poder mostrar. Las excepciones se reservan para
       fallas reales de infraestructura. Sigue el mismo patron que ResultadoIdentidad. */
    public sealed record ResultadoOperacion<T>(bool Exitoso, T? Valor, IReadOnlyList<string> Errores)
    {
        public static ResultadoOperacion<T> Ok(T valor) =>
            new(true, valor, Array.Empty<string>());

        public static ResultadoOperacion<T> Fallo(IEnumerable<string> errores) =>
            new(false, default, errores.ToArray());

        public static ResultadoOperacion<T> Fallo(string error) =>
            new(false, default, new[] { error });
    }
}

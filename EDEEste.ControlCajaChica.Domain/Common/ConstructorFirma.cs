using System;
using System.Globalization;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Common
{
    /// <summary>
    /// Arma la cadena canónica que después se firma con HMAC.
    ///
    /// Tres reglas que hacen la firma confiable:
    ///  1. Cada campo se escribe como "longitud:valor". Así dos combinaciones
    ///     distintas nunca producen la misma cadena (sin esto, "AB"+"C" y "A"+"BC"
    ///     colisionan y un atacante podría mover texto de un campo a otro sin
    ///     romper la firma).
    ///  2. Todo se formatea con CultureInfo.InvariantCulture. Si no, el mismo monto
    ///     da "5000.00" o "5000,00" segun el locale del servidor y la firma dejaría
    ///     de validar al mover la app a otra máquina.
    ///  3. La cadena arranca con una versión de esquema. Si algún día hay que
    ///     cambiar qué campos se firman, se sube la versión y se distinguen las
    ///     firmas viejas de las nuevas en vez de invalidar toda la tabla en silencio.
    /// </summary>
    public sealed class ConstructorFirma
    {
        public const string VersionEsquema = "v1";

        private const string MarcaNulo = "~";

        private readonly StringBuilder _cadena = new();

        public ConstructorFirma(string tipoEntidad)
        {
          /* El tipo entra en la firma para que una fila no pueda copiarse de una
             tabla a otra conservando un hash válido. */
            Agregar(VersionEsquema);
            Agregar(tipoEntidad);
        }

        public ConstructorFirma Agregar(string? valor)
        {
            if (valor is null)
            {
                _cadena.Append(MarcaNulo).Append(':');
                return this;
            }

            _cadena.Append(valor.Length.ToString(CultureInfo.InvariantCulture))
                   .Append(':')
                   .Append(valor);
            return this;
        }

        public ConstructorFirma Agregar(Guid valor) => Agregar(valor.ToString("D", CultureInfo.InvariantCulture));

        public ConstructorFirma Agregar(Guid? valor) => valor.HasValue ? Agregar(valor.Value) : Agregar((string?)null);

        public ConstructorFirma Agregar(bool valor) => Agregar(valor ? "1" : "0");

        public ConstructorFirma Agregar(long valor) => Agregar(valor.ToString(CultureInfo.InvariantCulture));

      /* Los montos se normalizan a 4 decimales para que 5000, 5000.00 y 5000.0000
         (que es lo que devuelve la BDD) produzcan exactamente la misma cadena. */
        public ConstructorFirma Agregar(decimal valor) => Agregar(valor.ToString("F4", CultureInfo.InvariantCulture));

        public ConstructorFirma Agregar(DateTime valor) => Agregar((DateTime?)valor);

        /// <summary>
        /// SQL Server devuelve los datetime2 con Kind.Unspecified, mientras que en
        /// memoria los creamos con Kind.Utc. Se ignora el Kind y se formatea el
        /// instante tal cual para que escribir y releer den la misma cadena.
        /// Requiere que las columnas sean datetime2(7) (el default de EF Core): con
        /// menos precision la BDD trunca y la firma dejaria de validar al releer.
        /// </summary>
        public ConstructorFirma Agregar(DateTime? valor)
        {
            if (valor is null)
            {
                return Agregar((string?)null);
            }

            return Agregar(valor.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Agrega campos opcionales que se añadieron a una entidad DESPUÉS de que ya
        /// hubiera filas firmadas, sin invalidar esas firmas.
        ///
        /// Escribe los valores en orden hasta el último que tenga valor, y los nulos que
        /// quedan en medio llevan su marca de nulo. Los nulos del final no se escriben:
        /// una fila que no usa ninguno produce exactamente la misma cadena que antes de
        /// que existieran los campos.
        ///
        /// Por qué no basta con agregar cada campo solo si no es null: "hash=X,
        /// motivo=null" y "hash=null, motivo=X" darían la misma cadena, y alguien con
        /// acceso a la base podría mover un valor de un campo a otro sin romper la firma
        /// (por ejemplo, para apagar la verificación de un archivo). Escribiendo los
        /// huecos, la posición de cada valor forma parte de lo que se firma.
        ///
        /// Los campos nuevos solo pueden agregarse AL FINAL de la lista de una misma
        /// llamada, nunca en medio ni en otra posición.
        /// </summary>
        public ConstructorFirma AgregarOpcionalesAlFinal(params string?[] valores)
        {
            var ultimoConValor = Array.FindLastIndex(valores, valor => valor is not null);

            for (var i = 0; i <= ultimoConValor; i++)
            {
                Agregar(valores[i]);
            }

            return this;
        }

        public override string ToString() => _cadena.ToString();
    }
}

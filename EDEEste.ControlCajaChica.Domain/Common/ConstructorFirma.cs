using System;
using System.Globalization;
using System.Text;

namespace EDEEste.ControlCajaChica.Domain.Common
{
    /// <summary>
    /// Arma la cadena canonica que despues se firma con HMAC.
    ///
    /// Tres reglas que hacen la firma confiable:
    ///  1. Cada campo se escribe como "longitud:valor". Asi dos combinaciones
    ///     distintas nunca producen la misma cadena (sin esto, "AB"+"C" y "A"+"BC"
    ///     colisionan y un atacante podria mover texto de un campo a otro sin
    ///     romper la firma).
    ///  2. Todo se formatea con CultureInfo.InvariantCulture. Si no, el mismo monto
    ///     da "5000.00" o "5000,00" segun el locale del servidor y la firma dejaria
    ///     de validar al mover la app a otra maquina.
    ///  3. La cadena arranca con una version de esquema. Si algun dia hay que
    ///     cambiar que campos se firman, se sube la version y se distinguen las
    ///     firmas viejas de las nuevas en vez de invalidar toda la tabla en silencio.
    /// </summary>
    public sealed class ConstructorFirma
    {
        public const string VersionEsquema = "v1";

        private const string MarcaNulo = "~";

        private readonly StringBuilder _cadena = new();

        public ConstructorFirma(string tipoEntidad)
        {
            // El tipo entra en la firma para que una fila no pueda copiarse de una
            // tabla a otra conservando un hash valido.
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

        /// <summary>
        /// Los montos se normalizan a 4 decimales para que 5000, 5000.00 y 5000.0000
        /// (que es lo que devuelve la BDD) produzcan exactamente la misma cadena.
        /// </summary>
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

        public override string ToString() => _cadena.ToString();
    }
}

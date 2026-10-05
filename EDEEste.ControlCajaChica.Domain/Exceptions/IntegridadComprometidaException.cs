using System;

namespace EDEEste.ControlCajaChica.Domain.Exceptions
{
    /// <summary>
    /// Se lanza cuando un registro firmado llega a guardarse pero su HashFirma no
    /// corresponde a los datos que trae: alguien lo alteró por fuera de la aplicación.
    /// Cortar aquí evita que la app "lave" la manipulación recalculando la firma
    /// sobre los datos ya adulterados.
    /// </summary>
    public sealed class IntegridadComprometidaException : Exception
    {
        public IntegridadComprometidaException(string entidad, string registroId)
            : base($"El registro '{registroId}' de '{entidad}' fue alterado directamente en la base de datos " +
                   "sin autorización del programa. La operación fue bloqueada.")
        {
            Entidad = entidad;
            RegistroId = registroId;
        }

        public string Entidad { get; }

        public string RegistroId { get; }
    }
}

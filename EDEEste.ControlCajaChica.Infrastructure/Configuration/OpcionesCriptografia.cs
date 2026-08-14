using System;

namespace EDEEste.ControlCajaChica.Infrastructure.Configuration
{
    /// <summary>
    /// Clave con la que se firman las entidades y la bitacora. Todo el esquema
    /// anti-fraude depende de que esta clave NO este en la base de datos ni en el
    /// codigo fuente: si el DBA la tuviera, podria recalcular las firmas y la
    /// manipulacion seria indetectable.
    /// </summary>
    public sealed class OpcionesCriptografia
    {
        public const string Seccion = "Criptografia";

        /// <summary>
        /// Clave HMAC en Base64, de al menos 32 bytes (256 bits).
        /// Se lee de user-secrets, variables de entorno o Key Vault; nunca de un
        /// appsettings.json versionado.
        /// </summary>
        public string ClaveHmac { get; set; } = string.Empty;

        /// <summary>Tamano minimo aceptado para la clave, en bytes.</summary>
        public const int BytesMinimosClave = 32;
    }
}

using System;

namespace EDEEste.ControlCajaChica.Infrastructure.Configuration
{
    /// <summary>
    /// Clave con la que se firman las entidades y la bitácora. Todo el esquema
    /// anti-fraude depende de que esta clave NO esté en la base de datos ni en el
    /// código fuente: si el DBA la tuviera, podría recalcular las firmas y la
    /// manipulación sería indetectable.
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

        // Tamaño mínimo aceptado para la clave, en bytes
        public const int BytesMinimosClave = 32;
    }
}

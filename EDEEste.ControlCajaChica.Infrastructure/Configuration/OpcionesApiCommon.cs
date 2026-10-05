using System;

namespace EDEEste.ControlCajaChica.Infrastructure.Configuration
{
    /// <summary>
    /// Conexión con el APICommon de la empresa (directorio activo).
    ///
    /// La UrlBase sí puede vivir en appsettings.json (es un host interno, no un
    /// secreto), pero la ApiKey NO: va en user-secrets igual que la clave HMAC.
    /// </summary>
    public sealed class OpcionesApiCommon
    {
        public const string Seccion = "ApiCommon";

        // Ej: http://inapprueba/apicommon
        public string UrlBase { get; set; } = string.Empty;

        public string ApiKey { get; set; } = string.Empty;

        /* Ficha de un usuario por su nombre de usuario. Contrato conocido y ya
           implementado. */
        public const string RutaObtenerUsuario = "api/ActiveDirectory/GetUserByUserName";

        /* Validación de credenciales. Se conoce la ruta pero NO que recibe ni que
           devuelve, así que todavía no se puede implementar. */
        public const string RutaValidarCredenciales = "api/ActiveDirectory/ValidateCredentials";

        /* Cabecera con la que el APICommon espera la clave. Se deja configurable
           porque tampoco esta confirmado que sea exactamente esta. */
        public string NombreCabeceraApiKey { get; set; } = "X-Api-Key";
    }
}

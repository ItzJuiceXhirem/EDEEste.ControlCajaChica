using System;

namespace EDEEste.ControlCajaChica.Infrastructure.Configuration
{
    /// <summary>
    /// Conexion con el APICommon de la empresa (directorio activo).
    ///
    /// La UrlBase si puede vivir en appsettings.json (es un host interno, no un
    /// secreto), pero la ApiKey NO: va en user-secrets igual que la clave HMAC.
    /// </summary>
    public sealed class OpcionesApiCommon
    {
        public const string Seccion = "ApiCommon";

        /// <summary>Ej: http://inapprueba/apicommon</summary>
        public string UrlBase { get; set; } = string.Empty;

        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Ficha de un usuario por su nombre de usuario. Contrato conocido y ya
        /// implementado.
        /// </summary>
        public const string RutaObtenerUsuario = "api/ActiveDirectory/GetUserByUserName";

        /// <summary>
        /// Validacion de credenciales. Se conoce la ruta pero NO que recibe ni que
        /// devuelve, asi que todavia no se puede implementar.
        /// </summary>
        public const string RutaValidarCredenciales = "api/ActiveDirectory/ValidateCredentials";

        /// <summary>
        /// Cabecera con la que el APICommon espera la clave. Se deja configurable
        /// porque tampoco esta confirmado que sea exactamente esta.
        /// </summary>
        public string NombreCabeceraApiKey { get; set; } = "X-Api-Key";
    }
}

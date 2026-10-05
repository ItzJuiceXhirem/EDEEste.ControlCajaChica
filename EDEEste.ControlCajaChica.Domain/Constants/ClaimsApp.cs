namespace EDEEste.ControlCajaChica.Domain.Constants
{
    // claims propios de la aplicación (no los que ya trae ASP.NET Identity por default).
    public static class ClaimsApp
    {
        /// <summary>
        /// Se agrega al iniciar sesión, con la fecha/hora (UTC, formato "o") de la
        /// sesión ANTERIOR a esta -- nunca la de ahora mismo. Vive solo en la cookie de
        /// esta sesión, no en la base de datos: es lo que permite mostrar "Última
        /// sesión" en el perfil sin que, al refrescar la pagina, ese dato se convierta
        /// en el de la sesión que se está viendo (lo que le quitaría todo el sentido a
        /// "avisar de un acceso que no reconoce").
        /// </summary>
        public const string UltimoAccesoAnterior = "ultimo_acceso_anterior";
    }
}

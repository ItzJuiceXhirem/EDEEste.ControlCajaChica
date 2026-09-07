namespace EDEEste.ControlCajaChica.Domain.Constants
{
    /// <summary>
    /// Claims propios de la aplicacion (no los que ya trae ASP.NET Identity de fabrica).
    /// </summary>
    public static class ClaimsApp
    {
        /// <summary>
        /// Se agrega al iniciar sesion, con la fecha/hora (UTC, formato "o") de la
        /// sesion ANTERIOR a esta -- nunca la de ahora mismo. Vive solo en la cookie de
        /// esta sesion, no en la base de datos: es lo que permite mostrar "Ultima
        /// sesion" en el perfil sin que, al refrescar la pagina, ese dato se convierta
        /// en el de la sesion que se esta viendo (lo que le quitaria todo el sentido a
        /// "avisar de un acceso que no reconoce").
        /// </summary>
        public const string UltimoAccesoAnterior = "ultimo_acceso_anterior";
    }
}

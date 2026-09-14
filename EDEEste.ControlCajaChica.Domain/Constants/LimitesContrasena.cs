namespace EDEEste.ControlCajaChica.Domain.Constants
{
    /// <summary>
    /// Longitud aceptada para una contraseña, en un solo sitio.
    ///
    /// LongitudMinima la usan dos capas distintas que deben estar de acuerdo:
    /// Program.cs (Identity.Password.RequiredLength, la politica real que aplica
    /// el servidor) y los [StringLength] de los cuatro formularios (Register,
    /// ConfiguracionInicial, ChangePassword, ResetPassword), que solo dan
    /// retroalimentacion en el cliente. Si alguna vez se suben de forma
    /// independiente, un formulario podria aceptar como "valida" una contrasena
    /// que el servidor va a rechazar.
    ///
    /// LongitudMaxima no tiene equivalente en Identity (no impone tope): es una
    /// decision propia de este proyecto, tambien compartida por los cuatro
    /// formularios.
    /// </summary>
    public static class LimitesContrasena
    {
        public const int LongitudMinima = 8;
        public const int LongitudMaxima = 100;
    }
}

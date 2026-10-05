namespace EDEEste.ControlCajaChica.Domain.Constants
{
    /// <summary>
    /// Longitud aceptada para una contraseña, en un solo sitio.
    ///
    /// LongitudMinima la usan dos capas distintas que deben estar de acuerdo:
    /// Program.cs (Identity.Password.RequiredLength, la política real que aplica
    /// el servidor) y los [StringLength] de los cuatro formularios (Register,
    /// ConfiguracionInicial, ChangePassword, ResetPassword), que solo dan
    /// retroalimentación en el cliente. Si alguna vez se suben de forma
    /// independiente, un formulario podría aceptar como "válida" una contraseña
    /// que el servidor va a rechazar.
    ///
    /// LongitudMaxima no tiene equivalente en Identity (no impone tope): es una
    /// decisión propia de este proyecto, también compartida por los cuatro
    /// formularios.
    /// </summary>
    public static class LimitesContrasena
    {
        public const int LongitudMinima = 8;
        public const int LongitudMaxima = 100;
    }
}

namespace EDEEste.ControlCajaChica.Infrastructure.Configuration
{
    /// <summary>
    /// Controla quién puede usar /configuracion-inicial para crear el primer
    /// Administrador del sistema. Esa pantalla es pública por necesidad (nadie puede
    /// iniciar sesión todavía), así que sin este token, la primera persona que llegue
    /// a un despliegue nuevo -- no necesariamente su dueño -- se queda con el sistema
    /// completo.
    /// </summary>
    public sealed class OpcionesConfiguracionInicial
    {
        public const string Seccion = "ConfiguracionInicial";

        /// <summary>
        /// Token de un solo uso, válido solo mientras no exista ningún Administrador.
        /// Se lee de user-secrets, variables de entorno o Key Vault; nunca de un
        /// appsettings.json versionado. Si queda vacío, la pantalla se niega a
        /// mostrar el formulario en vez de dejar la creación abierta a cualquiera.
        /// </summary>
        public string TokenArranque { get; set; } = string.Empty;
    }
}

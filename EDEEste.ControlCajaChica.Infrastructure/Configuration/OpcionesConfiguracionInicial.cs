namespace EDEEste.ControlCajaChica.Infrastructure.Configuration
{
    /// <summary>
    /// Controla quien puede usar /configuracion-inicial para crear el primer
    /// Administrador del sistema. Esa pantalla es publica por necesidad (nadie puede
    /// iniciar sesion todavia), asi que sin este token, la primera persona que llegue
    /// a un despliegue nuevo -- no necesariamente su dueno -- se queda con el sistema
    /// completo.
    /// </summary>
    public sealed class OpcionesConfiguracionInicial
    {
        public const string Seccion = "ConfiguracionInicial";

        /// <summary>
        /// Token de un solo uso, valido solo mientras no exista ningun Administrador.
        /// Se lee de user-secrets, variables de entorno o Key Vault; nunca de un
        /// appsettings.json versionado. Si queda vacio, la pantalla se niega a
        /// mostrar el formulario en vez de dejar la creacion abierta a cualquiera.
        /// </summary>
        public string TokenArranque { get; set; } = string.Empty;
    }
}

namespace EDEEste.ControlCajaChica.Domain.Constants
{
    /// <summary>
    /// Limites de negocio sobre una categoria de gasto. Compartidos por tres sitios
    /// que deben estar de acuerdo: la validacion en CrearCategoriaGastoHandler y
    /// ActualizarCategoriaGastoHandler, y la columna real en ApplicationDbContext
    /// (HasMaxLength).
    /// </summary>
    public static class LimitesCategoriaGasto
    {
        public const int LongitudMaximaNombre = 100;
        public const int LongitudMaximaCuentaContable = 50;
    }
}

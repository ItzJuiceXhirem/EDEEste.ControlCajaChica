namespace EDEEste.ControlCajaChica.Domain.Constants
{
    /// <summary>
    /// Límites de negocio sobre una categoría de gasto. Compartidos por tres sitios
    /// que deben estar de acuerdo: la validación en CrearCategoriaGastoHandler y
    /// ActualizarCategoriaGastoHandler, y la columna real en ApplicationDbContext
    /// (HasMaxLength).
    /// </summary>
    public static class LimitesCategoriaGasto
    {
        public const int LongitudMaximaNombre = 100;
        public const int LongitudMaximaCuentaContable = 50;
    }
}

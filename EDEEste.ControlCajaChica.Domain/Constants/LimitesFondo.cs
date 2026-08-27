namespace EDEEste.ControlCajaChica.Domain.Constants
{
    /// <summary>
    /// Rangos permitidos para los parametros configurables de un fondo.
    ///
    /// Viven en Domain y no en cada handler porque son una regla de negocio, y
    /// porque los necesitan tres sitios a la vez: los dos handlers que validan
    /// (crear y actualizar) y la pantalla de Fondos, que dibuja estos rangos como
    /// escalas graficas. Con las cifras sueltas en cada lugar, cambiar un limite
    /// obligaba a acordarse de los tres.
    /// </summary>
    public static class LimitesFondo
    {
        /// <summary>
        /// Un gasto no puede consumir mas de este porcentaje del fondo fijo. El techo
        /// del 30% es deliberadamente bajo: por encima de eso, dos o tres gastos
        /// vaciarian la caja y la reposicion dejaria de tener sentido como ciclo.
        /// </summary>
        public const decimal TopePorGastoMinimo = 1m;

        public const decimal TopePorGastoMaximo = 30m;

        /// <summary>
        /// Con que porcentaje restante se habilita la reposicion. Por debajo del 10%
        /// el custodio se queda sin efectivo antes de poder pedir; por encima del 50%
        /// estaria reponiendo una caja que aun esta medio llena.
        /// </summary>
        public const decimal AlertaReposicionMinima = 10m;

        public const decimal AlertaReposicionMaxima = 50m;
    }
}

namespace EDEEste.ControlCajaChica.Domain.Constants
{
    /// <summary>
    /// Rangos permitidos para los parámetros configurables de un fondo.
    ///
    /// Viven en Domain y no en cada handler porque son una regla de negocio, y
    /// porque los necesitan tres sitios a la vez: los dos handlers que validan
    /// (crear y actualizar) y la pantalla de Fondos, que dibuja estos rangos como
    /// escalas gráficas. Con las cifras sueltas en cada lugar, cambiar un límite
    /// obligaba a acordarse de los tres.
    /// </summary>
    public static class LimitesFondo
    {
        /// <summary>
        /// Un gasto no puede consumir más de este porcentaje del fondo fijo. El techo
        /// del 30% es deliberadamente bajo: por encima de eso, dos o tres gastos
        /// vaciarían la caja y la reposición dejaría de tener sentido como ciclo.
        /// </summary>
        public const decimal TopePorGastoMinimo = 1m;

        public const decimal TopePorGastoMaximo = 30m;

        /// <summary>
        /// Valor con el que arranca el campo al crear un fondo nuevo, si el
        /// Administrador no lo cambia. El README lo fija como punto de partida, pero
        /// sigue siendo editable por fondo dentro del rango
        /// [<see cref="TopePorGastoMinimo"/>, <see cref="TopePorGastoMaximo"/>].
        /// </summary>
        public const decimal TopePorGastoPorDefecto = 2.5m;

        /// <summary>
        /// Con qué porcentaje restante se habilita la reposición. Por debajo del 10%
        /// el custodio se queda sin efectivo antes de poder pedir; por encima del 50%
        /// estaria reponiendo una caja que aún está medio llena.
        /// </summary>
        public const decimal AlertaReposicionMinima = 10m;

        public const decimal AlertaReposicionMaxima = 50m;

        /// <summary>
        /// Umbral que se usa cuando un fondo no trae su propio
        /// PorcentajeAlertaReposicion configurado (o vale 0): tanto para prellenar el
        /// campo al crear un fondo nuevo, como al decidir si ya toca habilitar la
        /// reposición sobre un fondo existente. El README pide solicitarla cuando el
        /// fondo restante cae en la banda 30%-20%, así que el default coincide con el
        /// borde superior de esa banda.
        ///
        /// Coincide numéricamente con <see cref="TopePorGastoMaximo"/> (30), pero son
        /// dos reglas de negocio distintas sin relación entre sí -- una es el tope de
        /// cuánto puede costar un solo gasto, esta es el umbral de cuánto debe quedar
        /// en caja. No los unifiques solo porque hoy comparten valor.
        /// </summary>
        public const decimal AlertaReposicionPorDefecto = 30m;
    }
}

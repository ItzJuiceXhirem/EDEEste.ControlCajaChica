namespace EDEEste.ControlCajaChica.Domain.Constants
{
    /// <summary>
    /// Límites de negocio sobre un gasto y sus comprobantes.
    ///
    /// Vive en Domain y no en el handler porque lo necesitan dos capas a la vez: la
    /// validación real en Application (RegistrarGastoHandler.Validar) y la pantalla en
    /// Presentation, que debe mostrar un error amigable ANTES de intentar subir más
    /// comprobantes de los que el servidor va a aceptar. Con la cifra suelta en cada
    /// lado, cambiar el límite obligaba a acordarse de los dos sitios.
    /// </summary>
    public static class LimitesGasto
    {
        /// <summary>
        /// Cuántos comprobantes admite un solo gasto. Antes vivía solo del lado del
        /// cliente (InputFileChangeEventArgs.GetMultipleFiles(maximumFileCount: 20),
        /// que además lanzaba una excepción si se superaba -- sin captura, eso tumbaba
        /// el circuito de Blazor Server entero). Ahora la regla real esta aquí: una
        /// lista, un comando, una transacción, sin la carrera que tendría contar
        /// archivos en una carpeta de staging.
        /// </summary>
        public const int ComprobantesPorGasto = 20;

        /// <summary>
        /// Tope de Gasto.MotivoAnulacion. Compartido por tres sitios que deben estar de
        /// acuerdo: la validación en SolicitarAnulacionGastoHandler (Custodio pide la
        /// anulación) y en AnularGastoHandler (Gerente la confirma), y la columna real
        /// en ApplicationDbContext (HasMaxLength). El valor iguala a propósito el tope
        /// de Gasto.Concepto -- mismo tenor de texto libre -- pero es una constante
        /// propia, no la misma: no la confundas si Concepto cambia de cota algún día.
        /// </summary>
        public const int LongitudMaximaMotivoAnulacion = 500;
    }
}

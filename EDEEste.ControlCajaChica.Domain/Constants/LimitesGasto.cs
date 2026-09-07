namespace EDEEste.ControlCajaChica.Domain.Constants
{
    /// <summary>
    /// Limites de negocio sobre un gasto y sus comprobantes.
    ///
    /// Vive en Domain y no en el handler porque lo necesitan dos capas a la vez: la
    /// validacion real en Application (RegistrarGastoHandler.Validar) y la pantalla en
    /// Presentation, que debe mostrar un error amigable ANTES de intentar subir mas
    /// comprobantes de los que el servidor va a aceptar. Con la cifra suelta en cada
    /// lado, cambiar el limite obligaba a acordarse de los dos sitios.
    /// </summary>
    public static class LimitesGasto
    {
        /// <summary>
        /// Cuantos comprobantes admite un solo gasto. Antes vivia solo del lado del
        /// cliente (InputFileChangeEventArgs.GetMultipleFiles(maximumFileCount: 20),
        /// que ademas lanzaba una excepcion si se superaba -- sin captura, eso tumbaba
        /// el circuito de Blazor Server entero). Ahora la regla real esta aqui: una
        /// lista, un comando, una transaccion, sin la carrera que tendria contar
        /// archivos en una carpeta de staging.
        /// </summary>
        public const int ComprobantesPorGasto = 20;

        /// <summary>
        /// Tope de Gasto.MotivoAnulacion. Compartido por tres sitios que deben estar de
        /// acuerdo: la validacion en SolicitarAnulacionGastoHandler (Custodio pide la
        /// anulacion) y en AnularGastoHandler (Gerente la confirma), y la columna real
        /// en ApplicationDbContext (HasMaxLength). El valor iguala a proposito el tope
        /// de Gasto.Concepto -- mismo tenor de texto libre -- pero es una constante
        /// propia, no la misma: no la confundas si Concepto cambia de cota algun dia.
        /// </summary>
        public const int LongitudMaximaMotivoAnulacion = 500;
    }
}

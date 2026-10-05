namespace EDEEste.ControlCajaChica.Domain.Constants
{
    /// <summary>
    /// Límites de texto de una solicitud de reposición.
    ///
    /// Cada tope lo comparten la validación del handler y la columna real en
    /// ApplicationDbContext (HasMaxLength): con la cifra suelta en cada lado, cambiarla
    /// obligaba a acordarse de los dos sitios.
    /// </summary>
    public static class LimitesReposicion
    {
        /// <summary>
        /// Tope de los tres motivos de una solicitud (devolución de Finanzas, nueva
        /// aprobación del Gerente y rechazo). Es una constante propia y no
        /// <see cref="LimitesGasto.LongitudMaximaMotivoAnulacion"/>: tienen el mismo
        /// valor porque son el mismo tenor de texto libre, no porque deban cambiar
        /// juntos.
        /// </summary>
        public const int LongitudMaximaMotivo = 500;

        // Referencia del pago que Finanzas digita al registrarlo (número de
        // transferencia o cheque).
        public const int LongitudMaximaReferenciaPago = 100;
    }
}

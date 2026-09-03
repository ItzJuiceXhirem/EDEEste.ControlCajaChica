using System;

namespace EDEEste.ControlCajaChica.Application.DTOs
{
    /// <summary>
    /// Manifiesto que acompaña un archivo en staging: los metadatos en los que el
    /// handler puede confiar cuando promueve el comprobante a su ubicacion final.
    ///
    /// Firmado con HMAC (Firma) sobre UsuarioId + Referencia + NombreOriginal +
    /// Extension. El UsuarioId entra en la firma aunque la carpeta ya este separada
    /// por usuario: es defensa en profundidad -- si algun dia un manifiesto
    /// terminara copiado a la carpeta de otro usuario, la firma deja de validar en
    /// vez de que la unica proteccion sea la ruta.
    /// </summary>
    public sealed class ComprobanteStagingDto
    {
        public Guid Referencia { get; set; }
        public string UsuarioId { get; set; } = string.Empty;
        public string NombreOriginal { get; set; } = string.Empty;

        /// <summary>Extension en minusculas con el punto, ej. ".pdf". La eligio el
        /// servidor al subir, nunca el nombre que declaro el cliente.</summary>
        public string Extension { get; set; } = string.Empty;

        public DateTime FechaSubida { get; set; }
        public string Firma { get; set; } = string.Empty;
    }

    /// <summary>
    /// Metadatos ya confiables de un comprobante recien promovido de staging a su
    /// ubicacion final. TipoMime, TamanoBytes y HashSha256 se derivan del archivo
    /// ya copiado (nunca de lo que declaro el cliente al subir), para que quien
    /// arma la entidad ComprobanteAdjunto no tenga que volver a tocar disco.
    /// </summary>
    public sealed class ComprobantePromovidoDto
    {
        public string RutaRelativa { get; set; } = string.Empty;
        public string NombreOriginal { get; set; } = string.Empty;
        public string TipoMime { get; set; } = string.Empty;
        public long TamanoBytes { get; set; }
        public string HashSha256 { get; set; } = string.Empty;
    }
}

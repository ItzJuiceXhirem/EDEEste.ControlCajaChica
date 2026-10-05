using System;

namespace EDEEste.ControlCajaChica.Application.DTOs
{
    /// <summary>
    /// Un archivo leido de disco junto con el resultado de verificar su hash sobre
    /// esos mismos bytes. Quien lo recibe sirve o usa Contenido, nunca vuelve a leer
    /// el archivo: entre dos lecturas podria cambiar y se usaria uno no verificado.
    /// </summary>
    public sealed class ArchivoVerificadoDto
    {
        public byte[] Contenido { get; init; } = Array.Empty<byte>();
        public IntegridadArchivo Integridad { get; init; }
    }
}

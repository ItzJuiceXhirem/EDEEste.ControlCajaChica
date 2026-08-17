using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace EDEEste.ControlCajaChica.Application.DTOs
{
    public class SubirComprobanteDto
    {
        public string NombreOriginal { get; set; } = string.Empty;
        public string TipoMime { get; set; } = string.Empty;
        public long TamanoBytes { get; set; }

        // Se utiliza Stream para manejar archivos grandes sin saturar la RAM del servidor
        public Stream ContenidoArchivo { get; set; } = Stream.Null;
    }
}

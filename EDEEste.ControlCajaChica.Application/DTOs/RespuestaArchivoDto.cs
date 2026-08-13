using System;
using System.Collections.Generic;
using System.Text;

namespace EDEEste.ControlCajaChica.Application.DTOs
{
    public class RespuestaArchivoDto
    {
        public string RutaRelativa { get; set; } = string.Empty;
        public string HashSha256 { get; set; } = string.Empty;
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.DTOs;

namespace EDEEste.ControlCajaChica.Application.Common.Interfaces
{
    public interface IFileStorageService
    {
        Task<RespuestaArchivoDto> GuardarComprobanteAsync(SubirComprobanteDto comprobanteDto);
        Task EliminarArchivoAsync(string rutaRelativa);
        Task<bool> VerificarIntegridadArchivoAsync(string rutaRelativa, string hashOriginal);
    }
}

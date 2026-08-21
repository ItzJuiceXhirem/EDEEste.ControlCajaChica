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

        /// <summary>
        /// Guarda el expediente PDF ya generado de una reposicion. Va en una carpeta
        /// aparte de los comprobantes porque no es un archivo que subio un usuario:
        /// lo produce el sistema y se puede regenerar.
        /// </summary>
        Task<RespuestaArchivoDto> GuardarPdfConsolidadoAsync(byte[] contenido, string nombreArchivo);

        /// <summary>Lee un archivo ya guardado (por ejemplo, para descargarlo).</summary>
        Task<byte[]?> LeerArchivoAsync(string rutaRelativa);

        /// <summary>Resuelve la ruta relativa guardada en BDD a una ruta fisica en disco.</summary>
        string ObtenerRutaFisica(string rutaRelativa);
    }
}

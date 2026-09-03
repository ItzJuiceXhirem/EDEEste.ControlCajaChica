using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
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

        /// <summary>
        /// Guarda un archivo recien subido en staging, bajo la carpeta del usuario
        /// dado (nunca la de otro), junto con su manifiesto firmado. Devuelve la
        /// referencia con la que despues se pide promoverlo o descartarlo.
        /// </summary>
        Task<Guid> GuardarComprobanteEnStagingAsync(
            string usuarioId,
            string nombreOriginal,
            string extension,
            Stream contenido,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lee y valida el manifiesto de un archivo en staging del usuario dado.
        /// Null si no existe, si la firma no coincide, o si el archivo o el
        /// manifiesto que le corresponde ya no estan los dos presentes.
        /// </summary>
        Task<ComprobanteStagingDto?> LeerManifiestoStagingAsync(
            string usuarioId,
            Guid referencia,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Copia (nunca mueve) el archivo de staging a su ubicacion final y devuelve
        /// los metadatos ya confiables para construir la entidad. No borra el
        /// original -- eso es responsabilidad de quien llama, y solo despues de que
        /// el guardado en BDD haya tenido exito.
        /// </summary>
        Task<ComprobantePromovidoDto> PromoverComprobanteAsync(
            string usuarioId,
            ComprobanteStagingDto manifiesto,
            CancellationToken cancellationToken = default);

        /// <summary>Borra el archivo y el manifiesto de staging del usuario dado, si existen.</summary>
        Task EliminarStagingAsync(
            string usuarioId,
            Guid referencia,
            CancellationToken cancellationToken = default);

        /// <summary>Archivos y bytes que el usuario tiene hoy en staging, para la cuota anti-DoS.</summary>
        Task<(int Archivos, long Bytes)> ContarStagingAsync(
            string usuarioId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Borra del staging del usuario dado todo archivo (y su manifiesto) con mas
        /// de "antiguedad" desde su ultima escritura. Pensado para llamarse en cada
        /// subida (barrido oportunista): es el mecanismo real bajo IIS, donde el app
        /// pool se duerme y un servicio de fondo puede no llegar a correr nunca.
        /// </summary>
        Task LimpiarStagingDelUsuarioAsync(
            string usuarioId,
            TimeSpan antiguedad,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Igual que <see cref="LimpiarStagingDelUsuarioAsync"/> pero sobre TODAS las
        /// carpetas de usuario en staging. Es el respaldo global (ver
        /// LimpiezaStagingBackgroundService), para el usuario que sube una vez y no
        /// vuelve a generar un barrido oportunista. Devuelve cuantos archivos borro.
        /// </summary>
        Task<int> LimpiarStagingAbandonadoAsync(
            TimeSpan antiguedad,
            CancellationToken cancellationToken = default);
    }
}

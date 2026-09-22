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

      /* Guarda el expediente PDF ya generado de una reposición. Va en una carpeta
         aparte de los comprobantes porque no es un archivo que subió un usuario:
         lo produce el sistema y se puede regenerar. */
        Task<RespuestaArchivoDto> GuardarPdfConsolidadoAsync(byte[] contenido, string nombreArchivo);

        // lee un archivo ya guardado (por ejemplo, para descargarlo)
        Task<byte[]?> LeerArchivoAsync(string rutaRelativa);

        // resuelve la ruta relativa guardada en BDD a una ruta física en disco
        string ObtenerRutaFisica(string rutaRelativa);

      /* Guarda un archivo recién subido en staging, bajo la carpeta del usuario
         dado (nunca la de otro), junto con su manifiesto firmado. Devuelve la
         referencia con la que despues se pide promoverlo o descartarlo. */
        Task<Guid> GuardarComprobanteEnStagingAsync(
            string usuarioId,
            string nombreOriginal,
            string extension,
            Stream contenido,
            CancellationToken cancellationToken = default);

      /* Lee y valida el manifiesto de un archivo en staging del usuario dado.
         Null si no existe, si la firma no coincide, o si el archivo o el
         manifiesto que le corresponde ya no están los dos presentes. */
        Task<ComprobanteStagingDto?> LeerManifiestoStagingAsync(
            string usuarioId,
            Guid referencia,
            CancellationToken cancellationToken = default);

      /* Copia (nunca mueve) el archivo de staging a su ubicación final y devuelve
         los metadatos ya confiables para construir la entidad. No borra el
         original -- eso es responsabilidad de quien llama, y solo después de que
         el guardado en BDD haya tenido éxito. */
        Task<ComprobantePromovidoDto> PromoverComprobanteAsync(
            string usuarioId,
            ComprobanteStagingDto manifiesto,
            CancellationToken cancellationToken = default);

        // Borra el archivo y el manifiesto de staging del usuario dado, si existen
        Task EliminarStagingAsync(
            string usuarioId,
            Guid referencia,
            CancellationToken cancellationToken = default);

        // Archivos y bytes que el usuario tiene hoy en staging, para la cuota anti-DoS
        Task<(int Archivos, long Bytes)> ContarStagingAsync(
            string usuarioId,
            CancellationToken cancellationToken = default);

      /* Borra del staging del usuario dado todo archivo (y su manifiesto) con más
         de "antiguedad" desde su última escritura. Pensado para llamarse en cada
         subida (barrido oportunista): es el mecanismo real bajo IIS, donde el app
         pool se duerme y un servicio de fondo puede no llegar a correr nunca. */
        Task LimpiarStagingDelUsuarioAsync(
            string usuarioId,
            TimeSpan antiguedad,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Igual que <see cref="LimpiarStagingDelUsuarioAsync"/> pero sobre TODAS las
        /// carpetas de usuario en staging. Es el respaldo global (ver
        /// LimpiezaStagingBackgroundService), para el usuario que sube una vez y no
        /// vuelve a generar un barrido oportunista. Devuelve cuantos archivos borró.
        /// </summary>
        Task<int> LimpiarStagingAbandonadoAsync(
            TimeSpan antiguedad,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Guarda (o reemplaza) la foto de perfil del usuario y devuelve su ruta
        /// relativa, para guardarla en la fila del usuario.
        ///
        /// No pasa por staging, a diferencia de un comprobante: aquel existe porque el
        /// gasto se confirma en una transacción aparte que puede fallar por
        /// concurrencia, y hasta entonces el archivo no tiene dueño. Una foto de perfil
        /// se escribe y se referencia en el acto, sobre la propia fila del usuario.
        ///
        /// El archivo anterior NO se borra aqui: si la extensión cambia, el nombre
        /// cambia, y quien llama (que es quien conoce la ruta anterior guardada en la
        /// BDD) decide cuándo borrarlo -- después de actualizar la fila, nunca antes,
        /// para no dejar una fila apuntando a un archivo que ya no existe.
        /// </summary>
        Task<string> GuardarFotoPerfilAsync(
            string usuarioId,
            string extension,
            Stream contenido,
            CancellationToken cancellationToken = default);
    }
}

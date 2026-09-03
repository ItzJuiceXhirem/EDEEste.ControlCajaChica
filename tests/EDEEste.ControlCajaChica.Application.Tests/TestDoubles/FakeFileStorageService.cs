using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.DTOs;

namespace EDEEste.ControlCajaChica.Application.Tests.TestDoubles
{
    public sealed class FakeFileStorageService : IFileStorageService
    {
        // Mismas instancias que devolvio GuardarComprobanteEnStagingAsync: es lo que
        // permite que PromoverComprobanteAsync devuelva datos consistentes con lo que
        // el propio handler subio, igual que el DbContext compartido del resto de los
        // fakes (ver AGENTS.md sobre por que no se usa una libreria de mocking aqui).
        private readonly Dictionary<Guid, ComprobanteStagingDto> _staging = new();

        // El handler real relee y vuelve a verificar la firma del archivo YA
        // promovido (defensa en profundidad contra una manipulacion entre la subida
        // y el guardado del gasto). Para que ese chequeo pase en las pruebas, cada
        // ruta final "promovida" guarda aqui una firma que coincide con su MIME --
        // sin esto, RegistrarGastoHandlerTests fallaria siempre en esa verificacion.
        private readonly Dictionary<string, byte[]> _archivosFinales = new();

        public Task<RespuestaArchivoDto> GuardarComprobanteAsync(SubirComprobanteDto comprobanteDto) =>
            Task.FromResult(new RespuestaArchivoDto
            {
                RutaRelativa = $"uploads/{comprobanteDto.NombreOriginal}",
                HashSha256 = "hash-de-prueba"
            });

        public Task EliminarArchivoAsync(string rutaRelativa) => Task.CompletedTask;

        public Task<bool> VerificarIntegridadArchivoAsync(string rutaRelativa, string hashOriginal) =>
            Task.FromResult(true);

        public Task<RespuestaArchivoDto> GuardarPdfConsolidadoAsync(byte[] contenido, string nombreArchivo) =>
            Task.FromResult(new RespuestaArchivoDto
            {
                RutaRelativa = $"reposiciones/{nombreArchivo}",
                HashSha256 = "hash-de-prueba"
            });

        public Task<byte[]?> LeerArchivoAsync(string rutaRelativa) =>
            Task.FromResult<byte[]?>(_archivosFinales.TryGetValue(rutaRelativa, out var bytes) ? bytes : []);

        public string ObtenerRutaFisica(string rutaRelativa) => rutaRelativa;

        public Task<Guid> GuardarComprobanteEnStagingAsync(
            string usuarioId,
            string nombreOriginal,
            string extension,
            Stream contenido,
            CancellationToken cancellationToken = default)
        {
            var referencia = Guid.NewGuid();
            _staging[referencia] = new ComprobanteStagingDto
            {
                Referencia = referencia,
                UsuarioId = usuarioId,
                NombreOriginal = nombreOriginal,
                Extension = extension,
                FechaSubida = DateTime.UtcNow,
                Firma = "firma-de-prueba"
            };

            return Task.FromResult(referencia);
        }

        public Task<ComprobanteStagingDto?> LeerManifiestoStagingAsync(
            string usuarioId,
            Guid referencia,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                _staging.TryGetValue(referencia, out var manifiesto) && manifiesto.UsuarioId == usuarioId
                    ? manifiesto
                    : null);

        public Task<ComprobantePromovidoDto> PromoverComprobanteAsync(
            string usuarioId,
            ComprobanteStagingDto manifiesto,
            CancellationToken cancellationToken = default)
        {
            var tipoMime = TipoMimePorExtension(manifiesto.Extension);
            var rutaRelativa = $"uploads/comprobantes/{Guid.NewGuid()}{manifiesto.Extension}";

            _archivosFinales[rutaRelativa] = FirmaDe(tipoMime);

            return Task.FromResult(new ComprobantePromovidoDto
            {
                RutaRelativa = rutaRelativa,
                NombreOriginal = manifiesto.NombreOriginal,
                TipoMime = tipoMime,
                TamanoBytes = _archivosFinales[rutaRelativa].Length,
                HashSha256 = "hash-de-prueba"
            });
        }

        public Task EliminarStagingAsync(
            string usuarioId,
            Guid referencia,
            CancellationToken cancellationToken = default)
        {
            _staging.Remove(referencia);
            return Task.CompletedTask;
        }

        public Task<(int Archivos, long Bytes)> ContarStagingAsync(
            string usuarioId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult((_staging.Values.Count(m => m.UsuarioId == usuarioId), 0L));

        // Sin implementacion real: ningun escenario de prueba de Application deja
        // pasar tiempo suficiente como para que "abandonado" tenga sentido -- lo que
        // se prueba aqui es la logica de negocio del handler, no el barrido en si
        // (ese vive en Infrastructure, fuera del alcance de este proyecto de pruebas).
        public Task LimpiarStagingDelUsuarioAsync(
            string usuarioId,
            TimeSpan antiguedad,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<int> LimpiarStagingAbandonadoAsync(
            TimeSpan antiguedad,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        private static string TipoMimePorExtension(string extension) => extension.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };

        private static byte[] FirmaDe(string tipoMime) => tipoMime switch
        {
            "application/pdf" => "%PDF-1.4"u8.ToArray(),
            "image/png" => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
            "image/jpeg" => [0xFF, 0xD8, 0xFF],
            _ => []
        };
    }
}

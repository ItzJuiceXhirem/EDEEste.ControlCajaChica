using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.DTOs;

namespace EDEEste.ControlCajaChica.Application.Tests.TestDoubles
{
    public sealed class FakeFileStorageService : IFileStorageService
    {
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
            Task.FromResult<byte[]?>([]);

        public string ObtenerRutaFisica(string rutaRelativa) => rutaRelativa;
    }
}

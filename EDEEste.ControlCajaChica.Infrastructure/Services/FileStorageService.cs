using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.DTOs;

namespace EDEEste.ControlCajaChica.Infrastructure.Services
{
    public class FileStorageService : IFileStorageService
    {
        // Ruta base donde se guardarán los archivos (se inyectaría por configuración en producción)
        private readonly string _baseStoragePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "comprobantes");

        public FileStorageService()
        {
            if (!Directory.Exists(_baseStoragePath))
            {
                Directory.CreateDirectory(_baseStoragePath);
            }
        }

        public string ObtenerRutaFisica(string rutaRelativa) =>
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", rutaRelativa);

        public Task EliminarArchivoAsync(string rutaRelativa)
        {
            var rutaFisica = ObtenerRutaFisica(rutaRelativa);
            if (File.Exists(rutaFisica))
            {
                File.Delete(rutaFisica);
            }
            return Task.CompletedTask;
        }

        public async Task<RespuestaArchivoDto> GuardarComprobanteAsync(SubirComprobanteDto comprobanteDto)
        {
            // Seguridad: Generar un nombre único para evitar sobreescrituras y Path Traversal Attacks
            var extension = Path.GetExtension(comprobanteDto.NombreOriginal);
            var nombreArchivoSeguro = $"{Guid.NewGuid()}{extension}";
            var rutaCompleta = Path.Combine(_baseStoragePath, nombreArchivoSeguro);

            // Se usa un FileStream para escribir el archivo directamente en disco chunk por chunk
            using (var fileStream = new FileStream(rutaCompleta, FileMode.Create))
            {
                await comprobanteDto.ContenidoArchivo.CopyToAsync(fileStream);
            }

            // Calcular el Hash SHA-256 físico del archivo guardado
            var hashCalculado = await CalcularHashArchivoAsync(rutaCompleta);

            return new RespuestaArchivoDto
            {
                // Devolvemos la ruta relativa para guardarla en BDD, ej: "uploads/comprobantes/uuid.pdf"
                RutaRelativa = Path.Combine("uploads", "comprobantes", nombreArchivoSeguro).Replace("\\", "/"),
                HashSha256 = hashCalculado
            };
        }

        public async Task<bool> VerificarIntegridadArchivoAsync(string rutaRelativa, string hashOriginal)
        {
            var rutaFisica = ObtenerRutaFisica(rutaRelativa);
            if (!File.Exists(rutaFisica)) return false;

            var hashActual = await CalcularHashArchivoAsync(rutaFisica);
            return hashActual == hashOriginal;
        }


        // Método privado para calcular el hash criptográfico del archivo físico
        private async Task<string> CalcularHashArchivoAsync(string rutaFisica)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(rutaFisica);
            var hashBytes = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hashBytes);
        }
    }
}

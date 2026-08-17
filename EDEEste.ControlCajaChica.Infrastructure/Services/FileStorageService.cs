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
        private const string CarpetaComprobantes = "comprobantes";
        private const string CarpetaReposiciones = "reposiciones";

        // Raiz de todo lo que sube o genera la aplicacion (se inyectaría por configuración en producción)
        private readonly string _rutaRaiz = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        public FileStorageService()
        {
            Directory.CreateDirectory(Path.Combine(_rutaRaiz, "uploads", CarpetaComprobantes));
            Directory.CreateDirectory(Path.Combine(_rutaRaiz, "uploads", CarpetaReposiciones));
        }

        /// <summary>
        /// Resuelve la ruta relativa guardada en BDD a una ruta fisica en disco.
        ///
        /// Se valida que el resultado siga colgando de wwwroot: si una ruta con ".."
        /// llegara desde la BDD (fila manipulada, migracion de datos vieja), sin esta
        /// comprobacion se podria leer o borrar cualquier archivo del servidor.
        /// </summary>
        public string ObtenerRutaFisica(string rutaRelativa)
        {
            var rutaCompleta = Path.GetFullPath(Path.Combine(_rutaRaiz, rutaRelativa));
            var raiz = Path.GetFullPath(_rutaRaiz) + Path.DirectorySeparatorChar;

            if (!rutaCompleta.StartsWith(raiz, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"La ruta '{rutaRelativa}' apunta fuera del directorio de archivos de la aplicacion.");
            }

            return rutaCompleta;
        }

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
            var rutaCompleta = Path.Combine(_rutaRaiz, "uploads", CarpetaComprobantes, nombreArchivoSeguro);

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
                RutaRelativa = ConstruirRutaRelativa(CarpetaComprobantes, nombreArchivoSeguro),
                HashSha256 = hashCalculado
            };
        }

        public async Task<RespuestaArchivoDto> GuardarPdfConsolidadoAsync(byte[] contenido, string nombreArchivo)
        {
            // El nombre viene armado por la aplicacion, pero se le quita cualquier
            // componente de ruta por si acaso: solo interesa el nombre del archivo.
            var nombreSeguro = Path.GetFileName(nombreArchivo);
            var rutaCompleta = Path.Combine(_rutaRaiz, "uploads", CarpetaReposiciones, nombreSeguro);

            await File.WriteAllBytesAsync(rutaCompleta, contenido);

            return new RespuestaArchivoDto
            {
                RutaRelativa = ConstruirRutaRelativa(CarpetaReposiciones, nombreSeguro),
                HashSha256 = await CalcularHashArchivoAsync(rutaCompleta)
            };
        }

        public async Task<byte[]?> LeerArchivoAsync(string rutaRelativa)
        {
            var rutaFisica = ObtenerRutaFisica(rutaRelativa);
            if (!File.Exists(rutaFisica))
            {
                return null;
            }

            return await File.ReadAllBytesAsync(rutaFisica);
        }

        public async Task<bool> VerificarIntegridadArchivoAsync(string rutaRelativa, string hashOriginal)
        {
            var rutaFisica = ObtenerRutaFisica(rutaRelativa);
            if (!File.Exists(rutaFisica)) return false;

            var hashActual = await CalcularHashArchivoAsync(rutaFisica);

            // Comparacion en tiempo constante: el hash decide si un comprobante se
            // considera integro, asi que se trata como un secreto mas.
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(hashActual),
                Encoding.UTF8.GetBytes(hashOriginal ?? string.Empty));
        }

        private static string ConstruirRutaRelativa(string carpeta, string nombreArchivo) =>
            Path.Combine("uploads", carpeta, nombreArchivo).Replace("\\", "/");

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

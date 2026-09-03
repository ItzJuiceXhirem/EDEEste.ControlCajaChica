using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Application.DTOs;
using EDEEste.ControlCajaChica.Application.Features.Gastos;
using EDEEste.ControlCajaChica.Domain.Common;
using EDEEste.ControlCajaChica.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace EDEEste.ControlCajaChica.Infrastructure.Services
{
    public class FileStorageService : IFileStorageService
    {
        private const string CarpetaComprobantes = "comprobantes";
        private const string CarpetaReposiciones = "reposiciones";
        private const string CarpetaStaging = "staging";
        private const string ExtensionManifiesto = ".meta.json";

        // Fuera de wwwroot a propósito: MapStaticAssets solo sirve el manifiesto
        // armado al compilar, así que hoy nada expone estos archivos por su ruta
        // directa -- pero guardarlos bajo wwwroot los dejaba a un solo cambio de
        // configuración (agregar UseStaticFiles, por ejemplo) de quedar servidos
        // sin pasar por el permiso de GastoEndpoints/ReposicionEndpoints.
        private readonly string _rutaRaiz;
        private readonly ICriptografiaService _criptografia;

        public FileStorageService(IOptions<OpcionesAlmacenamiento> opciones, ICriptografiaService criptografia)
        {
            _rutaRaiz = opciones.Value.RutaRaiz;
            _criptografia = criptografia;

            Directory.CreateDirectory(Path.Combine(_rutaRaiz, "uploads", CarpetaComprobantes));
            Directory.CreateDirectory(Path.Combine(_rutaRaiz, "uploads", CarpetaReposiciones));
            Directory.CreateDirectory(Path.Combine(_rutaRaiz, "uploads", CarpetaStaging));
        }

        /// <summary>
        /// Resuelve la ruta relativa guardada en BDD a una ruta fisica en disco.
        ///
        /// Se valida que el resultado siga colgando de _rutaRaiz (App_Data): si una
        /// ruta con ".." llegara desde la BDD (fila manipulada, migracion de datos
        /// vieja), sin esta comprobacion se podria leer o borrar cualquier archivo
        /// del servidor.
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

        // ── Staging ──────────────────────────────────────────────────────────────

        /// <summary>
        /// La carpeta es el HMAC del Id del usuario y no el Id crudo: aunque hoy es
        /// un GUID de Identity, el modo ActiveDirectory (todavia stub) podria algun
        /// dia traer ids con separadores de dominio, y un Path.Combine con eso seria
        /// un escape de directorio DENTRO de App_Data que ObtenerRutaFisica no
        /// atraparia (su guarda solo cuida el limite exterior). El HMAC da un
        /// alfabeto fijo (hexadecimal) sin importar que forma tenga el Id de origen.
        /// </summary>
        private string CarpetaDeUsuario(string usuarioId) =>
            Path.Combine(_rutaRaiz, "uploads", CarpetaStaging, _criptografia.CalcularHMAC(usuarioId));

        private static string CadenaFirmaManifiesto(ComprobanteStagingDto manifiesto) =>
            new ConstructorFirma("ComprobanteStaging")
                .Agregar(manifiesto.UsuarioId)
                .Agregar(manifiesto.Referencia)
                .Agregar(manifiesto.NombreOriginal)
                .Agregar(manifiesto.Extension)
                .ToString();

        public async Task<Guid> GuardarComprobanteEnStagingAsync(
            string usuarioId,
            string nombreOriginal,
            string extension,
            Stream contenido,
            CancellationToken cancellationToken = default)
        {
            var referencia = Guid.NewGuid();
            var carpetaUsuario = CarpetaDeUsuario(usuarioId);
            Directory.CreateDirectory(carpetaUsuario);

            var rutaArchivo = Path.Combine(carpetaUsuario, $"{referencia}{extension}");

            // FileMode.CreateNew y no Create: una colision de Guid debe reventar de
            // forma ruidosa, no pisar en silencio un archivo que ya estaba ahi.
            await using (var destino = new FileStream(rutaArchivo, FileMode.CreateNew))
            {
                contenido.Position = 0;
                await contenido.CopyToAsync(destino, cancellationToken);
            }

            var manifiesto = new ComprobanteStagingDto
            {
                Referencia = referencia,
                UsuarioId = usuarioId,
                NombreOriginal = nombreOriginal,
                Extension = extension,
                FechaSubida = DateTime.UtcNow
            };
            manifiesto.Firma = _criptografia.CalcularHMAC(CadenaFirmaManifiesto(manifiesto));

            var rutaManifiesto = Path.Combine(carpetaUsuario, $"{referencia}{ExtensionManifiesto}");
            await File.WriteAllTextAsync(rutaManifiesto, JsonSerializer.Serialize(manifiesto), cancellationToken);

            return referencia;
        }

        public async Task<ComprobanteStagingDto?> LeerManifiestoStagingAsync(
            string usuarioId,
            Guid referencia,
            CancellationToken cancellationToken = default)
        {
            var carpetaUsuario = CarpetaDeUsuario(usuarioId);
            var rutaManifiesto = Path.Combine(carpetaUsuario, $"{referencia}{ExtensionManifiesto}");

            if (!File.Exists(rutaManifiesto))
            {
                return null;
            }

            ComprobanteStagingDto? manifiesto;
            try
            {
                var json = await File.ReadAllTextAsync(rutaManifiesto, cancellationToken);
                manifiesto = JsonSerializer.Deserialize<ComprobanteStagingDto>(json);
            }
            catch (JsonException)
            {
                return null;
            }

            if (manifiesto is null
                || !_criptografia.ValidarFirma(CadenaFirmaManifiesto(manifiesto), manifiesto.Firma))
            {
                return null;
            }

            // El manifiesto vive bajo la carpeta del usuario (que ya lo delimita por
            // si sola), pero se revalida el campo tambien: es lo que hace que copiar
            // un manifiesto entero a otra carpeta no baste para reusarlo.
            if (!string.Equals(manifiesto.UsuarioId, usuarioId, StringComparison.Ordinal)
                || manifiesto.Referencia != referencia)
            {
                return null;
            }

            var rutaArchivo = Path.Combine(carpetaUsuario, $"{referencia}{manifiesto.Extension}");
            return File.Exists(rutaArchivo) ? manifiesto : null;
        }

        public async Task<ComprobantePromovidoDto> PromoverComprobanteAsync(
            string usuarioId,
            ComprobanteStagingDto manifiesto,
            CancellationToken cancellationToken = default)
        {
            var carpetaUsuario = CarpetaDeUsuario(usuarioId);
            var rutaOrigen = Path.Combine(carpetaUsuario, $"{manifiesto.Referencia}{manifiesto.Extension}");

            var nombreFinal = $"{Guid.NewGuid()}{manifiesto.Extension}";
            var rutaDestino = Path.Combine(_rutaRaiz, "uploads", CarpetaComprobantes, nombreFinal);

            // Copia y no mueve: si el guardado en BDD falla despues (el ejemplo mas
            // comun es un choque de concurrencia en el balance del fondo, no algo
            // exotico), el original en staging sigue intacto y el reintento no
            // depende de volver a adjuntar nada.
            await using (var origen = File.OpenRead(rutaOrigen))
            await using (var destino = new FileStream(rutaDestino, FileMode.CreateNew))
            {
                await origen.CopyToAsync(destino, cancellationToken);
            }

            var tipoMime = ValidadorComprobante.MimeCanonicoPorExtension(manifiesto.Extension)
                ?? throw new InvalidOperationException(
                    $"La extension '{manifiesto.Extension}' del manifiesto no tiene un MIME canonico.");

            return new ComprobantePromovidoDto
            {
                RutaRelativa = ConstruirRutaRelativa(CarpetaComprobantes, nombreFinal),
                NombreOriginal = manifiesto.NombreOriginal,
                TipoMime = tipoMime,
                TamanoBytes = new FileInfo(rutaDestino).Length,
                HashSha256 = await CalcularHashArchivoAsync(rutaDestino)
            };
        }

        public Task EliminarStagingAsync(
            string usuarioId,
            Guid referencia,
            CancellationToken cancellationToken = default)
        {
            var carpetaUsuario = CarpetaDeUsuario(usuarioId);
            if (!Directory.Exists(carpetaUsuario))
            {
                return Task.CompletedTask;
            }

            // El patron "{referencia}.*" alcanza tanto el archivo como su manifiesto
            // ("{referencia}.meta.json") sin depender de poder leer el manifiesto
            // primero -- una limpieza no deberia poder fallar por un manifiesto ya
            // corrupto.
            foreach (var ruta in Directory.EnumerateFiles(carpetaUsuario, $"{referencia}.*"))
            {
                File.Delete(ruta);
            }

            return Task.CompletedTask;
        }

        public Task<(int Archivos, long Bytes)> ContarStagingAsync(
            string usuarioId,
            CancellationToken cancellationToken = default)
        {
            var carpetaUsuario = CarpetaDeUsuario(usuarioId);
            if (!Directory.Exists(carpetaUsuario))
            {
                return Task.FromResult((0, 0L));
            }

            // Solo los archivos de datos, no los manifiestos: contar los dos
            // duplicaria el numero de "archivos pendientes" que percibe el usuario.
            var archivos = Directory.EnumerateFiles(carpetaUsuario)
                .Where(ruta => !ruta.EndsWith(ExtensionManifiesto, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return Task.FromResult((archivos.Count, archivos.Sum(ruta => new FileInfo(ruta).Length)));
        }

        public Task LimpiarStagingDelUsuarioAsync(
            string usuarioId,
            TimeSpan antiguedad,
            CancellationToken cancellationToken = default)
        {
            LimpiarCarpeta(CarpetaDeUsuario(usuarioId), DateTime.UtcNow - antiguedad);
            return Task.CompletedTask;
        }

        public Task<int> LimpiarStagingAbandonadoAsync(
            TimeSpan antiguedad,
            CancellationToken cancellationToken = default)
        {
            var rutaStaging = Path.Combine(_rutaRaiz, "uploads", CarpetaStaging);
            if (!Directory.Exists(rutaStaging))
            {
                return Task.FromResult(0);
            }

            var limite = DateTime.UtcNow - antiguedad;
            var total = 0;

            foreach (var carpetaUsuario in Directory.EnumerateDirectories(rutaStaging))
            {
                cancellationToken.ThrowIfCancellationRequested();
                total += LimpiarCarpeta(carpetaUsuario, limite);
            }

            return Task.FromResult(total);
        }

        /// <summary>
        /// El manifiesto se borra antes que su archivo: una limpieza a medias deja un
        /// archivo sin manifiesto (que LeerManifiestoStagingAsync ya rechaza), nunca
        /// un manifiesto senalando un archivo que ya no esta. Al final borra la
        /// carpeta del usuario si quedo vacia, para que no se acumulen carpetas
        /// vacias de usuarios que ya ni suben archivos.
        /// </summary>
        private static int LimpiarCarpeta(string carpetaUsuario, DateTime limite)
        {
            if (!Directory.Exists(carpetaUsuario))
            {
                return 0;
            }

            foreach (var manifiesto in Directory.EnumerateFiles(carpetaUsuario, $"*{ExtensionManifiesto}"))
            {
                if (File.GetLastWriteTimeUtc(manifiesto) < limite)
                {
                    File.Delete(manifiesto);
                }
            }

            var borrados = 0;
            foreach (var archivo in Directory.EnumerateFiles(carpetaUsuario)
                         .Where(ruta => !ruta.EndsWith(ExtensionManifiesto, StringComparison.OrdinalIgnoreCase)))
            {
                if (File.GetLastWriteTimeUtc(archivo) < limite)
                {
                    File.Delete(archivo);
                    borrados++;
                }
            }

            if (!Directory.EnumerateFileSystemEntries(carpetaUsuario).Any())
            {
                Directory.Delete(carpetaUsuario);
            }

            return borrados;
        }
    }
}

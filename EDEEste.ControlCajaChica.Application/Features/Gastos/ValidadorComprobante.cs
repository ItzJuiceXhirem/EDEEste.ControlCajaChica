using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EDEEste.ControlCajaChica.Application.Features.Gastos
{
    /// <summary>
    /// Reglas de que archivo se acepta como comprobante, en un solo sitio.
    ///
    /// Vive en Application y no en la capa web a proposito: es la unica capa que el
    /// proyecto de pruebas referencia, y estas comprobaciones son controles de
    /// seguridad (lista blanca de formatos y firma del contenido) que no pueden
    /// quedarse sin cobertura por mudarse a un endpoint.
    ///
    /// Estatico y sin dependencias: no toca disco, no conoce HTTP y no sabe de donde
    /// salio el archivo, asi que sirve igual para el endpoint de subida que para el
    /// handler que confirma el gasto.
    /// </summary>
    public static class ValidadorComprobante
    {
        private static readonly HashSet<string> TiposMimePermitidos = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "image/jpeg",
            "image/jpg",
            "image/png"
        };

        private static readonly HashSet<string> ExtensionesPermitidas = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png"
        };

        // Firmas (magic bytes) de los unicos cuatro tipos que TiposMimePermitidos
        // acepta. No hace falta una firma de PNG/JPEG separada por variante: los
        // primeros bytes ya identifican el formato sin importar el resto del archivo.
        private static readonly byte[] FirmaPdf = "%PDF"u8.ToArray();
        private static readonly byte[] FirmaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        private static readonly byte[] FirmaJpeg = [0xFF, 0xD8, 0xFF];

        /// <summary>
        /// MIME y extension, la unica comprobacion que no necesita leer el contenido
        /// del archivo. Se validan las dos cosas: el navegador reporta el MIME y es
        /// facil de falsear, pero la extension es la que decide como se abre el
        /// archivo despues.
        /// </summary>
        public static bool EsFormatoAceptado(string? nombreOriginal, string? tipoMime) =>
            tipoMime is not null
            && TiposMimePermitidos.Contains(tipoMime)
            && ExtensionesPermitidas.Contains(Path.GetExtension(nombreOriginal ?? string.Empty));

        /// <summary>
        /// Compara los primeros bytes del archivo contra la firma del tipo que declara.
        /// Deja el stream en la posicion 0 al terminar: quien llama todavia necesita
        /// leerlo completo desde el principio para guardarlo.
        ///
        /// Un stream que no se pueda rebobinar devuelve false en vez de saltarse la
        /// comprobacion: que este control degrade en silencio es el peor resultado
        /// posible, asi que falla cerrado.
        /// </summary>
        public static async Task<bool> CoincideConFirmaEsperadaAsync(
            Stream contenido,
            string? tipoMime,
            CancellationToken cancellationToken = default)
        {
            var firma = FirmaEsperada(tipoMime);

            // Un TipoMime fuera de la lista blanca lo rechaza EsFormatoAceptado por su
            // cuenta; aqui no hay firma con la que comparar, asi que no se declara
            // coincidencia.
            if (firma is null || !contenido.CanSeek)
            {
                return false;
            }

            var buffer = new byte[firma.Length];
            contenido.Position = 0;
            // ReadAtLeastAsync y no ReadAsync: una sola lectura puede devolver menos
            // bytes de los pedidos aunque el archivo los tenga. Con throwOnEndOfStream
            // en false, un archivo mas corto que la firma devuelve lo que haya y la
            // comparacion de longitud de abajo lo rechaza.
            var leidos = await contenido.ReadAtLeastAsync(
                buffer, firma.Length, throwOnEndOfStream: false, cancellationToken);
            contenido.Position = 0;

            return leidos == firma.Length && buffer.AsSpan().SequenceEqual(firma);
        }

        /// <summary>
        /// La extension en minusculas si esta en la lista blanca; null si no. Es la
        /// extension que el servidor le pone al archivo guardado -- nunca se reutiliza
        /// la que venga en el nombre del cliente sin pasar por aqui.
        /// </summary>
        public static string? ExtensionCanonica(string? nombreOriginal)
        {
            var extension = Path.GetExtension(nombreOriginal ?? string.Empty);

            return ExtensionesPermitidas.Contains(extension)
                ? extension.ToLowerInvariant()
                : null;
        }

        /// <summary>
        /// El MIME que corresponde a una extension ya validada. Es la fuente de verdad
        /// del TipoMime que se guarda: derivarlo de la extension que eligio el servidor
        /// (y no del que reporta el navegador) evita que alguien suba un PDF legitimo y
        /// lo declare "text/html", que es como se sirve despues en la descarga.
        ///
        /// De paso normaliza "image/jpg", que no es un tipo registrado, a "image/jpeg".
        /// </summary>
        public static string? MimeCanonicoPorExtension(string? extension) =>
            (extension ?? string.Empty).ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                _ => null
            };

        private static byte[]? FirmaEsperada(string? tipoMime) =>
            (tipoMime ?? string.Empty).ToLowerInvariant() switch
            {
                "application/pdf" => FirmaPdf,
                "image/png" => FirmaPng,
                "image/jpeg" or "image/jpg" => FirmaJpeg,
                _ => null
            };
    }
}

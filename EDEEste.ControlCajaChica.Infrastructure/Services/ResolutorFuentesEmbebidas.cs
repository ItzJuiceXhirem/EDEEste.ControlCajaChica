using System;
using System.IO;
using PdfSharp.Fonts;

namespace EDEEste.ControlCajaChica.Infrastructure.Services
{
    /// <summary>
    /// Sirve Lato (embebida como recurso, SIL OFL) para todo lo que PDFsharp/MigraDoc
    /// dibuje. Se resuelve SIEMPRE a Lato sin importar el nombre de familia pedido: es
    /// la unica tipografia que el proyecto embebe, y dejar caer una familia no
    /// reconocida al resolutor de plataforma reintroduciria la dependencia que esto
    /// existe para evitar -- bajo IIS, con la identidad del app pool, no hay garantia
    /// de que la carpeta de fuentes de Windows sea siquiera accesible.
    ///
    /// Se eligio Lato (y no otra libre como Liberation Sans) porque es la misma
    /// tipografia que QuestPDF usaba por defecto: el expediente cambia de motor de PDF
    /// sin cambiar de cara, que es la mejor garantia de fidelidad visual disponible.
    /// </summary>
    public sealed class ResolutorFuentesEmbebidas : IFontResolver
    {
        /// <summary>
        /// Nombre de familia para pedirle esta fuente a PDFsharp/MigraDoc (XFont,
        /// Styles["Normal"].Font.Name, etc.). ResolveTypeface ignora el nombre
        /// recibido y siempre devuelve Lato, asi que este valor no cambia que fuente
        /// se dibuja -- pero es la unica fuente de verdad de como se llama, para no
        /// repetir el literal en cada sitio que arma texto (ver PdfConsolidadorService).
        /// </summary>
        public const string NombreFamilia = "Lato";

        private const string RecursoRegular = "Lato-Regular.ttf";
        private const string RecursoBold = "Lato-Bold.ttf";
        private const string RecursoItalic = "Lato-Italic.ttf";
        private const string RecursoBoldItalic = "Lato-BoldItalic.ttf";

        /// <summary>
        /// Registra el resolutor una sola vez. GlobalFontSettings.FontResolver lanza
        /// "Must not change font resolver after it was once used" si se reasigna
        /// despues del primer GetFont/ResolveTypeface, asi que esta guarda importa mas
        /// alla de evitar trabajo repetido -- una segunda llamada real (dos hosts en el
        /// mismo proceso, por ejemplo en pruebas) tumbaria el proceso sin ella.
        /// </summary>
        public static void Registrar()
        {
            if (GlobalFontSettings.FontResolver is ResolutorFuentesEmbebidas)
            {
                return;
            }

            GlobalFontSettings.FontResolver = new ResolutorFuentesEmbebidas();
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            var faceName = (isBold, isItalic) switch
            {
                (true, true) => RecursoBoldItalic,
                (true, false) => RecursoBold,
                (false, true) => RecursoItalic,
                (false, false) => RecursoRegular
            };

            return new FontResolverInfo(faceName);
        }

        // El prefijo es el namespace raiz del proyecto (Infrastructure), NO el de esta
        // clase (Infrastructure.Services): la carpeta Fonts/ cuelga de la raiz del
        // proyecto, y el nombre de un recurso embebido sigue esa ruta de carpetas, no
        // el namespace del tipo que lo lee. Verificado listando GetManifestResourceNames()
        // sobre el ensamblado compilado antes de fijar esto.
        private const string PrefijoRecursos = "EDEEste.ControlCajaChica.Infrastructure.Fonts";

        public byte[] GetFont(string faceName)
        {
            var nombreCompleto = $"{PrefijoRecursos}.{faceName}";
            using var recurso = typeof(ResolutorFuentesEmbebidas).Assembly.GetManifestResourceStream(nombreCompleto)
                ?? throw new InvalidOperationException(
                    $"No se encontro el recurso embebido '{nombreCompleto}' para la fuente '{faceName}'.");

            using var memoria = new MemoryStream();
            recurso.CopyTo(memoria);
            return memoria.ToArray();
        }
    }
}

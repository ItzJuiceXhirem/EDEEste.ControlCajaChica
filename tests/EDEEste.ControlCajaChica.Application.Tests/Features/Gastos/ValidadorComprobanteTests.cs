using System.IO;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Features.Gastos;

namespace EDEEste.ControlCajaChica.Application.Tests.Features.Gastos
{
    /// <summary>
    /// Las reglas de que archivo se acepta como comprobante. Antes vivian dentro de
    /// RegistrarGastoHandler y solo se probaban de rebote a traves de el; al quedar
    /// en Application se pueden probar directas, incluida la lista blanca de
    /// extensiones, que no tenia ninguna prueba (todas usaban "factura.pdf").
    /// </summary>
    public class ValidadorComprobanteTests
    {
        // Bastante mas grande que el piso de tamano de ValidadorComprobante (512
        // bytes): un PDF real, aunque sea minimo, nunca se acerca a ese piso. Lo que
        // haya despues de la firma es irrelevante para estas pruebas.
        private static MemoryStream ContenidoPdf()
        {
            var bytes = new byte[600];
            "%PDF-1.4"u8.CopyTo(bytes);
            return new MemoryStream(bytes);
        }

        [Fact]
        public void EsFormatoAceptado_ConPdfValido_Pasa() =>
            Assert.True(ValidadorComprobante.EsFormatoAceptado("factura.pdf", "application/pdf"));

        [Fact]
        public void EsFormatoAceptado_ConMimeFueraDeListaBlanca_Falla() =>
            Assert.False(ValidadorComprobante.EsFormatoAceptado("factura.pdf", "text/html"));

        /// <summary>
        /// El MIME lo reporta el navegador y se falsea con solo renombrar; la extension
        /// es la que decide como se abre el archivo despues, asi que las dos tienen que
        /// estar en la lista blanca. Este caso no tenia cobertura.
        /// </summary>
        [Fact]
        public void EsFormatoAceptado_ConExtensionFueraDeListaBlanca_Falla() =>
            Assert.False(ValidadorComprobante.EsFormatoAceptado("factura.exe", "application/pdf"));

        [Fact]
        public void EsFormatoAceptado_SinExtension_Falla() =>
            Assert.False(ValidadorComprobante.EsFormatoAceptado("factura", "application/pdf"));

        [Fact]
        public void EsFormatoAceptado_IgnoraMayusculas() =>
            Assert.True(ValidadorComprobante.EsFormatoAceptado("FACTURA.PDF", "APPLICATION/PDF"));

        [Fact]
        public async Task CoincideConFirmaEsperada_ConPdfReal_Pasa()
        {
            await using var contenido = ContenidoPdf();
            Assert.True(await ValidadorComprobante.CoincideConFirmaEsperadaAsync(contenido, "application/pdf"));
        }

        [Fact]
        public async Task CoincideConFirmaEsperada_ConContenidoQueNoEsPdf_Falla()
        {
            await using var contenido = new MemoryStream([1, 2, 3]);
            Assert.False(await ValidadorComprobante.CoincideConFirmaEsperadaAsync(contenido, "application/pdf"));
        }

        /// <summary>
        /// Renombrar un PDF a .png no lo convierte en PNG: la firma es lo unico que un
        /// cambio de nombre no evade.
        /// </summary>
        [Fact]
        public async Task CoincideConFirmaEsperada_ConPdfDeclaradoComoPng_Falla()
        {
            await using var contenido = ContenidoPdf();
            Assert.False(await ValidadorComprobante.CoincideConFirmaEsperadaAsync(contenido, "image/png"));
        }

        [Fact]
        public async Task CoincideConFirmaEsperada_DejaElStreamAlPrincipio()
        {
            await using var contenido = ContenidoPdf();
            await ValidadorComprobante.CoincideConFirmaEsperadaAsync(contenido, "application/pdf");

            // Quien llama despues necesita leerlo completo para guardarlo.
            Assert.Equal(0, contenido.Position);
        }

        /// <summary>
        /// Falla cerrado: un stream que no se puede rebobinar no permite comprobar la
        /// firma, y saltarse el control en silencio seria peor que rechazar el archivo.
        /// </summary>
        [Fact]
        public async Task CoincideConFirmaEsperada_ConStreamNoRebobinable_Falla()
        {
            await using var contenido = new StreamSoloAvance("%PDF-1.4"u8.ToArray());
            Assert.False(await ValidadorComprobante.CoincideConFirmaEsperadaAsync(contenido, "application/pdf"));
        }

        /// <summary>
        /// El caso real que motivo este piso: una subida cortada por un corte de red
        /// puede dejar en disco solo la cabecera "%PDF-1.4" (8 bytes) y nada mas. Esos
        /// bytes coinciden con la firma esperada igual que un PDF completo -- el
        /// piso de tamano es lo unico que distingue los dos casos.
        /// </summary>
        [Fact]
        public async Task CoincideConFirmaEsperada_PorDebajoDelTamanoMinimo_Falla()
        {
            await using var contenido = new MemoryStream("%PDF-1.4"u8.ToArray());
            Assert.False(await ValidadorComprobante.CoincideConFirmaEsperadaAsync(contenido, "application/pdf"));
        }

        /// <summary>
        /// Exactamente en el piso (512 bytes): confirma que el limite es inclusive y
        /// no rechaza un comprobante legitimo que caiga justo ahi.
        /// </summary>
        [Fact]
        public async Task CoincideConFirmaEsperada_EnElTamanoMinimo_Pasa()
        {
            var bytes = new byte[512];
            "%PDF-1.4"u8.CopyTo(bytes);
            await using var contenido = new MemoryStream(bytes);
            Assert.True(await ValidadorComprobante.CoincideConFirmaEsperadaAsync(contenido, "application/pdf"));
        }

        [Fact]
        public void ExtensionCanonica_NormalizaAMinusculas() =>
            Assert.Equal(".pdf", ValidadorComprobante.ExtensionCanonica("FACTURA.PDF"));

        [Fact]
        public void ExtensionCanonica_ConExtensionNoPermitida_DaNull() =>
            Assert.Null(ValidadorComprobante.ExtensionCanonica("factura.exe"));

        /// <summary>
        /// "image/jpg" no es un tipo registrado; el canonico es "image/jpeg". Derivar el
        /// MIME de la extension que eligio el servidor es lo que impide que alguien suba
        /// un PDF legitimo y lo haga servir como otra cosa en la descarga.
        /// </summary>
        [Fact]
        public void MimeCanonicoPorExtension_NormalizaJpgAJpeg()
        {
            Assert.Equal("image/jpeg", ValidadorComprobante.MimeCanonicoPorExtension(".jpg"));
            Assert.Equal("image/jpeg", ValidadorComprobante.MimeCanonicoPorExtension(".jpeg"));
        }

        [Fact]
        public void MimeCanonicoPorExtension_CubreLosTresFormatos()
        {
            Assert.Equal("application/pdf", ValidadorComprobante.MimeCanonicoPorExtension(".pdf"));
            Assert.Equal("image/png", ValidadorComprobante.MimeCanonicoPorExtension(".png"));
        }

        [Fact]
        public void MimeCanonicoPorExtension_ConExtensionDesconocida_DaNull() =>
            Assert.Null(ValidadorComprobante.MimeCanonicoPorExtension(".exe"));

        /// <summary>
        /// Un stream de solo avance, como los que entrega el navegador: no se puede
        /// rebobinar para leer la firma y volver al principio.
        /// </summary>
        private sealed class StreamSoloAvance(byte[] datos) : MemoryStream(datos)
        {
            public override bool CanSeek => false;
        }
    }
}

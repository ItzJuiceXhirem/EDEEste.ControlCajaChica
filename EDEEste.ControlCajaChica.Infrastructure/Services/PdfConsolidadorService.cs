using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Entities;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace EDEEste.ControlCajaChica.Infrastructure.Services
{
    /// <summary>
    /// Arma el expediente PDF de una solicitud de reposicion: una portada-resumen con
    /// la tabla de gastos, seguida, por cada comprobante adjunto, de una pagina de
    /// transcripcion (los datos del gasto + la etiqueta que digito el custodio) y
    /// despues el archivo original.
    ///
    /// MigraDoc genera cada pieza (paginas nuevas, texto, imagenes) pero no puede
    /// insertar paginas de un PDF que no genero el; por eso el ensamblaje final usa
    /// PDFsharp solo para concatenar paginas ya generadas -- el PDF original de cada
    /// comprobante se copia tal cual, nunca se reabre para editarlo ni se
    /// re-renderiza, asi que el archivo guardado en disco (y su HashSHA256) no se
    /// tocan en ningun momento.
    /// </summary>
    public class PdfConsolidadorService : IPdfConsolidadorService
    {
        // Aproximan la paleta Material Design que usaba QuestPDF.Helpers.Colors, para
        // que el expediente cambie de motor de PDF sin cambiar de aspecto:
        // Blue.Darken3, Grey.Lighten3, Grey.Lighten2, Grey.Darken1 y Red.Medium.
        private static readonly Color AzulOscuro = new(0x15, 0x65, 0xC0);
        private static readonly Color GrisClaro = new(0xEE, 0xEE, 0xEE);
        private static readonly Color GrisClaro2 = new(0xE0, 0xE0, 0xE0);
        private static readonly Color GrisOscuro = new(0x75, 0x75, 0x75);
        private static readonly Color RojoMedio = new(0xF4, 0x43, 0x36);

        private static readonly Unit MargenPagina = Unit.FromCentimeter(1.5);

        private readonly IFileStorageService _fileStorageService;
        private readonly IIdentityService _identityService;

        public PdfConsolidadorService(IFileStorageService fileStorageService, IIdentityService identityService)
        {
            _fileStorageService = fileStorageService;
            _identityService = identityService;
        }

        public async Task<byte[]> ConsolidarComprobantesAsync(SolicitudReposicion solicitud, CancellationToken cancellationToken = default)
        {
            using var documentoFinal = new PdfDocument();

            var nombreCustodio = await ResolverNombreCustodioAsync(solicitud.FondoCajaChica?.CustodioId);
            AgregarPaginas(documentoFinal, GenerarResumen(solicitud, nombreCustodio));

            foreach (var gasto in solicitud.Gastos)
            {
                foreach (var comprobante in gasto.Comprobantes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AgregarComprobante(documentoFinal, gasto, comprobante);
                }
            }

            EstamparNumerosDePagina(documentoFinal);

            using var salida = new MemoryStream();
            // closeStream: false a proposito: por defecto Save() cierra el stream que
            // recibe, y si lo cerrara aca, salida.ToArray() de la linea siguiente
            // fallaria con ObjectDisposedException porque el stream ya no existiria.
            documentoFinal.Save(salida, closeStream: false);
            return salida.ToArray();
        }

        /// <summary>
        /// Numera el expediente completo al final, cuando ya se sabe cuantas paginas
        /// tiene.
        ///
        /// No se puede hacer con un footer de MigraDoc: el documento se arma juntando
        /// varios PDF chicos independientes (y los originales de los comprobantes, que
        /// ni siquiera generamos nosotros), asi que cada pieza numeraria desde 1 por su
        /// cuenta. Para un expediente contable la numeracion continua importa: es lo
        /// que permite afirmar que no falta ninguna hoja.
        /// </summary>
        private static void EstamparNumerosDePagina(PdfDocument documento)
        {
            var fuente = new XFont("Lato", 8, XFontStyleEx.Regular);
            var total = documento.PageCount;

            for (var indice = 0; indice < total; indice++)
            {
                var pagina = documento.Pages[indice];

                // Append dibuja encima del contenido que ya trae la pagina, sin
                // reescribirlo: las paginas importadas de un PDF ajeno quedan intactas.
                using var lienzo = XGraphics.FromPdfPage(pagina, XGraphicsPdfPageOptions.Append);

                var area = new XRect(0, pagina.Height.Point - 25, pagina.Width.Point, 15);
                lienzo.DrawString(
                    $"Pagina {indice + 1} de {total}",
                    fuente,
                    XBrushes.Gray,
                    area,
                    XStringFormats.TopCenter);
            }
        }

        private async Task<string> ResolverNombreCustodioAsync(string? custodioId)
        {
            if (string.IsNullOrWhiteSpace(custodioId))
            {
                return "(sin custodio asignado)";
            }

            var nombre = await _identityService.ObtenerNombreUsuarioAsync(custodioId);
            return nombre ?? custodioId;
        }

        private void AgregarComprobante(PdfDocument documentoFinal, Gasto gasto, ComprobanteAdjunto comprobante)
        {
            var rutaFisica = _fileStorageService.ObtenerRutaFisica(comprobante.RutaArchivo);
            var esImagen = comprobante.TipoMime.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

            var portada = NuevoDocumento();
            ComponerPortada(portada.LastSection, gasto, comprobante);
            AgregarPaginas(documentoFinal, Renderizar(portada));

            if (esImagen)
            {
                var imagen = NuevoDocumento();
                ComponerImagen(imagen.LastSection, rutaFisica);
                AgregarPaginas(documentoFinal, Renderizar(imagen));
                return;
            }

            if (!File.Exists(rutaFisica))
            {
                AgregarPaginas(documentoFinal, GenerarAviso("El comprobante no existe en el servidor."));
                return;
            }

            // Un PDF guardado pero truncado o corrupto (por ejemplo, una subida
            // cortada por un corte de red) pasa el chequeo de magic bytes de
            // ValidadorComprobante -- que solo mira que el archivo EMPIECE con "%PDF"
            // -- pero PdfReader.Open no puede parsearlo despues. Sin este try/catch,
            // CrearSolicitudReposicionHandler genera este PDF ANTES de guardar la
            // solicitud, asi que un solo comprobante ilegible tumbaba la reposicion
            // completa; y el custodio no tiene como resolverlo por su cuenta, porque
            // ComprobanteAdjunto ya esta firmado con HMAC y es inmutable. Se prefiere
            // dejar constancia visible del problema dentro del propio expediente,
            // igual que ya se hacia para un archivo faltante.
            try
            {
                AgregarPaginas(documentoFinal, File.ReadAllBytes(rutaFisica));
            }
            catch (Exception)
            {
                AgregarPaginas(documentoFinal, GenerarAviso("El comprobante no se pudo leer (archivo dañado o incompleto)."));
            }
        }

        private static void AgregarPaginas(PdfDocument destino, byte[] pdfBytes)
        {
            using var stream = new MemoryStream(pdfBytes);
            using var origen = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
            foreach (var pagina in origen.Pages)
            {
                destino.AddPage(pagina);
            }
        }

        private static Document NuevoDocumento()
        {
            var documento = new Document();
            var seccion = documento.AddSection();
            seccion.PageSetup.PageFormat = PageFormat.A4;
            seccion.PageSetup.LeftMargin = MargenPagina;
            seccion.PageSetup.RightMargin = MargenPagina;
            seccion.PageSetup.TopMargin = MargenPagina;
            seccion.PageSetup.BottomMargin = MargenPagina;
            documento.Styles["Normal"]!.Font.Name = "Lato";
            documento.Styles["Normal"]!.Font.Size = 10;
            return documento;
        }

        /// <summary>
        /// Ancho y alto del area de contenido (formato de pagina menos margenes),
        /// para dimensionar tablas e imagenes.
        ///
        /// OJO: no se puede leer seccion.PageSetup.PageWidth/PageHeight para esto --
        /// esas propiedades devuelven CERO si se consultan antes de renderizar el
        /// documento (MigraDoc solo las calcula durante el render, no al asignar
        /// PageFormat). Usar ese cero produjo anchos de columna negativos y un
        /// colapso visible de todas las tablas del expediente -- detectado
        /// rasterizando el PDF a imagen y comparando contra el resultado esperado,
        /// no fue evidente sin verlo. PageSetup.GetPageSize es el metodo estatico
        /// que da las dimensiones reales de un formato de pagina sin depender del
        /// estado de render de ningun documento.
        /// </summary>
        private static (Unit Ancho, Unit Alto) ObtenerAreaDeContenido()
        {
            PageSetup.GetPageSize(PageFormat.A4, out var anchoPagina, out var altoPagina);
            return (anchoPagina - MargenPagina - MargenPagina, altoPagina - MargenPagina - MargenPagina);
        }

        private static byte[] Renderizar(Document documento)
        {
            var renderizador = new PdfDocumentRenderer { Document = documento };
            renderizador.RenderDocument();

            using var ms = new MemoryStream();
            renderizador.PdfDocument.Save(ms, closeStream: false);
            return ms.ToArray();
        }

        private static void ComponerPortada(Section seccion, Gasto gasto, ComprobanteAdjunto comprobante)
        {
            var titulo = seccion.AddParagraph();
            titulo.AddFormattedText("Comprobante de gasto", TextFormat.Bold);
            titulo.Format.Font.Size = 16;
            titulo.Format.Font.Color = AzulOscuro;

            var descripcion = seccion.AddParagraph(comprobante.Descripcion);
            descripcion.Format.Font.Italic = true;
            descripcion.Format.Font.Size = 12;
            descripcion.Format.SpaceBefore = Unit.FromPoint(4);

            var datos = seccion.AddParagraph();
            datos.Format.Shading.Color = GrisClaro;
            datos.Format.SpaceBefore = Unit.FromPoint(15);
            datos.Format.LeftIndent = Unit.FromPoint(10);
            AgregarEtiquetaValor(datos, "Proveedor: ", gasto.Proveedor);
            datos.AddLineBreak();
            AgregarEtiquetaValor(datos, "RNC/Cedula: ", gasto.RNCProveedor);
            datos.AddLineBreak();
            AgregarEtiquetaValor(datos, "NCF: ", gasto.NCF);
            datos.AddLineBreak();
            AgregarEtiquetaValor(datos, "Concepto: ", gasto.Concepto ?? "-");
            datos.AddLineBreak();
            AgregarEtiquetaValor(datos, "Fecha del gasto: ", gasto.FechaGasto.ToString("dd/MM/yyyy"));
            datos.AddLineBreak();
            AgregarEtiquetaValor(datos, "Monto total: ", $"RD$ {gasto.MontoTotal:N2}");

            var nota = seccion.AddParagraph("Archivo original a continuacion:");
            nota.Format.Font.Size = 9;
            nota.Format.Font.Italic = true;
            nota.Format.Font.Color = GrisOscuro;
            nota.Format.SpaceBefore = Unit.FromPoint(10);
        }

        private static void AgregarEtiquetaValor(Paragraph parrafo, string etiqueta, string valor)
        {
            parrafo.AddFormattedText(etiqueta, TextFormat.Bold);
            parrafo.AddText(valor);
        }

        private static void ComponerImagen(Section seccion, string rutaFisica)
        {
            if (!File.Exists(rutaFisica))
            {
                var aviso = seccion.AddParagraph("El comprobante no existe en el servidor.");
                aviso.Format.Alignment = ParagraphAlignment.Center;
                aviso.Format.Font.Color = RojoMedio;
                return;
            }

            var imagen = seccion.AddImage(rutaFisica);
            imagen.LockAspectRatio = true;

            // Equivalente al MaxHeight(700).FitArea() de QuestPDF: sin un tope, una
            // imagen de camara moderna (varios miles de px de alto) desborda la
            // pagina. 700 puntos ~= 24.7 cm, el mismo limite que se usaba antes.
            var altoMaximo = Unit.FromPoint(700);
            var (anchoContenido, _) = ObtenerAreaDeContenido();
            imagen.Height = altoMaximo;
            if (imagen.Width > anchoContenido)
            {
                imagen.Width = anchoContenido;
            }
        }

        /// <summary>
        /// Pagina de aviso centrada (vertical y horizontalmente), en rojo. Comparte
        /// texto y estilo entre "no existe" y "no se pudo leer" -- ver el comentario en
        /// AgregarComprobante sobre por que un comprobante ilegible no puede bloquear
        /// la reposicion completa.
        ///
        /// MigraDoc no tiene un AlignMiddle de pagina como QuestPDF: se aproxima con
        /// una tabla de una sola celda cuya fila ocupa toda el area de contenido
        /// (PageWidth/Height menos margenes, calculado a mano porque las propiedades
        /// Effective* estan obsoletas desde 6.x y ahora describen otra cosa --
        /// orientacion, no margenes) y centra el texto verticalmente adentro.
        /// </summary>
        private static byte[] GenerarAviso(string mensaje)
        {
            var documento = NuevoDocumento();
            var seccion = documento.LastSection;

            var (anchoContenido, altoContenido) = ObtenerAreaDeContenido();

            var tabla = seccion.AddTable();
            tabla.Borders.Width = 0;
            tabla.AddColumn(anchoContenido);

            var fila = tabla.AddRow();
            fila.Height = altoContenido;
            fila.HeightRule = RowHeightRule.Exactly;
            fila.VerticalAlignment = VerticalAlignment.Center;

            var parrafo = fila.Cells[0].AddParagraph(mensaje);
            parrafo.Format.Alignment = ParagraphAlignment.Center;
            parrafo.Format.Font.Color = RojoMedio;

            return Renderizar(documento);
        }

        private static byte[] GenerarResumen(SolicitudReposicion solicitud, string nombreCustodio)
        {
            var documento = NuevoDocumento();
            var seccion = documento.LastSection;
            var (anchoContenido, _) = ObtenerAreaDeContenido();

            // Fila de encabezado: titulo+subtitulo a la izquierda, fecha+id a la
            // derecha, igual que el Row(row => row.RelativeItem()/.ConstantItem(150))
            // de QuestPDF.
            const int anchoColumnaFecha = 150;
            var tablaEncabezado = seccion.AddTable();
            tablaEncabezado.Borders.Width = 0;
            tablaEncabezado.AddColumn(anchoContenido - Unit.FromPoint(anchoColumnaFecha));
            tablaEncabezado.AddColumn(Unit.FromPoint(anchoColumnaFecha));

            var filaEncabezado = tablaEncabezado.AddRow();

            var pTitulo = filaEncabezado.Cells[0].AddParagraph();
            pTitulo.AddFormattedText("CONTROL DE CAJA CHICA", TextFormat.Bold);
            pTitulo.Format.Font.Size = 18;
            pTitulo.Format.Font.Color = AzulOscuro;
            var pSubtitulo = filaEncabezado.Cells[0].AddParagraph("Expediente Consolidado de Reposicion");
            pSubtitulo.Format.Font.Size = 12;
            pSubtitulo.Format.Font.Color = GrisOscuro;

            filaEncabezado.Cells[1].Format.Alignment = ParagraphAlignment.Right;
            filaEncabezado.Cells[1].AddParagraph($"Fecha: {solicitud.FechaSolicitud:dd/MM/yyyy}");
            var pSolicitud = filaEncabezado.Cells[1].AddParagraph($"Solicitud: {solicitud.Id.ToString()[..8]}");
            pSolicitud.Format.Font.Size = 9;

            var custodio = seccion.AddParagraph();
            custodio.Format.Shading.Color = GrisClaro;
            custodio.Format.SpaceBefore = Unit.FromPoint(15);
            custodio.Format.LeftIndent = Unit.FromPoint(10);
            custodio.AddFormattedText("Custodio: ", TextFormat.Bold);
            custodio.AddText(nombreCustodio);

            var tituloDetalle = seccion.AddParagraph("Detalle de comprobantes a reponer");
            tituloDetalle.Format.Font.Bold = true;
            tituloDetalle.Format.Font.Size = 12;
            tituloDetalle.Format.SpaceBefore = Unit.FromPoint(15);

            // Mismas proporciones que QuestPDF: Fecha y NCF fijas, Proveedor:Concepto
            // en razon 2:3, Monto fija.
            const int anchoFecha = 75;
            const int anchoNcf = 90;
            const int anchoMonto = 90;
            var anchoVariable = anchoContenido - Unit.FromPoint(anchoFecha + anchoNcf + anchoMonto);
            var unidad = anchoVariable.Point / 5.0;

            var tabla = seccion.AddTable();
            tabla.Borders.Width = 0;
            tabla.TopPadding = Unit.FromPoint(5);
            tabla.BottomPadding = Unit.FromPoint(5);
            tabla.LeftPadding = Unit.FromPoint(5);
            tabla.RightPadding = Unit.FromPoint(5);
            tabla.AddColumn(Unit.FromPoint(anchoFecha));
            tabla.AddColumn(Unit.FromPoint(anchoNcf));
            tabla.AddColumn(Unit.FromPoint(unidad * 2));
            tabla.AddColumn(Unit.FromPoint(unidad * 3));
            tabla.AddColumn(Unit.FromPoint(anchoMonto));

            var cabecera = tabla.AddRow();
            cabecera.Shading.Color = AzulOscuro;
            cabecera.Format.Font.Color = Colors.White;
            cabecera.Format.Font.Bold = true;
            string[] titulos = { "Fecha", "NCF", "Proveedor", "Concepto", "Monto" };
            for (var i = 0; i < titulos.Length; i++)
            {
                cabecera.Cells[i].AddParagraph(titulos[i]);
            }
            cabecera.Cells[4].Format.Alignment = ParagraphAlignment.Right;

            foreach (var gasto in solicitud.Gastos)
            {
                var fila = tabla.AddRow();
                fila.Borders.Bottom.Width = 1;
                fila.Borders.Bottom.Color = GrisClaro2;
                fila.Cells[0].AddParagraph(gasto.FechaGasto.ToString("dd/MM/yyyy"));
                fila.Cells[1].AddParagraph(gasto.NCF);
                fila.Cells[2].AddParagraph(gasto.Proveedor);
                fila.Cells[3].AddParagraph(gasto.Concepto ?? "-");
                fila.Cells[4].AddParagraph($"RD$ {gasto.MontoTotal:N2}");
                fila.Cells[4].Format.Alignment = ParagraphAlignment.Right;
            }

            var filaTotal = tabla.AddRow();
            filaTotal.Shading.Color = GrisClaro;
            filaTotal.Cells[0].MergeRight = 3;
            var pTotalEtiqueta = filaTotal.Cells[0].AddParagraph("TOTAL RECLAMADO:");
            pTotalEtiqueta.Format.Alignment = ParagraphAlignment.Right;
            pTotalEtiqueta.Format.Font.Bold = true;
            var pTotalMonto = filaTotal.Cells[4].AddParagraph($"RD$ {solicitud.MontoReclamado:N2}");
            pTotalMonto.Format.Alignment = ParagraphAlignment.Right;
            pTotalMonto.Format.Font.Bold = true;
            pTotalMonto.Format.Font.Color = AzulOscuro;

            return Renderizar(documento);
        }
    }
}

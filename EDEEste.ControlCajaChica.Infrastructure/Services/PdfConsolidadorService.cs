using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Entities;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EDEEste.ControlCajaChica.Infrastructure.Services
{
    /// <summary>
    /// Arma el expediente PDF de una solicitud de reposicion: una portada-resumen con
    /// la tabla de gastos, seguida, por cada comprobante adjunto, de una pagina de
    /// transcripcion (los datos del gasto + la etiqueta que digito el custodio) y
    /// despues el archivo original.
    ///
    /// QuestPDF genera cada pieza (paginas nuevas, texto, imagenes) pero no puede
    /// insertar paginas de un PDF que no genero el; por eso el ensamblaje final usa
    /// PdfSharpCore solo para concatenar paginas ya generadas -- el PDF original de
    /// cada comprobante se copia tal cual, nunca se reabre para editarlo ni se
    /// re-renderiza, asi que el archivo guardado en disco (y su HashSHA256) no se
    /// tocan en ningun momento.
    /// </summary>
    public class PdfConsolidadorService : IPdfConsolidadorService
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IIdentityService _identityService;

        public PdfConsolidadorService(IFileStorageService fileStorageService, IIdentityService identityService)
        {
            _fileStorageService = fileStorageService;
            _identityService = identityService;
        }

        public async Task<byte[]> ConsolidarComprobantesAsync(SolicitudReposicion solicitud, CancellationToken cancellationToken = default)
        {
            QuestPDF.Settings.License = LicenseType.Community;

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

            using var salida = new MemoryStream();
            documentoFinal.Save(salida, closeStream: false);
            return salida.ToArray();
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

            if (esImagen)
            {
                // Portada e imagen conviven en un mismo documento QuestPDF: no hace
                // falta fusionar nada, QuestPDF ya sabe dibujar ambas paginas.
                var paginas = Document.Create(container =>
                {
                    container.Page(pagina => ComponerPagina(pagina, c => ComponerPortada(c, gasto, comprobante)));
                    container.Page(pagina => ComponerPagina(pagina, c => ComponerImagen(c, rutaFisica)));
                }).GeneratePdf();

                AgregarPaginas(documentoFinal, paginas);
                return;
            }

            // Un PDF original no puede dibujarse dentro de un documento QuestPDF, asi
            // que la portada se genera aparte y se fusiona con PdfSharpCore.
            var portada = Document.Create(container =>
            {
                container.Page(pagina => ComponerPagina(pagina, c => ComponerPortada(c, gasto, comprobante)));
            }).GeneratePdf();
            AgregarPaginas(documentoFinal, portada);

            if (File.Exists(rutaFisica))
            {
                AgregarPaginas(documentoFinal, File.ReadAllBytes(rutaFisica));
            }
            else
            {
                var aviso = Document.Create(container =>
                {
                    container.Page(pagina => ComponerPagina(pagina, c =>
                        c.AlignCenter().AlignMiddle().Text("[Archivo adjunto no encontrado en servidor]").FontColor(Colors.Red.Medium)));
                }).GeneratePdf();
                AgregarPaginas(documentoFinal, aviso);
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

        private static void ComponerPagina(PageDescriptor pagina, Action<IContainer> contenido)
        {
            pagina.Size(PageSizes.A4);
            pagina.Margin(1.5f, Unit.Centimetre);
            pagina.PageColor(Colors.White);
            pagina.DefaultTextStyle(x => x.FontSize(10));
            pagina.Content().Element(c => contenido(c));
        }

        private static void ComponerPortada(IContainer container, Gasto gasto, ComprobanteAdjunto comprobante)
        {
            container.Padding(20).Column(col =>
            {
                col.Item().Text("Comprobante de gasto").Bold().FontSize(16).FontColor(Colors.Blue.Darken3);
                col.Item().PaddingTop(4).Text(comprobante.Descripcion).Italic().FontSize(12);

                col.Item().PaddingTop(15).Background(Colors.Grey.Lighten3).Padding(10).Column(datos =>
                {
                    datos.Item().Text(t => { t.Span("Proveedor: ").Bold(); t.Span(gasto.Proveedor); });
                    datos.Item().Text(t => { t.Span("RNC/Cedula: ").Bold(); t.Span(gasto.RNCProveedor); });
                    datos.Item().Text(t => { t.Span("NCF: ").Bold(); t.Span(gasto.NCF); });
                    datos.Item().Text(t => { t.Span("Concepto: ").Bold(); t.Span(gasto.Concepto ?? "-"); });
                    datos.Item().Text(t => { t.Span("Fecha del gasto: ").Bold(); t.Span(gasto.FechaGasto.ToString("dd/MM/yyyy")); });
                    datos.Item().Text(t => { t.Span("Monto total: ").Bold(); t.Span($"RD$ {gasto.MontoTotal:N2}"); });
                });

                col.Item().PaddingTop(10).Text("Archivo original a continuacion:").FontSize(9).Italic().FontColor(Colors.Grey.Darken1);
            });
        }

        private static void ComponerImagen(IContainer container, string rutaFisica)
        {
            if (File.Exists(rutaFisica))
            {
                // FitArea es necesario: sin un modificador de ajuste, QuestPDF puede
                // pedir mas espacio del que el contenedor tiene disponible y lanzar
                // DocumentLayoutException con imagenes grandes.
                container.Padding(20).AlignCenter().MaxHeight(700).Image(rutaFisica).FitArea();
            }
            else
            {
                container.Padding(20).AlignCenter().Text("[Archivo adjunto no encontrado en servidor]").FontColor(Colors.Red.Medium);
            }
        }

        private static byte[] GenerarResumen(SolicitudReposicion solicitud, string nombreCustodio)
        {
            return Document.Create(container =>
            {
                container.Page(pagina =>
                {
                    pagina.Size(PageSizes.A4);
                    pagina.Margin(1.5f, Unit.Centimetre);
                    pagina.PageColor(Colors.White);
                    pagina.DefaultTextStyle(x => x.FontSize(10));

                    pagina.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("CONTROL DE CAJA CHICA").Bold().FontSize(18).FontColor(Colors.Blue.Darken3);
                            col.Item().Text("Expediente Consolidado de Reposicion").FontSize(12).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(150).Column(col =>
                        {
                            col.Item().AlignRight().Text($"Fecha: {solicitud.FechaSolicitud:dd/MM/yyyy}");
                            col.Item().AlignRight().Text($"Solicitud: {solicitud.Id.ToString()[..8]}").FontSize(9);
                        });
                    });

                    pagina.Content().PaddingTop(15).Column(col =>
                    {
                        col.Item().Background(Colors.Grey.Lighten3).Padding(10).Text(t =>
                        {
                            t.Span("Custodio: ").Bold();
                            t.Span(nombreCustodio);
                        });

                        col.Item().PaddingTop(15).Text("Detalle de comprobantes a reponer").Bold().FontSize(12);

                        col.Item().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(75);
                                columns.ConstantColumn(90);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                                columns.ConstantColumn(90);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Fecha").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("NCF").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Proveedor").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Concepto").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Blue.Darken3).Padding(5).AlignRight().Text("Monto").FontColor(Colors.White).Bold();
                            });

                            foreach (var gasto in solicitud.Gastos)
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(gasto.FechaGasto.ToString("dd/MM/yyyy"));
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(gasto.NCF);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(gasto.Proveedor);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(gasto.Concepto ?? "-");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"RD$ {gasto.MontoTotal:N2}");
                            }

                            table.Cell().ColumnSpan(4).Background(Colors.Grey.Lighten3).Padding(6).AlignRight().Text("TOTAL RECLAMADO:").Bold();
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(6).AlignRight()
                                .Text($"RD$ {solicitud.MontoReclamado:N2}").Bold().FontColor(Colors.Blue.Darken3);
                        });
                    });
                });
            }).GeneratePdf();
        }
    }
}

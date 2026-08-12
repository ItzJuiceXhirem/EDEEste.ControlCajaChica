using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EDEEste.ControlCajaChica.Application.Common.Interfaces;
using EDEEste.ControlCajaChica.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EDEEste.ControlCajaChica.Infrastructure.Services
{
    public class QuestPdfReporteService : IReporteGastosService
    {
        public Task<byte[]> GenerarReporteCierreCajaPdfAsync(IEnumerable<Gasto> gastos, decimal balanceFinal)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.Header().Text("Cierre de Caja Chica").SemiBold().FontSize(20);

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("NCF");
                            header.Cell().Text("Fecha");
                            header.Cell().Text("Monto");
                        });

                        foreach (var gasto in gastos)
                        {
                            table.Cell().Text(gasto.NCF);
                            table.Cell().Text(gasto.FechaGasto.ToString("dd/MM/yyyy"));
                            table.Cell().Text(gasto.MontoTotal.ToString("C"));
                        }
                    });

                    page.Footer().Text($"Balance Final: {balanceFinal:C}").FontSize(14).Bold();
                });
            });

            return Task.FromResult(document.GeneratePdf());
        }
    }
}

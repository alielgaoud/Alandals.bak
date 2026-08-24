using Andalos.API.Data;
using Andalos.API.DTOs.Reports;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Andalos.API.Services
{
    public class ReportPdfService
    {
        private readonly IReportService _reportService;

        public ReportPdfService(IReportService reportService)
        {
            _reportService = reportService;
        }

        public async Task<byte[]> GenerateOverdueReportPdfAsync()
        {
            var data = await _reportService.GetOverdueReportAsync();

            var document = Document.Create(container =>
            {
                PdfMasterTemplate.BuildPage(
                    container,
                    "تقرير المتأخرات",
                    $"تاريخ: {DateTime.Now:yyyy/MM/dd}",
                    content => BuildOverdueContent(content, data)
                );
            });

            return document.GeneratePdf();
        }

        public async Task<byte[]> GenerateOccupancyReportPdfAsync()
        {
            var data = await _reportService.GetUnitsOccupancyReportAsync();

            var document = Document.Create(container =>
            {
                PdfMasterTemplate.BuildPage(
                    container,
                    "تقرير إشغال المحلات",
                    $"تاريخ: {DateTime.Now:yyyy/MM/dd}",
                    content => BuildOccupancyContent(content, data)
                );
            });

            return document.GeneratePdf();
        }

        public async Task<byte[]> GenerateFinancialReportPdfAsync(int year)
        {
            var data = await _reportService.GetAnnualFinancialPerformanceAsync(year);

            var document = Document.Create(container =>
            {
                PdfMasterTemplate.BuildPage(
                    container,
                    "التقرير المالي السنوي",
                    $"السنة: {year}",
                    content => BuildFinancialContent(content, data, year)
                );
            });

            return document.GeneratePdf();
        }

        private void BuildOverdueContent(IContainer container, List<OverdueReportItemDto> data)
        {
            container.Column(col =>
            {
                col.Spacing(10);

                // ملخص
                decimal totalOverdue = data.Sum(x => x.RemainingAmount);
                col.Item().Background(PdfMasterTemplate.AccentColor).Padding(12)
                    .Text($"إجمالي المتأخرات: {totalOverdue:N2} د.ل | عدد العقود المتأخرة: {data.Count}")
                    .FontSize(13).Bold().FontColor(PdfMasterTemplate.White).AlignCenter();

                // الجدول
                var headers = new[] { "العقد", "المستأجر", "المحل", "الإيجار", "المستحق", "المدفوع", "المتبقي" };
                var rows = data.Select(x => new[]
                {
                    x.ContractNumber,
                    x.TenantName,
                    x.UnitNumber,
                    $"{x.MonthlyRent:N0}",
                    $"{x.TotalDue:N0}",
                    $"{x.TotalPaid:N0}",
                    $"{x.RemainingAmount:N0}"
                });

                col.Item().Element(c => PdfMasterTemplate.BuildTable(c, headers, rows));
            });
        }

        private void BuildOccupancyContent(IContainer container, List<UnitOccupancyReportDto> data)
        {
            container.Column(col =>
            {
                col.Spacing(10);

                int rented = data.Count(u => u.Status == "Rented");
                int vacant = data.Count(u => u.Status == "Vacant");
                col.Item().Background(PdfMasterTemplate.SecondaryColor).Padding(12)
                    .Text($"إجمالي المحلات: {data.Count} | مؤجر: {rented} | شاغر: {vacant} | نسبة الإشغال: {(data.Count > 0 ? (rented * 100.0 / data.Count) : 0):F1}%")
                    .FontSize(12).Bold().FontColor(PdfMasterTemplate.White).AlignCenter();

                var headers = new[] { "الرقم", "الاسم", "النوع", "الحالة", "المساحة", "المستأجر", "الإيجار" };
                var rows = data.Select(x => new[]
                {
                    x.UnitNumber,
                    x.UnitName,
                    x.UnitType,
                    x.Status,
                    $"{x.Area} م²",
                    x.CurrentTenantName ?? "-",
                    x.CurrentRentAmount.HasValue ? $"{x.CurrentRentAmount:N0}" : "-"
                });

                col.Item().Element(c => PdfMasterTemplate.BuildTable(c, headers, rows));
            });
        }

        private void BuildFinancialContent(IContainer container, List<MonthlyFinancialBarDto> data, int year)
        {
            container.Column(col =>
            {
                col.Spacing(10);

                decimal totalRev = data.Sum(x => x.Revenue);
                decimal totalExp = data.Sum(x => x.Expenses);
                col.Item().Background(PdfMasterTemplate.PrimaryColor).Padding(12)
                    .Text($"إجمالي الإيرادات: {totalRev:N2} | المصروفات: {totalExp:N2} | صافي الربح: {totalRev - totalExp:N2} د.ل")
                    .FontSize(12).Bold().FontColor(PdfMasterTemplate.White).AlignCenter();

                var headers = new[] { "الشهر", "الإيرادات", "المصروفات", "صافي الربح" };
                var rows = data.Select(x => new[]
                {
                    x.MonthName,
                    $"{x.Revenue:N2}",
                    $"{x.Expenses:N2}",
                    $"{x.NetProfit:N2}"
                });

                col.Item().Element(c => PdfMasterTemplate.BuildTable(c, headers, rows));
            });
        }
    }
}
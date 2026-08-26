
using Andalos.API.DTOs.Reports;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using QuestPDF.Fluent;
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

        // =========================================================
        // تقرير المتأخرات
        // =========================================================

        public async Task<byte[]> GenerateOverdueReportPdfAsync()
        {
            var data =
                await _reportService.GetOverdueReportAsync();

            var document = Document.Create(container =>
            {
                PdfMasterTemplate.BuildPage(
                    container,
                    "تقرير المتأخرات",
                    $"تاريخ: {DateTime.Now:yyyy/MM/dd}",
                    content =>
                        BuildOverdueContent(
                            content,
                            data)
                );
            });

            return document.GeneratePdf();
        }

        // =========================================================
        // تقرير الإشغال
        // =========================================================

        public async Task<byte[]> GenerateOccupancyReportPdfAsync()
        {
            var data =
                await _reportService
                    .GetUnitsOccupancyReportAsync();

            var document = Document.Create(container =>
            {
                PdfMasterTemplate.BuildPage(
                    container,
                    "تقرير إشغال المحلات",
                    $"تاريخ: {DateTime.Now:yyyy/MM/dd}",
                    content =>
                        BuildOccupancyContent(
                            content,
                            data)
                );
            });

            return document.GeneratePdf();
        }

        // =========================================================
        // التقرير المالي
        // =========================================================

        public async Task<byte[]> GenerateFinancialReportPdfAsync(
            int year)
        {
            var data =
                await _reportService
                    .GetAnnualFinancialPerformanceAsync(year);

            var document = Document.Create(container =>
            {
                PdfMasterTemplate.BuildPage(
                    container,
                    "التقرير المالي السنوي",
                    $"السنة: {year}",
                    content =>
                        BuildFinancialContent(
                            content,
                            data,
                            year)
                );
            });

            return document.GeneratePdf();
        }

        // =========================================================
        // تقرير المتأخرات
        // =========================================================

        private void BuildOverdueContent(
            IContainer container,
            List<OverdueReportItemDto> data)
        {
            container.Column(col =>
            {
                col.Spacing(12);

                decimal totalOverdue =
                    data.Sum(x => x.RemainingAmount);

                // Summary
                col.Item().Row(row =>
                {
                    row.RelativeItem()
                        .Element(c =>
                            SummaryCard(
                                c,
                                "إجمالي المتأخرات",
                                $"{totalOverdue:N2} د.ل"));

                    row.ConstantItem(10);

                    row.RelativeItem()
                        .Element(c =>
                            SummaryCard(
                                c,
                                "العقود المتأخرة",
                                data.Count.ToString("N0")));
                });

                // Table
                PdfMasterTemplate.SectionTitle(
                    col.Item(),
                    "تفاصيل المتأخرات");

                var headers = new[]
                {
                    "العقد",
                    "المستأجر",
                    "المحل",
                    "الإيجار",
                    "المستحق",
                    "المدفوع",
                    "المتبقي"
                };

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

                col.Item()
                    .Element(c =>
                        PdfMasterTemplate.BuildTable(
                            c,
                            headers,
                            rows));
            });
        }

        // =========================================================
        // تقرير الإشغال
        // =========================================================

        private void BuildOccupancyContent(
            IContainer container,
            List<UnitOccupancyReportDto> data)
        {
            container.Column(col =>
            {
                col.Spacing(12);

                int rented =
                    data.Count(u => u.Status == "Rented");

                int vacant =
                    data.Count(u => u.Status == "Vacant");

                double occupancyRate =
                    data.Count > 0
                        ? rented * 100.0 / data.Count
                        : 0;

                // Summary
                col.Item().Row(row =>
                {
                    row.RelativeItem()
                        .Element(c =>
                            SummaryCard(
                                c,
                                "إجمالي المحلات",
                                data.Count.ToString("N0")));

                    row.ConstantItem(8);

                    row.RelativeItem()
                        .Element(c =>
                            SummaryCard(
                                c,
                                "مؤجر",
                                rented.ToString("N0")));

                    row.ConstantItem(8);

                    row.RelativeItem()
                        .Element(c =>
                            SummaryCard(
                                c,
                                "شاغر",
                                vacant.ToString("N0")));

                    row.ConstantItem(8);

                    row.RelativeItem()
                        .Element(c =>
                            SummaryCard(
                                c,
                                "نسبة الإشغال",
                                $"{occupancyRate:F1}%"));
                });

                // Table
                PdfMasterTemplate.SectionTitle(
                    col.Item(),
                    "تفاصيل الإشغال");

                var headers = new[]
                {
                    "الرقم",
                    "الاسم",
                    "نوع النشاط", // 👈 تحديث التسمية العربية لتطابق نوع النشاط
                    "الحالة",
                    "المساحة",
                    "المستأجر",
                    "الإيجار"
                };

                var rows = data.Select(x => new[]
                {
                    x.UnitNumber,
                    x.UnitName ?? "-",
                    x.ActivityType, // 👈 تم تصحيح الخطأ الأول هنا من UnitType إلى ActivityType
                    x.Status,
                    $"{x.Area:N2} م²",
                    x.CurrentTenantName ?? "-",
                    x.CurrentRentAmount.HasValue
                        ? $"{x.CurrentRentAmount:N0}"
                        : "-"
                });

                col.Item()
                    .Element(c =>
                        PdfMasterTemplate.BuildTable(
                            c,
                            headers,
                            rows));
            });
        }

        // =========================================================
        // التقرير المالي
        // =========================================================

        private void BuildFinancialContent(
            IContainer container,
            List<MonthlyFinancialBarDto> data,
            int year)
        {
            container.Column(col =>
            {
                col.Spacing(12);

                decimal totalRevenue =
                    data.Sum(x => x.Revenue);

                decimal totalExpenses =
                    data.Sum(x => x.Expenses);

                decimal netProfit =
                    totalRevenue - totalExpenses;

                // Summary
                col.Item().Row(row =>
                {
                    row.RelativeItem()
                        .Element(c =>
                            SummaryCard(
                                c,
                                "إجمالي الإيرادات",
                                $"{totalRevenue:N2} د.ل"));

                    row.ConstantItem(10);

                    row.RelativeItem()
                        .Element(c =>
                            SummaryCard(
                                c,
                                "إجمالي المصروفات",
                                $"{totalExpenses:N2} د.ل"));

                    row.ConstantItem(10);

                    row.RelativeItem()
                        .Element(c =>
                            SummaryCard(
                                c,
                                "صافي الربح",
                                $"{netProfit:N2} د.ل"));
                });

                // Table
                PdfMasterTemplate.SectionTitle(
                    col.Item(),
                    $"الأداء المالي الشهري - {year}");

                var headers = new[]
                {
                    "الشهر",
                    "الإيرادات",
                    "المصروفات",
                    "صافي الربح"
                };

                var rows = data.Select(x => new[]
                {
                    x.MonthName,
                    $"{x.Revenue:N2}",
                    $"{x.Expenses:N2}",
                    $"{x.NetProfit:N2}"
                });

                col.Item()
                    .Element(c =>
                        PdfMasterTemplate.BuildTable(
                            c,
                            headers,
                            rows));
            });
        }

        // =========================================================
        // Summary Card
        // =========================================================

        private static void SummaryCard(
            IContainer container,
            string label,
            string value)
        {
            container
                .Background(PdfMasterTemplate.LightGray)
                .Border(0.8f)
                .BorderColor(PdfMasterTemplate.BorderGray)
                .Padding(10)
                .Column(c =>
                {
                    c.Item()
                        .Text(label)
                        .FontSize(8)
                        .FontColor(PdfMasterTemplate.Gray)
                        .AlignCenter();

                    c.Item()
                        .PaddingTop(5)
                        .Text(value)
                        .FontSize(14)
                        .Bold()
                        .FontColor(PdfMasterTemplate.Black)
                        .AlignCenter();
                });
        }
    }
}

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Andalos.API.Helpers
{
    /// <summary>
    /// القالب الرئيسي الموحد لجميع مستندات منظومة الأندلس.
    /// التصميم: Minimal / Professional / Black & White
    /// </summary>
    public static class PdfMasterTemplate
    {
        // =========================================================
        // نظام الألوان الموحد
        // التصميم الرسمي: Black & White فقط
        // =========================================================
        public const string Black = "#000000";
        public const string White = "#FFFFFF";

        // =========================================================
        // Compatibility Aliases
        // =========================================================
        public const string Gray = Black;
        public const string DarkGray = Black;
        public const string BorderGray = Black;
        public const string LightGray = White;

        // =========================================================
        // بيانات الشركة
        // =========================================================
        public static string CompanyName = "الأندلس للاستثمار السياحي";
        public static string CompanyPhone = "0925288883";
        public static string CompanyEmail = "info@andalos.ly";
        public static string CompanyAddress = "Tripoli, Libya";

        // =========================================================
        // بناء الصفحة الموحدة
        // =========================================================
        public static void BuildPage(
            IDocumentContainer container,
            string documentTitle,
            string documentNumber,
            Action<IContainer> contentBuilder)
        {
            container.Page(page =>
            {
                // -------------------------------------------------
                // حجم الصفحة والهوامش
                // -------------------------------------------------
                page.Size(PageSizes.A4);
                page.MarginHorizontal(45);
                page.MarginVertical(32);

                // -------------------------------------------------
                // الخط الافتراضي
                // -------------------------------------------------
                page.DefaultTextStyle(x => x
                    .FontFamily("Arial")
                    .FontSize(10)
                    .FontColor(Black)
                    .DirectionFromRightToLeft());

                // -------------------------------------------------
                // Header
                // -------------------------------------------------
                page.Header()
                    .Element(c => BuildHeader(
                        c,
                        documentTitle,
                        documentNumber));

                // -------------------------------------------------
                // Content
                // -------------------------------------------------
                page.Content()
                    .PaddingTop(18)
                    .Element(contentBuilder);

                // -------------------------------------------------
                // Footer
                // -------------------------------------------------
                page.Footer()
                    .PaddingTop(12)
                    .Element(BuildFooter);
            });
        }

        // =========================================================
        // Header
        // =========================================================
        private static void BuildHeader(
            IContainer container,
            string title,
            string number)
        {
            container.Column(column =>
            {
                column.Item()
                    .MinHeight(72)
                    .Row(row =>
                    {
                        // =================================================
                        // بيانات الشركة - الجهة اليسرى
                        // =================================================
                        row.RelativeItem()
                            .Column(company =>
                            {
                                company.Item()
                                    .Row(logoRow =>
                                    {
                                        logoRow.ConstantItem(38)
                                            .AlignMiddle()
                                            .Text("A")
                                            .FontFamily("Arial")
                                            .FontSize(32)
                                            .Bold()
                                            .FontColor(Black);

                                        logoRow.RelativeItem()
                                            .PaddingLeft(8)
                                            .Column(info =>
                                            {
                                                info.Item()
                                                    .Text(CompanyName)
                                                    .FontFamily("Arial")
                                                    .FontSize(17)
                                                    .Bold()
                                                    .FontColor(Black);

                                                info.Item()
                                                    .PaddingTop(5)
                                                    .Text($"{CompanyPhone}   •   {CompanyEmail}")
                                                    .FontFamily("Arial")
                                                    .FontSize(8)
                                                    .FontColor(Black);

                                                info.Item()
                                                    .Text(CompanyAddress)
                                                    .FontFamily("Arial")
                                                    .FontSize(8)
                                                    .FontColor(Black);
                                            });
                                    });
                            });

                        // =================================================
                        // بيانات المستند - الجهة اليمنى (معدلة)
                        // =================================================
                        row.ConstantItem(220)
                            .AlignRight()
                            .Column(document =>
                            {
                                if (!string.IsNullOrWhiteSpace(title))
                                {
                                    document.Item()
                                        .AlignRight()
                                        .Text(title)
                                        .FontFamily("Arial")
                                        .FontSize(16)
                                        .Bold()
                                        .FontColor(Black);
                                }

                                // 👈 يظهر فقط إذا كان رقم المستند غير فارغ
                                if (!string.IsNullOrWhiteSpace(number))
                                {
                                    document.Item()
                                        .PaddingTop(4)
                                        .AlignRight()
                                        .Text($"رقم المستند: {number}")
                                        .FontFamily("Arial")
                                        .FontSize(9)
                                        .FontColor(Black);
                                }
                            });
                    });

                // -------------------------------------------------
                // الخط الرئيسي أسفل الـ Header
                // -------------------------------------------------
                column.Item()
                    .PaddingTop(10)
                    .LineHorizontal(1.2f)
                    .LineColor(Black);
            });
        }

        // =========================================================
        // Footer (تم إزالة المربعات السوداء)
        // =========================================================
        private static void BuildFooter(IContainer container)
        {
            container.Column(column =>
            {
                column.Item()
                    .LineHorizontal(0.8f)
                    .LineColor(Black);

                column.Item()
                    .PaddingTop(7)
                    .MinHeight(22)
                    .Row(row =>
                    {
                        // =================================================
                        // بيانات التواصل
                        // =================================================
                        row.RelativeItem()
                            .AlignLeft()
                            .Row(contact =>
                            {
                                contact.AutoItem()
                                    .Text(CompanyPhone)
                                    .FontFamily("Arial")
                                    .FontSize(7.5f)
                                    .FontColor(Black);

                                contact.ConstantItem(16)
                                    .AlignCenter()
                                    .Text("│")
                                    .FontSize(7)
                                    .FontColor(Black);

                                contact.AutoItem()
                                    .Text(CompanyEmail)
                                    .FontFamily("Arial")
                                    .FontSize(7.5f)
                                    .FontColor(Black);

                                contact.ConstantItem(16)
                                    .AlignCenter()
                                    .Text("│")
                                    .FontSize(7)
                                    .FontColor(Black);

                                contact.AutoItem()
                                    .Text(CompanyAddress)
                                    .FontFamily("Arial")
                                    .FontSize(7.5f)
                                    .FontColor(Black);
                            });

                        // =================================================
                        // رقم الصفحة
                        // =================================================
                        row.ConstantItem(100)
                            .AlignRight()
                            .Text(text =>
                            {
                                text.Span("الصفحة ")
                                    .FontFamily("Arial")
                                    .FontSize(7.5f)
                                    .FontColor(Black);

                                text.CurrentPageNumber()
                                    .FontFamily("Arial")
                                    .FontSize(7.5f)
                                    .Bold()
                                    .FontColor(Black);

                                text.Span(" من ")
                                    .FontFamily("Arial")
                                    .FontSize(7.5f)
                                    .FontColor(Black);

                                text.TotalPages()
                                    .FontFamily("Arial")
                                    .FontSize(7.5f)
                                    .Bold()
                                    .FontColor(Black);
                            });
                    });
            });
        }

        // =========================================================
        // جدول موحد
        // =========================================================
        public static void BuildTable(
            IContainer container,
            string[] headers,
            IEnumerable<string[]> rows,
            float[]? columnWidths = null)
        {
            container.Table(table =>
            {
                int colCount = headers.Length;

                table.ColumnsDefinition(columns =>
                {
                    if (columnWidths != null && columnWidths.Length == colCount)
                    {
                        for (int i = 0; i < colCount; i++)
                            columns.ConstantColumn(columnWidths[i]);
                    }
                    else
                    {
                        for (int i = 0; i < colCount; i++)
                            columns.RelativeColumn();
                    }
                });

                foreach (var header in headers)
                {
                    table.Cell()
                        .Element(HeaderCell)
                        .Text(header)
                        .FontFamily("Arial")
                        .FontSize(9)
                        .Bold()
                        .FontColor(White);
                }

                foreach (var row in rows)
                {
                    foreach (var cell in row)
                    {
                        table.Cell()
                            .Element(DataCell)
                            .Text(cell)
                            .FontFamily("Arial")
                            .FontSize(8.5f)
                            .FontColor(Black);
                    }
                }
            });
        }

        private static IContainer HeaderCell(IContainer container)
        {
            return container
                .Background(Black)
                .Border(0.6f)
                .BorderColor(Black)
                .PaddingVertical(7)
                .PaddingHorizontal(7)
                .AlignCenter()
                .AlignMiddle();
        }

        private static IContainer DataCell(IContainer container)
        {
            return container
                .Background(White)
                .BorderBottom(0.5f)
                .BorderColor(Black)
                .PaddingVertical(7)
                .PaddingHorizontal(7)
                .AlignMiddle();
        }

        public static void SectionTitle(
            IContainer container,
            string title)
        {
            container
                .PaddingTop(14)
                .PaddingBottom(7)
                .BorderBottom(1f)
                .BorderColor(Black)
                .Text(title)
                .FontFamily("Arial")
                .FontSize(12)
                .Bold()
                .FontColor(Black);
        }

        public static void InfoBox(
            IContainer container,
            string title,
            string value)
        {
            container
                .Border(0.7f)
                .BorderColor(Black)
                .Padding(10)
                .Background(White)
                .Column(column =>
                {
                    column.Item()
                        .Text(title)
                        .FontFamily("Arial")
                        .FontSize(8)
                        .FontColor(Black);

                    column.Item()
                        .PaddingTop(4)
                        .Text(value)
                        .FontFamily("Arial")
                        .FontSize(10)
                        .Bold()
                        .FontColor(Black);
                });
        }
    }
}
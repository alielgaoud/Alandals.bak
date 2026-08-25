using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Andalos.API.Helpers
{
    public static class PdfMasterTemplate
    {
        // =========================================================
        // الألوان - أبيض وأسود فقط
        // =========================================================
        public static readonly string Black = "#111111";
        public static readonly string DarkGray = "#333333";
        public static readonly string Gray = "#666666";
        public static readonly string LightGray = "#F5F5F5";
        public static readonly string BorderGray = "#D9D9D9";
        public static readonly string White = "#FFFFFF";

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
                page.Size(PageSizes.A4);

                // هوامش متوازنة
                page.MarginHorizontal(45);
                page.MarginVertical(35);

                // الخط الافتراضي
                page.DefaultTextStyle(x => x
                    .FontFamily("Arial")
                    .FontSize(10)
                    .FontColor(DarkGray)
                    .DirectionFromRightToLeft());

                // Header
                page.Header()
                    .Element(c => BuildHeader(
                        c,
                        documentTitle,
                        documentNumber));

                // Content
                page.Content()
                    .PaddingTop(15)
                    .Element(contentBuilder);

                // Footer
                page.Footer()
                    .PaddingTop(10)
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
            container.Column(col =>
            {
                // السطر الرئيسي
                col.Item().Row(row =>
                {
                    // بيانات الشركة
                    row.RelativeItem().Column(company =>
                    {
                        company.Item()
                            .Text(CompanyName)
                            .FontSize(17)
                            .Bold()
                            .FontColor(Black);

                        company.Item()
                            .PaddingTop(4)
                            .Text($"{CompanyPhone}  •  {CompanyEmail}")
                            .FontSize(8)
                            .FontColor(Gray);

                        company.Item()
                            .Text(CompanyAddress)
                            .FontSize(8)
                            .FontColor(Gray);
                    });

                    // معلومات المستند
                    row.ConstantItem(170)
                        .AlignRight()
                        .Column(document =>
                        {
                            document.Item()
                                .AlignRight()
                                .Text(title)
                                .FontSize(16)
                                .Bold()
                                .FontColor(Black);

                            document.Item()
                                .PaddingTop(5)
                                .AlignRight()
                                .Text(number)
                                .FontSize(9)
                                .FontColor(Gray);
                        });
                });

                // خط بسيط تحت الهيدر
                col.Item()
                    .PaddingTop(12)
                    .LineHorizontal(1)
                    .LineColor(Black);
            });
        }

        // =========================================================
        // Footer
        // =========================================================
        private static void BuildFooter(IContainer container)
        {
            container.Column(col =>
            {
                col.Item()
                    .LineHorizontal(0.7f)
                    .LineColor(BorderGray);

                col.Item()
                    .PaddingTop(6)
                    .Row(row =>
                    {
                        row.RelativeItem()
                            .Text("منظومة الأندلس الذكية لإدارة المجمعات الاستثمارية")
                            .FontSize(7)
                            .FontColor(Gray);

                        row.RelativeItem()
                            .AlignRight()
                            .Text(text =>
                            {
                                text.Span("صفحة ")
                                    .FontSize(7)
                                    .FontColor(Gray);

                                text.CurrentPageNumber()
                                    .FontSize(7)
                                    .FontColor(Black)
                                    .Bold();

                                text.Span(" / ")
                                    .FontSize(7)
                                    .FontColor(Gray);

                                text.TotalPages()
                                    .FontSize(7)
                                    .FontColor(Black)
                                    .Bold();
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

                // -------------------------------------------------
                // تعريف الأعمدة
                // -------------------------------------------------
                table.ColumnsDefinition(columns =>
                {
                    if (columnWidths != null &&
                        columnWidths.Length == colCount)
                    {
                        for (int i = 0; i < colCount; i++)
                        {
                            columns.ConstantColumn(columnWidths[i]);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < colCount; i++)
                        {
                            columns.RelativeColumn();
                        }
                    }
                });

                // -------------------------------------------------
                // Header
                // -------------------------------------------------
                foreach (var header in headers)
                {
                    table.Cell()
                        .Element(HeaderCell)
                        .Text(header)
                        .FontSize(9)
                        .Bold()
                        .FontColor(White);
                }

                // -------------------------------------------------
                // Rows
                // -------------------------------------------------
                bool isEven = false;

                foreach (var row in rows)
                {
                    foreach (var cell in row)
                    {
                        table.Cell()
                            .Element(c =>
                                DataCell(
                                    c,
                                    isEven))
                            .Text(cell)
                            .FontSize(8.5f)
                            .FontColor(DarkGray);
                    }

                    isEven = !isEven;
                }
            });
        }

        // =========================================================
        // تنسيق Header Cell
        // =========================================================
        private static IContainer HeaderCell(IContainer container)
        {
            return container
                .Background(Black)
                .Border(0.5f)
                .BorderColor(Black)
                .PaddingVertical(7)
                .PaddingHorizontal(6)
                .AlignCenter()
                .AlignMiddle();
        }

        // =========================================================
        // تنسيق Data Cell
        // =========================================================
        private static IContainer DataCell(
            IContainer container,
            bool isEven)
        {
            return container
                .Background(isEven ? LightGray : White)
                .BorderBottom(0.5f)
                .BorderColor(BorderGray)
                .PaddingVertical(6)
                .PaddingHorizontal(6)
                .AlignMiddle();
        }

        // =========================================================
        // عنوان قسم داخل المستند
        // =========================================================
        public static void SectionTitle(
            IContainer container,
            string title)
        {
            container
                .PaddingTop(12)
                .PaddingBottom(7)
                .BorderBottom(1)
                .BorderColor(Black)
                .Text(title)
                .FontSize(12)
                .Bold()
                .FontColor(Black);
        }

        // =========================================================
        // بطاقة معلومات بسيطة
        // =========================================================
        public static void InfoBox(
            IContainer container,
            string title,
            string value)
        {
            container
                .Border(0.7f)
                .BorderColor(BorderGray)
                .Padding(10)
                .Column(column =>
                {
                    column.Item()
                        .Text(title)
                        .FontSize(8)
                        .FontColor(Gray);

                    column.Item()
                        .PaddingTop(3)
                        .Text(value)
                        .FontSize(10)
                        .Bold()
                        .FontColor(Black);
                });
        }
    }
}
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Andalos.API.Helpers
{
    public static class PdfMasterTemplate
    {
        // ===== الألوان الموحدة (ألوان متناسقة تناسب الهوية العربية) =====
        public static readonly string PrimaryColor = "#1B4F72";    // أزرق داكن
        public static readonly string SecondaryColor = "#2E86C1";  // أزرق فاتح
        public static readonly string AccentColor = "#E74C3C";     // أحمر للمتأخرات
        public static readonly string LightGray = "#F8F9F9";       // خلفية رمادية خفيفة
        public static readonly string DarkText = "#2C3E50";        // لون النص الأساسي
        public static readonly string White = "#FFFFFF";

        // ===== بيانات الشركة =====
        public static string CompanyName = "الأندلس للاستثمار السياحي";
        public static string CompanyPhone = "0925288883";
        public static string CompanyEmail = "info@andalos.ly";
        public static string CompanyAddress = "Tripoli, Libya";

        // ===== بناء هيكل الصفحة الموحد =====
        public static void BuildPage(
            IDocumentContainer container, // 👈 تصحيح نوع الواجهة البرمجية هنا
            string documentTitle,
            string documentNumber,
            Action<IContainer> contentBuilder)
        {
            container.Page(page =>
            {
                // إعدادات حجم الصفحة والهوامش والخط الافتراضي الداعم للعربية
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x
                    .FontFamily("Arial")
                    .FontSize(11)
                    .FontColor(DarkText)
                    .DirectionFromRightToLeft()); // 👈 دعم اتجاه القراءة العربي

                // الهيدر الموحد
                page.Header().Element(c => BuildHeader(c, documentTitle, documentNumber));

                // المحتوى المتغير لكل مستند
                page.Content().Element(contentBuilder);

                // الفوتر الموحد
                page.Footer().Element(BuildFooter);
            });
        }

        // ===== الهيدر الموحد =====
        private static void BuildHeader(IContainer container, string title, string number)
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    // يسار الهيدر: بيانات وشعار الشركة
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(CompanyName)
                            .FontSize(18)
                            .Bold()
                            .FontColor(PrimaryColor);

                        c.Item().Text($"هاتف: {CompanyPhone}  |  بريد: {CompanyEmail}")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken2);

                        c.Item().Text(CompanyAddress)
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken2);
                    });

                    // يمين الهيدر: عنوان المستند والترقيم التلقائي
                    row.ConstantItem(180).Column(c =>
                    {
                        // 👈 تصحيح الخطأ: وضع الخلفية والحشو قبل كتابة النص
                        c.Item()
                         .Background(PrimaryColor)
                         .Padding(6)
                         .AlignCenter()
                         .Text(title)
                         .FontSize(14)
                         .Bold()
                         .FontColor(White);

                        c.Item()
                         .PaddingVertical(4)
                         .AlignCenter()
                         .Text(number)
                         .FontSize(11)
                         .Bold()
                         .FontColor(SecondaryColor);
                    });
                });

                // خط فاصل بتصميم أنيق أسفل الهيدر
                col.Item().PaddingVertical(10)
                    .LineHorizontal(2)
                    .LineColor(PrimaryColor);
            });
        }

        // ===== الفوتر الموحد =====
        private static void BuildFooter(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                col.Item().PaddingVertical(5).Row(row =>
                {
                    row.RelativeItem().Text("منظومة الأندلس الذكية لإدارة المجمعات الاستثمارية")
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken1);

                    row.RelativeItem().AlignRight()
                        .Text(text =>
                        {
                            text.Span("صفحة ").FontSize(8).FontColor(Colors.Grey.Darken1);
                            text.CurrentPageNumber().FontSize(8).FontColor(PrimaryColor).Bold();
                            text.Span(" من ").FontSize(8).FontColor(Colors.Grey.Darken1);
                            text.TotalPages().FontSize(8).FontColor(PrimaryColor).Bold();
                        });
                });
            });
        }

        // ===== بناء الجداول الموحدة =====
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

                // 1. رأس الجدول (Header)
                foreach (var header in headers)
                {
                    // 👈 تصحيح الخطأ: استدعاء Cell عادي مع تطبيق التنسيق بدلاً من دالة HeaderCell غير المعرفة
                    table.Cell()
                         .Element(CellStyle)
                         .Text(header)
                         .Bold()
                         .FontColor(White);
                }

                // 2. صفوف الجدول (Data Rows)
                bool isEven = false;
                foreach (var row in rows)
                {
                    foreach (var cell in row)
                    {
                        table.Cell()
                            .Element(c => isEven ? c.Background(LightGray).Padding(6) : c.Padding(6))
                            .Text(cell)
                            .FontSize(9);
                    }
                    isEven = !isEven;
                }
            });
        }

        private static IContainer CellStyle(IContainer c) =>
            c.Background(PrimaryColor).Padding(6).AlignCenter().AlignMiddle();
    }
}
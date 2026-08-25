
using Andalos.API.Data;
using Andalos.API.Helpers;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Andalos.API.Services
{
    public class ReceiptPdfService
    {
        private readonly AppDbContext _db;

        public ReceiptPdfService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<byte[]> GenerateReceiptPdfAsync(int paymentId)
        {
            var payment = await _db.Payments
                .Include(p => p.Contract)
                    .ThenInclude(c => c!.Tenant)
                .Include(p => p.Contract)
                    .ThenInclude(c => c!.Unit)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
                throw new KeyNotFoundException(
                    "الدفعة غير موجودة");

            var document = Document.Create(container =>
            {
                PdfMasterTemplate.BuildPage(
                    container,
                    "سند قبض",
                    payment.ReceiptNumber,
                    content => BuildReceiptContent(
                        content,
                        payment)
                );
            });

            return document.GeneratePdf();
        }

        // =========================================================
        // محتوى سند القبض
        // =========================================================

        private void BuildReceiptContent(
            IContainer container,
            Payment payment)
        {
            container.Column(col =>
            {
                col.Spacing(12);

                // =================================================
                // عنوان
                // =================================================

                col.Item()
                    .AlignCenter()
                    .Text("سند قبض")
                    .FontSize(20)
                    .Bold()
                    .FontColor(PdfMasterTemplate.Black);

                col.Item()
                    .AlignCenter()
                    .Text($"رقم السند: {payment.ReceiptNumber}")
                    .FontSize(9)
                    .FontColor(PdfMasterTemplate.Gray);

                // =================================================
                // بيانات السند
                // =================================================

                PdfMasterTemplate.SectionTitle(
                    col.Item(),
                    "بيانات السند");

                col.Item()
                    .Row(row =>
                    {
                        row.RelativeItem()
                            .Element(c =>
                                InfoBox(
                                    c,
                                    "التاريخ",
                                    payment.PaymentDate
                                        .ToString("yyyy/MM/dd")));

                        row.ConstantItem(10);

                        row.RelativeItem()
                            .Element(c =>
                                InfoBox(
                                    c,
                                    "طريقة الدفع",
                                    payment.PaymentMethod.ToString()));
                    });

                col.Item()
                    .Row(row =>
                    {
                        row.RelativeItem()
                            .Element(c =>
                                InfoBox(
                                    c,
                                    "نوع الدفعة",
                                    payment.PaymentType.ToString()));

                        row.ConstantItem(10);

                        row.RelativeItem()
                            .Element(c =>
                                InfoBox(
                                    c,
                                    "رقم العقد",
                                    payment.Contract?.ContractNumber
                                        ?? "-"));
                    });

                // =================================================
                // بيانات المستأجر والمحل
                // =================================================

                PdfMasterTemplate.SectionTitle(
                    col.Item(),
                    "بيانات العملية");

                col.Item()
                    .Row(row =>
                    {
                        row.RelativeItem()
                            .Element(c =>
                                InfoBox(
                                    c,
                                    "المستأجر",
                                    payment.Contract?.Tenant?.FullName
                                        ?? "-"));

                        row.ConstantItem(10);

                        row.RelativeItem()
                            .Element(c =>
                                InfoBox(
                                    c,
                                    "المحل",
                                    payment.Contract?.Unit?.UnitNumber
                                        ?? "-"));
                    });

                // =================================================
                // المرجع
                // =================================================

                if (!string.IsNullOrWhiteSpace(
                    payment.ReferenceNumber))
                {
                    col.Item()
                        .Element(c =>
                            InfoBox(
                                c,
                                "رقم التحويل / المرجع",
                                payment.ReferenceNumber));
                }

                // =================================================
                // المبلغ
                // =================================================

                col.Item()
                    .PaddingTop(10)
                    .Border(1.2f)
                    .BorderColor(PdfMasterTemplate.Black)
                    .Padding(18)
                    .Column(c =>
                    {
                        c.Item()
                            .AlignCenter()
                            .Text("المبلغ الإجمالي")
                            .FontSize(9)
                            .FontColor(PdfMasterTemplate.Gray);

                        c.Item()
                            .PaddingTop(5)
                            .AlignCenter()
                            .Text($"{payment.Amount:N3} د.ل")
                            .FontSize(25)
                            .Bold()
                            .FontColor(PdfMasterTemplate.Black);
                    });

                // =================================================
                // ملاحظات
                // =================================================

                if (!string.IsNullOrWhiteSpace(payment.Notes))
                {
                    PdfMasterTemplate.SectionTitle(
                        col.Item(),
                        "البيان");

                    col.Item()
                        .Background(PdfMasterTemplate.LightGray)
                        .Border(0.5f)
                        .BorderColor(PdfMasterTemplate.BorderGray)
                        .Padding(10)
                        .Text(payment.Notes)
                        .FontSize(10)
                        .FontColor(PdfMasterTemplate.DarkGray)
                        .LineHeight(1.5f);
                }

                // =================================================
                // التوقيعات
                // =================================================

                col.Item()
                    .PaddingTop(45)
                    .Row(row =>
                    {
                        row.RelativeItem()
                            .Column(c =>
                            {
                                c.Item()
                                    .Text("المستلم")
                                    .Bold()
                                    .AlignCenter();

                                c.Item()
                                    .PaddingTop(45)
                                    .LineHorizontal(0.8f)
                                    .LineColor(PdfMasterTemplate.BorderGray);

                                c.Item()
                                    .PaddingTop(5)
                                    .Text("التوقيع والختم")
                                    .FontSize(8)
                                    .FontColor(PdfMasterTemplate.Gray)
                                    .AlignCenter();
                            });

                        row.ConstantItem(60);

                        row.RelativeItem()
                            .Column(c =>
                            {
                                c.Item()
                                    .Text("المدفوع")
                                    .Bold()
                                    .AlignCenter();

                                c.Item()
                                    .PaddingTop(45)
                                    .LineHorizontal(0.8f)
                                    .LineColor(PdfMasterTemplate.BorderGray);

                                c.Item()
                                    .PaddingTop(5)
                                    .Text("التوقيع")
                                    .FontSize(8)
                                    .FontColor(PdfMasterTemplate.Gray)
                                    .AlignCenter();
                            });
                    });
            });
        }

        // =========================================================
        // Info Box
        // =========================================================

        private static void InfoBox(
            IContainer container,
            string label,
            string value)
        {
            container
                .Background(PdfMasterTemplate.LightGray)
                .Border(0.7f)
                .BorderColor(PdfMasterTemplate.BorderGray)
                .Padding(9)
                .Column(c =>
                {
                    c.Item()
                        .Text(label)
                        .FontSize(8)
                        .FontColor(PdfMasterTemplate.Gray);

                    c.Item()
                        .PaddingTop(3)
                        .Text(value)
                        .FontSize(10)
                        .Bold()
                        .FontColor(PdfMasterTemplate.DarkGray);
                });
        }
    }
}


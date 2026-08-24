using Andalos.API.Data;
using Andalos.API.Helpers;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
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
                throw new KeyNotFoundException("الدفعة غير موجودة");

            var document = Document.Create(container =>
            {
                PdfMasterTemplate.BuildPage(
                    container,
                    "سند قبض",
                    payment.ReceiptNumber,
                    content => BuildReceiptContent(content, payment)
                );
            });

            return document.GeneratePdf();
        }

        private void BuildReceiptContent(IContainer container, Payment payment)
        {
            container.Column(col =>
            {
                col.Spacing(15);

                // معلومات السند
                col.Item().Row(row =>
                {
                    row.RelativeItem().Element(c => InfoBox(c, "التاريخ", payment.PaymentDate.ToString("yyyy/MM/dd")));
                    row.RelativeItem().Element(c => InfoBox(c, "طريقة الدفع", payment.PaymentMethod.ToString()));
                });

                col.Item().Row(row =>
                {
                    row.RelativeItem().Element(c => InfoBox(c, "المستأجر", payment.Contract?.Tenant?.FullName ?? ""));
                    row.RelativeItem().Element(c => InfoBox(c, "المحل", payment.Contract?.Unit?.UnitNumber ?? ""));
                });

                col.Item().Row(row =>
                {
                    row.RelativeItem().Element(c => InfoBox(c, "رقم العقد", payment.Contract?.ContractNumber ?? ""));
                    row.RelativeItem().Element(c => InfoBox(c, "نوع الدفعة", payment.PaymentType.ToString()));
                });

                if (!string.IsNullOrEmpty(payment.ReferenceNumber))
                {
                    col.Item().Element(c => InfoBox(c, "رقم التحويل/المرجع", payment.ReferenceNumber));
                }

                // المبلغ
                col.Item().PaddingVertical(15)
                    .Background(PdfMasterTemplate.PrimaryColor)
                    .Padding(20)
                    .AlignCenter()
                    .Text($"{payment.Amount:N3} د.ل")
                    .FontSize(28)
                    .Bold()
                    .FontColor(PdfMasterTemplate.White);

                col.Item().AlignCenter()
                    .Text("المبلغ الإجمالي")
                    .FontSize(12)
                    .FontColor(Colors.Grey.Darken1);

                // ملاحظات
                if (!string.IsNullOrEmpty(payment.Notes))
                {
                    col.Item().PaddingTop(10)
                        .Background(PdfMasterTemplate.LightGray)
                        .Padding(10)
                        .Text($"البيان: {payment.Notes}")
                        .FontSize(11);
                }

                // التوقيعات
                col.Item().PaddingTop(50).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("المستلم").Bold().AlignCenter();
                        c.Item().PaddingTop(40).LineHorizontal(1);
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("المدفوع").Bold().AlignCenter();
                        c.Item().PaddingTop(40).LineHorizontal(1);
                    });
                });
            });
        }

        private void InfoBox(IContainer container, string label, string value)
        {
            container.Background(PdfMasterTemplate.LightGray)
                .Padding(8)
                .Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Column(c =>
                {
                    c.Item().Text(label).FontSize(9).FontColor(Colors.Grey.Darken1);
                    c.Item().Text(value).FontSize(12).Bold();
                });
        }
    }
}
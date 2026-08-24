using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Andalos.API.Services
{
    public class ContractPdfService
    {
        private readonly AppDbContext _db;
        private readonly ISettingService _settings; // 👈 جديد

        public ContractPdfService(AppDbContext db, ISettingService settings)
        {
            _db = db;
            _settings = settings;
        }

        public async Task<byte[]> GenerateContractPdfAsync(int contractId)
        {
            var contract = await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .Include(c => c.ContractItems)
                .FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract == null)
                throw new KeyNotFoundException("العقد غير موجود");

            // 👈 قراءة كل النصوص من الإعدادات
            var templateTitle = await _settings.GetValueAsync(SettingKeys.ContractTemplateTitle) ?? "عقد إيجار";
            var intro = await _settings.GetValueAsync(SettingKeys.ContractTemplateIntro) ?? "";
            var landlordLabel = await _settings.GetValueAsync(SettingKeys.ContractLandlordLabel) ?? "المؤجر";
            var tenantLabel = await _settings.GetValueAsync(SettingKeys.ContractTenantLabel) ?? "المستأجر";
            var unitSectionTitle = await _settings.GetValueAsync(SettingKeys.ContractUnitSectionTitle) ?? "بيانات المحل";
            var termsSectionTitle = await _settings.GetValueAsync(SettingKeys.ContractTermsSectionTitle) ?? "شروط الإيجار";
            var paymentSectionTitle = await _settings.GetValueAsync(SettingKeys.ContractPaymentSectionTitle) ?? "قيمة الإيجار";
            var clauses = await _settings.GetValueAsync(SettingKeys.ContractClauses) ?? "";
            var signatureLandlord = await _settings.GetValueAsync(SettingKeys.ContractSignatureLandlord) ?? "المؤجر";
            var signatureTenant = await _settings.GetValueAsync(SettingKeys.ContractSignatureTenant) ?? "المستأجر";
            var footerNote = await _settings.GetValueAsync(SettingKeys.ContractFooterNote) ?? "";
            var showWitnesses = await _settings.GetValueAsync<bool>(SettingKeys.ContractShowWitnesses, false);

            // استبدال الرموز في المقدمة
            intro = intro
                .Replace("{Date}", contract.StartDate.ToString("yyyy/MM/dd"))
                .Replace("{HijriDate}", ""); // يمكن إضافة تحويل هجري لاحقاً

            // استبدال الرموز في البنود
            var graceDays = await _settings.GetValueAsync(SettingKeys.RentGraceDays) ?? "5";
            clauses = clauses.Replace("{GraceDays}", graceDays);

            var document = Document.Create(container =>
            {
                PdfMasterTemplate.BuildPage(
                    container,
                    templateTitle,
                    contract.ContractNumber,
                    content => BuildContractContent(
                        content, contract,
                        intro, landlordLabel, tenantLabel,
                        unitSectionTitle, termsSectionTitle, paymentSectionTitle,
                        clauses, signatureLandlord, signatureTenant,
                        footerNote, showWitnesses
                    )
                );
            });

            return document.GeneratePdf();
        }

        private void BuildContractContent(
            IContainer container, Contract contract,
            string intro, string landlordLabel, string tenantLabel,
            string unitSectionTitle, string termsSectionTitle, string paymentSectionTitle,
            string clauses, string signatureLandlord, string signatureTenant,
            string footerNote, bool showWitnesses)
        {
            container.Column(col =>
            {
                col.Spacing(12);

                // ===== المقدمة =====
                col.Item().Text(intro).FontSize(12).LineHeight(1.6f);

                // ===== الأطراف =====
                col.Item().Row(row =>
                {
                    row.RelativeItem().Element(c => InfoBox(c, landlordLabel,
                        $"{PdfMasterTemplate.CompanyName}\n" +
                        $"هاتف: {PdfMasterTemplate.CompanyPhone}"));

                    row.RelativeItem().Element(c => InfoBox(c, tenantLabel,
                        $"{contract.Tenant?.FullName}\n" +
                        $"هوية: {contract.Tenant?.NationalId}\n" +
                        $"هاتف: {contract.Tenant?.Phone}"));
                });

                // ===== بيانات المحل =====
                col.Item().Text(unitSectionTitle).FontSize(14).Bold().FontColor(PdfMasterTemplate.PrimaryColor);

                col.Item().Row(row =>
                {
                    row.RelativeItem().Element(c => InfoBox(c, "رقم المحل", contract.Unit?.UnitNumber ?? ""));
                    row.RelativeItem().Element(c => InfoBox(c, "اسم المحل", contract.Unit?.UnitName ?? ""));
                });

                col.Item().Row(row =>
                {
                    row.RelativeItem().Element(c => InfoBox(c, "المساحة", $"{contract.Unit?.Area} م²"));
                    row.RelativeItem().Element(c => InfoBox(c, "الموقع", $"{contract.Unit?.Building} - {contract.Unit?.Floor}"));
                });

                // ===== شروط الإيجار =====
                col.Item().Text(termsSectionTitle).FontSize(14).Bold().FontColor(PdfMasterTemplate.PrimaryColor);

                col.Item().Row(row =>
                {
                    row.RelativeItem().Element(c => InfoBox(c, "تاريخ البدء", contract.StartDate.ToString("yyyy/MM/dd")));
                    row.RelativeItem().Element(c => InfoBox(c, "تاريخ الانتهاء", contract.EndDate.ToString("yyyy/MM/dd")));
                });

                // ===== قيمة الإيجار =====
                col.Item().Text(paymentSectionTitle).FontSize(14).Bold().FontColor(PdfMasterTemplate.PrimaryColor);

                col.Item().Row(row =>
                {
                    row.RelativeItem().Element(c => InfoBox(c, "قيمة الإيجار", $"{contract.RentAmount:N2} د.ل / {contract.RentCycle}"));
                    row.RelativeItem().Element(c => InfoBox(c, "العربون", $"{contract.DepositAmount:N2} د.ل"));
                });

                col.Item().Row(row =>
                {
                    row.RelativeItem().Element(c => InfoBox(c, "التجديد التلقائي", contract.AutoRenew ? "نعم" : "لا"));
                    row.RelativeItem().Element(c => InfoBox(c, "حالة العقد", contract.Status.ToString()));
                });

                // البنود الإضافية
                if (contract.ContractItems.Any())
                {
                    var headers = new[] { "#", "البند", "المبلغ", "ملاحظات" };
                    var rows = contract.ContractItems.Select((item, i) => new[]
                    {
                        (i + 1).ToString(),
                        item.ItemName,
                        $"{item.Amount:N2} د.ل",
                        item.Notes ?? "-"
                    });
                    col.Item().Element(c => PdfMasterTemplate.BuildTable(c, headers, rows));
                }

                // ===== البنود القانونية من الإعدادات =====
                if (!string.IsNullOrEmpty(clauses))
                {
                    col.Item().PaddingTop(10)
                        .Text("البنود والشروط").FontSize(14).Bold().FontColor(PdfMasterTemplate.PrimaryColor);

                    // تقسيم البنود حسب الأسطر
                    var lines = clauses.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        col.Item().Text(line.Trim()).FontSize(11).LineHeight(1.5f);
                    }
                }

                // ===== التوقيعات =====
                col.Item().PaddingTop(30).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(signatureLandlord).Bold().AlignCenter();
                        c.Item().PaddingTop(50).LineHorizontal(1).LineColor(Colors.Grey.Darken2);
                        c.Item().AlignCenter().Text("التوقيع والختم").FontSize(9).FontColor(Colors.Grey.Darken1);
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(signatureTenant).Bold().AlignCenter();
                        c.Item().PaddingTop(50).LineHorizontal(1).LineColor(Colors.Grey.Darken2);
                        c.Item().AlignCenter().Text("التوقيع").FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                });

                // الشهود (اختياري من الإعدادات)
                if (showWitnesses)
                {
                    var witnessLabel = _settings.GetValueAsync(SettingKeys.ContractWitnessLabel).Result ?? "الشهود";
                    col.Item().PaddingTop(20).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"{witnessLabel} - الأول").Bold().AlignCenter();
                            c.Item().PaddingTop(40).LineHorizontal(1);
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"{witnessLabel} - الثاني").Bold().AlignCenter();
                            c.Item().PaddingTop(40).LineHorizontal(1);
                        });
                    });
                }

                // ===== ملاحظة التذييل =====
                if (!string.IsNullOrEmpty(footerNote))
                {
                    col.Item().PaddingTop(15)
                        .Background(PdfMasterTemplate.LightGray)
                        .Padding(10)
                        .Text(footerNote)
                        .FontSize(10).Italic();
                }
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
                    c.Item().Text(value).FontSize(12).Bold().FontColor(PdfMasterTemplate.DarkText);
                });
        }
    }
}
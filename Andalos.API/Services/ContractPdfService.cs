
using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Andalos.API.Services
{
    public class ContractPdfService
    {
        private readonly AppDbContext _db;
        private readonly ISettingService _settings;

        public ContractPdfService(
            AppDbContext db,
            ISettingService settings)
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

            // =====================================================
            // إعدادات قالب العقد
            // =====================================================

            var templateTitle =
                await _settings.GetValueAsync(
                    SettingKeys.ContractTemplateTitle)
                ?? "عقد إيجار";

            var intro =
                await _settings.GetValueAsync(
                    SettingKeys.ContractTemplateIntro)
                ?? "";

            var landlordLabel =
                await _settings.GetValueAsync(
                    SettingKeys.ContractLandlordLabel)
                ?? "المؤجر";

            var tenantLabel =
                await _settings.GetValueAsync(
                    SettingKeys.ContractTenantLabel)
                ?? "المستأجر";

            var unitSectionTitle =
                await _settings.GetValueAsync(
                    SettingKeys.ContractUnitSectionTitle)
                ?? "بيانات المحل";

            var termsSectionTitle =
                await _settings.GetValueAsync(
                    SettingKeys.ContractTermsSectionTitle)
                ?? "شروط الإيجار";

            var paymentSectionTitle =
                await _settings.GetValueAsync(
                    SettingKeys.ContractPaymentSectionTitle)
                ?? "قيمة الإيجار";

            var clauses =
                await _settings.GetValueAsync(
                    SettingKeys.ContractClauses)
                ?? "";

            var signatureLandlord =
                await _settings.GetValueAsync(
                    SettingKeys.ContractSignatureLandlord)
                ?? "المؤجر";

            var signatureTenant =
                await _settings.GetValueAsync(
                    SettingKeys.ContractSignatureTenant)
                ?? "المستأجر";

            var footerNote =
                await _settings.GetValueAsync(
                    SettingKeys.ContractFooterNote)
                ?? "";

            var showWitnesses =
                await _settings.GetValueAsync<bool>(
                    SettingKeys.ContractShowWitnesses,
                    false);

            // =====================================================
            // استبدال الرموز
            // =====================================================

            intro = intro
                .Replace(
                    "{Date}",
                    contract.StartDate.ToString("yyyy/MM/dd"))
                .Replace("{HijriDate}", "");

            var graceDays =
                await _settings.GetValueAsync(
                    SettingKeys.RentGraceDays)
                ?? "5";

            clauses = clauses.Replace(
                "{GraceDays}",
                graceDays);

            // =====================================================
            // إنشاء المستند
            // =====================================================

            var document = Document.Create(container =>
            {
                PdfMasterTemplate.BuildPage(
                    container,
                    templateTitle,
                    contract.ContractNumber,
                    content => BuildContractContent(
                        content,
                        contract,
                        intro,
                        landlordLabel,
                        tenantLabel,
                        unitSectionTitle,
                        termsSectionTitle,
                        paymentSectionTitle,
                        clauses,
                        signatureLandlord,
                        signatureTenant,
                        footerNote,
                        showWitnesses
                    )
                );
            });

            return document.GeneratePdf();
        }

        // =========================================================
        // محتوى العقد
        // =========================================================

        private void BuildContractContent(
            IContainer container,
            Contract contract,
            string intro,
            string landlordLabel,
            string tenantLabel,
            string unitSectionTitle,
            string termsSectionTitle,
            string paymentSectionTitle,
            string clauses,
            string signatureLandlord,
            string signatureTenant,
            string footerNote,
            bool showWitnesses)
        {
            container.Column(col =>
            {
                col.Spacing(12);

                // =================================================
                // المقدمة
                // =================================================

                if (!string.IsNullOrWhiteSpace(intro))
                {
                    col.Item()
                        .PaddingBottom(5)
                        .Text(intro)
                        .FontSize(11)
                        .LineHeight(1.7f)
                        .FontColor(PdfMasterTemplate.DarkGray);
                }

                // =================================================
                // أطراف العقد
                // =================================================

                PdfMasterTemplate.SectionTitle(
                    col.Item(),
                    "أطراف العقد");

                col.Item()
                    .Row(row =>
                    {
                        row.RelativeItem()
                            .Element(c =>
                                InfoBox(
                                    c,
                                    landlordLabel,
                                    $"{PdfMasterTemplate.CompanyName}\n" +
                                    $"هاتف: {PdfMasterTemplate.CompanyPhone}"
                                ));

                        row.ConstantItem(10);

                        row.RelativeItem()
                            .Element(c =>
                                InfoBox(
                                    c,
                                    tenantLabel,
                                    $"{contract.Tenant?.FullName ?? "-"}\n" +
                                    $"هوية: {contract.Tenant?.NationalId ?? "-"}\n" +
                                    $"هاتف: {contract.Tenant?.Phone ?? "-"}"
                                ));
                    });

                // =================================================
                // بيانات المحل
                // =================================================

                PdfMasterTemplate.SectionTitle(
                    col.Item(),
                    unitSectionTitle);

                col.Item()
                    .Row(row =>
                    {
                        row.RelativeItem()
                            .Element(c =>
                                InfoBox(
                                    c,
                                    "رقم المحل",
                                    contract.Unit?.UnitNumber ?? "-"
                                ));

                        row.ConstantItem(10);

                        row.RelativeItem()
                            .Element(c =>
                                InfoBox(
                                    c,
                                    "اسم المحل",
                                    contract.Unit?.UnitName ?? "-"
                                ));
                    });

                col.Item()
                    .Row(row =>
                    {
                        row.RelativeItem()
                            .Element(c =>
                                InfoBox(
                                    c,
                                    "المساحة",
                                    $"{contract.Unit?.Area ?? 0:N2} م²"
                                ));

                        row.ConstantItem(10);

                        row.RelativeItem()
                            .Element(c =>
                                InfoBox(
                                    c,
                                    "الموقع",
                                    $"{contract.Unit?.Building ?? "-"} - " +
                                    $"{contract.Unit?.Floor ?? "-"}"
                                ));
                    });

                // =================================================
                // شروط الإيجار
                // =================================================

                PdfMasterTemplate.SectionTitle(
                    col.Item(),
                    termsSectionTitle);

                col.Item()
                    .Row(row =>
                    {
                        row.RelativeItem()
                            .Element(c =>
                                InfoBox(
                                    c,
                                    "تاريخ البدء",
                                    contract.StartDate
                                        .ToString("yyyy/MM/dd")
                                ));

                        row.ConstantItem(10);

                        row.RelativeItem()
                            .Element(c =>
                                InfoBox(
                                    c,
                                    "تاريخ الانتهاء",
                                    contract.EndDate
                                        .ToString("yyyy/MM/dd")
                                ));
                    });

                // =================================================
                // القيمة المالية
                // =================================================

                PdfMasterTemplate.SectionTitle(
                    col.Item(),
                    paymentSectionTitle);

                col.Item()
                    .Row(row =>
                    {
                        row.RelativeItem()
                            .Element(c =>
                                InfoBox(
                                    c,
                                    "قيمة الإيجار",
                                    $"{contract.RentAmount:N2} د.ل / {contract.RentCycle}"
                                ));

                        row.ConstantItem(10);

                        row.RelativeItem()
                            .Element(c =>
                                InfoBox(
                                    c,
                                    "العربون",
                                    $"{contract.DepositAmount:N2} د.ل"
                                ));
                    });

                col.Item()
                    .Row(row =>
                    {
                        row.RelativeItem()
                            .Element(c =>
                                InfoBox(
                                    c,
                                    "التجديد التلقائي",
                                    contract.AutoRenew
                                        ? "نعم"
                                        : "لا"
                                ));

                        row.ConstantItem(10);

                        row.RelativeItem()
                            .Element(c =>
                                InfoBox(
                                    c,
                                    "حالة العقد",
                                    contract.Status.ToString()
                                ));
                    });

                // =================================================
                // البنود الإضافية
                // =================================================

                if (contract.ContractItems.Any())
                {
                    PdfMasterTemplate.SectionTitle(
                        col.Item(),
                        "البنود الإضافية");

                    var headers = new[]
                    {
                        "#",
                        "البند",
                        "المبلغ",
                        "ملاحظات"
                    };

                    var rows =
                        contract.ContractItems
                            .Select((item, i) => new[]
                            {
                                (i + 1).ToString(),
                                item.ItemName,
                                $"{item.Amount:N2} د.ل",
                                item.Notes ?? "-"
                            });

                    col.Item()
                        .Element(c =>
                            PdfMasterTemplate.BuildTable(
                                c,
                                headers,
                                rows));
                }

                // =================================================
                // البنود القانونية
                // =================================================

                if (!string.IsNullOrWhiteSpace(clauses))
                {
                    PdfMasterTemplate.SectionTitle(
                        col.Item(),
                        "البنود والشروط");

                    var lines = clauses.Split(
                        '\n',
                        StringSplitOptions.RemoveEmptyEntries);

                    foreach (var line in lines)
                    {
                        col.Item()
                            .PaddingBottom(4)
                            .Text(line.Trim())
                            .FontSize(10)
                            .LineHeight(1.6f)
                            .FontColor(PdfMasterTemplate.DarkGray);
                    }
                }

                // =================================================
                // التوقيعات
                // =================================================

                col.Item()
                    .PaddingTop(25)
                    .Row(row =>
                    {
                        row.RelativeItem()
                            .Column(c =>
                            {
                                c.Item()
                                    .Text(signatureLandlord)
                                    .Bold()
                                    .AlignCenter()
                                    .FontColor(PdfMasterTemplate.Black);

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

                        row.ConstantItem(40);

                        row.RelativeItem()
                            .Column(c =>
                            {
                                c.Item()
                                    .Text(signatureTenant)
                                    .Bold()
                                    .AlignCenter()
                                    .FontColor(PdfMasterTemplate.Black);

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

                // =================================================
                // الشهود
                // =================================================

                if (showWitnesses)
                {
                    var witnessLabel =
                        awaitWitnessLabel();

                    col.Item()
                        .PaddingTop(20)
                        .Row(row =>
                        {
                            row.RelativeItem()
                                .Column(c =>
                                {
                                    c.Item()
                                        .Text($"{witnessLabel} - الأول")
                                        .Bold()
                                        .AlignCenter();

                                    c.Item()
                                        .PaddingTop(35)
                                        .LineHorizontal(0.8f)
                                        .LineColor(PdfMasterTemplate.BorderGray);
                                });

                            row.ConstantItem(40);

                            row.RelativeItem()
                                .Column(c =>
                                {
                                    c.Item()
                                        .Text($"{witnessLabel} - الثاني")
                                        .Bold()
                                        .AlignCenter();

                                    c.Item()
                                        .PaddingTop(35)
                                        .LineHorizontal(0.8f)
                                        .LineColor(PdfMasterTemplate.BorderGray);
                                });
                        });
                }

                // =================================================
                // ملاحظة العقد
                // =================================================

                if (!string.IsNullOrWhiteSpace(footerNote))
                {
                    col.Item()
                        .PaddingTop(15)
                        .Background(PdfMasterTemplate.LightGray)
                        .Border(0.5f)
                        .BorderColor(PdfMasterTemplate.BorderGray)
                        .Padding(10)
                        .Text(footerNote)
                        .FontSize(9)
                        .Italic()
                        .FontColor(PdfMasterTemplate.Gray);
                }
            });
        }

        // =========================================================
        // الحصول على تسمية الشهود
        // =========================================================

        private string awaitWitnessLabel()
        {
            return _settings
                .GetValueAsync(
                    SettingKeys.ContractWitnessLabel)
                .GetAwaiter()
                .GetResult()
                ?? "الشهود";
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
                        .FontColor(PdfMasterTemplate.DarkGray)
                        .LineHeight(1.4f);
                });
        }
    }
}

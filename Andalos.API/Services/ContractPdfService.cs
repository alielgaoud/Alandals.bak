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
        private readonly ISettingService _settings;
        public ContractPdfService(AppDbContext db, ISettingService settings){ _db=db; _settings=settings; }

        private string TranslateRentCycle(string cycle)
        {
            if (string.IsNullOrWhiteSpace(cycle)) return "شهري";
            var c = cycle.Trim().ToLower();
            return c switch
            {
                "monthly" => "شهري",
                "quarterly" => "ربع سنوي",
                "semiannually" => "نصف سنوي",
                "semi_annually" => "نصف سنوي",
                "annually" => "سنوي",
                "annual" => "سنوي",
                "yearly" => "سنوي",
                "1" => "شهري",
                "2" => "ربع سنوي",
                "3" => "نصف سنوي",
                "4" => "سنوي",
                _ => cycle
            };
        }

        private string FormatDuration(int months)
        {
            if (months <= 0) return "غير محددة";
            if (months < 12) return $"{months} شهر";
            int years = months / 12;
            int rem = months % 12;
            string yLabel = years switch { 1 => "سنة", 2 => "سنتان", 3 => "ثلاث سنوات", _ => $"{years} سنوات" };
            return rem == 0 ? yLabel : $"{yLabel} و {rem} شهر";
        }

        private string TranslateActivity(string act, string tradeName)
        {
            if (!string.IsNullOrWhiteSpace(tradeName)) return tradeName;
            if (string.IsNullOrWhiteSpace(act)) return "تجاري";
            var a = act.Trim();
            if (a.Equals("Other", StringComparison.OrdinalIgnoreCase)) return "تجاري";
            if (a.Equals("Restaurant", StringComparison.OrdinalIgnoreCase)) return "مطعم";
            if (a.Equals("Cafe", StringComparison.OrdinalIgnoreCase)) return "مقهى";
            if (a.Equals("Clothing", StringComparison.OrdinalIgnoreCase)) return "ملابس";
            if (a.Equals("Pharmacy", StringComparison.OrdinalIgnoreCase)) return "صيدلية";
            if (a.Equals("Supermarket", StringComparison.OrdinalIgnoreCase)) return "سوبر ماركت";
            if (a.Equals("Electronics", StringComparison.OrdinalIgnoreCase)) return "إلكترونيات";
            if (a.Equals("Salon", StringComparison.OrdinalIgnoreCase)) return "صالون";
            if (a.Equals("Office", StringComparison.OrdinalIgnoreCase)) return "مكتب";
            return a;
        }

        public async Task<byte[]> GenerateContractPdfAsync(int contractId)
        {
            var companyInfo = await _settings.GetCompanyInfoAsync();
            var allSettings = await _settings.GetAllSettingsDictionaryAsync();
            var showLogo = await _settings.GetValueAsync<bool>(SettingKeys.PdfShowLogo, true);
            PdfMasterTemplate.ConfigureFromCompanyInfo(companyInfo, showLogo, true, true, true);
            PdfMasterTemplate.ConfigureFromSettingsDictionary(allSettings);

            var contract = await _db.Contracts.Include(c=>c.Tenant).Include(c=>c.Unit).Include(c=>c.ContractItems.Where(i=>i.IsActive)).Include(c=>c.ContractFees.Where(f=>f.IsActive)).FirstOrDefaultAsync(c=>c.Id==contractId);
            if(contract==null) throw new KeyNotFoundException("العقد غير موجود");

            var title = await _settings.GetValueAsync(SettingKeys.ContractTemplateTitle) ?? "عقد إيجار محل تجاري";
            var intro = await _settings.GetValueAsync(SettingKeys.ContractTemplateIntro) ?? "";
            var clauses = await _settings.GetValueAsync(SettingKeys.ContractClauses) ?? "";
            var sigLandlord = await _settings.GetValueAsync(SettingKeys.ContractSignatureLandlord) ?? "الطرف الأول";
            var sigTenant = await _settings.GetValueAsync(SettingKeys.ContractSignatureTenant) ?? "الطرف الثاني";
            var footerNote = await _settings.GetValueAsync(SettingKeys.ContractFooterNote) ?? "";
            var showWitnesses = await _settings.GetValueAsync<bool>(SettingKeys.ContractShowWitnesses, false);
            var dateStr = contract.StartDate.ToString("yyyy/MM/dd");

            var grace = await _settings.GetValueAsync(SettingKeys.RentGraceDays) ?? "5";
            clauses = clauses.Replace("{GraceDays}", grace);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    // لا نستخدم DirectionFromRightToLeft على مستوى الصفحة لتجنب انقلاب الأرقام
                    page.DefaultTextStyle(x=>x.FontFamily("Arial").FontSize(10).FontColor("#111"));

                    // إطار جمالي مزدوج
                    page.Content().Border(1.5f).BorderColor("#000").Padding(2).Border(0.5f).BorderColor("#777").Padding(18)
                        .Element(c=>Build(c, contract, intro, clauses, sigLandlord, sigTenant, footerNote, showWitnesses, title, dateStr));
                });
            });

            return document.GeneratePdf();
        }

        private void Build(IContainer container, Contract contract, string intro, string clauses, string sigLandlord, string sigTenant, string footerNote, bool showWitnesses, string title, string dateStr)
        {
            string rentAr = TranslateRentCycle(contract.RentCycle.ToString());
            int months = Math.Max(1,(int)((contract.EndDate-contract.StartDate).TotalDays/30));
            string duration = FormatDuration(months);
            string activity = TranslateActivity(contract.ActivityType.ToString(), contract.TradeName ?? "");
            string unitNo = contract.Unit?.UnitNumber ?? "-";

            container.Column(col=>
            {
                col.Spacing(4);

                // Header: شركة يمين - عنوان وسط - تاريخ يسار لكن كله محاذاة يمين ل RTL
                col.Item().Row(row=>
                {
                    row.RelativeItem().Column(c=>
                    {
                        c.Item().AlignRight().Text(PdfMasterTemplate.CompanyName).FontSize(11).Bold().DirectionFromRightToLeft();
                        c.Item().AlignRight().Text(PdfMasterTemplate.CompanyPhone).FontSize(8).FontColor("#333").DirectionFromRightToLeft();
                    });
                    row.ConstantItem(100).AlignCenter().Column(c=>
                    {
                        c.Item().AlignCenter().Text(title).FontSize(14).Bold().DirectionFromRightToLeft();
                        c.Item().AlignCenter().Text(contract.ContractNumber).FontSize(9).Bold().DirectionFromRightToLeft();
                    });
                    row.RelativeItem().AlignLeft().Column(c=>
                    {
                        c.Item().AlignLeft().Text(dateStr).FontSize(8).DirectionFromRightToLeft();
                        if(PdfMasterTemplate.ShowLogo)
                        {
                            try{
                                string webRoot=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"wwwroot");
                                if(!Directory.Exists(webRoot)) webRoot=Path.Combine(Directory.GetCurrentDirectory(),"wwwroot");
                                string lp=PdfMasterTemplate.GetLogoPhysicalPath(webRoot);
                                if(!string.IsNullOrWhiteSpace(lp)&&File.Exists(lp)) c.Item().AlignLeft().PaddingTop(2).Width(35).Image(lp).FitArea();
                            }catch{}
                        }
                    });
                });

                // مقدمة
                if(!string.IsNullOrWhiteSpace(intro))
                {
                    col.Item().PaddingTop(8).AlignRight().Text(intro).FontSize(9).LineHeight(1.4f).DirectionFromRightToLeft();
                }

                // أطراف العقد - الأول يمين الثاني يسار بدون خطوط
                col.Item().PaddingTop(10).AlignRight().Text("أطراف العقد").FontSize(11).Bold().DirectionFromRightToLeft();

                col.Item().PaddingTop(4).Row(row=>
                {
                    // يمين: الطرف الأول
                    row.RelativeItem().Column(c=>
                    {
                        c.Item().AlignRight().Text("الطرف الأول المؤجر").FontSize(8).Bold().FontColor("#000").DirectionFromRightToLeft();
                        c.Item().AlignRight().Text(PdfMasterTemplate.CompanyName).FontSize(9).Bold().DirectionFromRightToLeft();
                        c.Item().AlignRight().Text($"هاتف {PdfMasterTemplate.CompanyPhone}").FontSize(8).DirectionFromRightToLeft();
                    });
                    // يسار: الطرف الثاني
                    row.RelativeItem().Column(c=>
                    {
                        c.Item().AlignRight().Text("الطرف الثاني المستأجر").FontSize(8).Bold().FontColor("#000").DirectionFromRightToLeft();
                        c.Item().AlignRight().Text(contract.Tenant?.FullName ?? "-").FontSize(9).Bold().DirectionFromRightToLeft();
                        c.Item().AlignRight().Text($"هوية {contract.Tenant?.NationalId ?? "-"}").FontSize(8).DirectionFromRightToLeft();
                        c.Item().AlignRight().Text($"هاتف {contract.Tenant?.Phone ?? "-"}").FontSize(8).DirectionFromRightToLeft();
                    });
                });

                // بيانات المحل: رقم المحل والنشاط فقط - بدون خطوط
                col.Item().PaddingTop(10).AlignRight().Text("بيانات المحل").FontSize(11).Bold().DirectionFromRightToLeft();
                col.Item().PaddingTop(2).Row(row=>
                {
                    row.ConstantItem(120).AlignRight().Text($"رقم المحل {unitNo}").FontSize(9).Bold().DirectionFromRightToLeft();
                    row.RelativeItem().AlignRight().Text($"النشاط {activity}").FontSize(9).Bold().DirectionFromRightToLeft();
                });

                // المدة
                col.Item().PaddingTop(8).AlignRight().Text("مدة الإيجار").FontSize(11).Bold().DirectionFromRightToLeft();
                col.Item().AlignRight().Text($"من {contract.StartDate:yyyy/MM/dd} إلى {contract.EndDate:yyyy/MM/dd}").FontSize(9).DirectionFromRightToLeft();
                col.Item().AlignRight().Text($"المدة {duration} - السداد {rentAr}").FontSize(9).DirectionFromRightToLeft();

                // القيمة المالية
                col.Item().PaddingTop(8).AlignRight().Text("القيمة المالية").FontSize(11).Bold().DirectionFromRightToLeft();
                col.Item().AlignRight().Text($"الإيجار {contract.RentAmount:N2} {PdfMasterTemplate.CurrencySymbol} لكل {rentAr}").FontSize(10).Bold().DirectionFromRightToLeft();
                col.Item().AlignRight().Text($"العربون {contract.DepositAmount:N2} {PdfMasterTemplate.CurrencySymbol}").FontSize(9).DirectionFromRightToLeft();
                if(contract.AnnualIncreasePercentage.HasValue)
                    col.Item().AlignRight().Text($"الزيادة السنوية {contract.AnnualIncreasePercentage}%").FontSize(8).DirectionFromRightToLeft();

                // بنود إضافية بدون أرقام لتجنب انقلاب
                if(contract.ContractItems.Any())
                {
                    col.Item().PaddingTop(6).AlignRight().Text("بنود إضافية").FontSize(10).Bold().DirectionFromRightToLeft();
                    foreach(var item in contract.ContractItems)
                    {
                        col.Item().AlignRight().Text($"- {item.ItemName} {item.Amount:N2} {PdfMasterTemplate.CurrencySymbol}").FontSize(8).DirectionFromRightToLeft();
                    }
                }

                // رسوم - جدول بسيط بدون خطوط كثيرة
                if(contract.ContractFees.Any())
                {
                    col.Item().PaddingTop(6).AlignRight().Text("الرسوم").FontSize(10).Bold().DirectionFromRightToLeft();
                    col.Item().Table(t=>
                    {
                        t.ColumnsDefinition(c=>{ c.RelativeColumn(3); c.RelativeColumn(1); c.RelativeColumn(1); });
                        t.Cell().Element(CellHeader).AlignRight().Text("البيان").FontSize(7).Bold().DirectionFromRightToLeft();
                        t.Cell().Element(CellHeader).AlignRight().Text("النوع").FontSize(7).Bold().DirectionFromRightToLeft();
                        t.Cell().Element(CellHeader).AlignRight().Text("المبلغ").FontSize(7).Bold().DirectionFromRightToLeft();
                        foreach(var fee in contract.ContractFees)
                        {
                            decimal calc=fee.CalculateActualAmount(contract.RentAmount, contract.RentAmount*months);
                            t.Cell().Element(CellBody).AlignRight().Text(fee.FeeName).FontSize(7).DirectionFromRightToLeft();
                            t.Cell().Element(CellBody).AlignRight().Text(fee.Frequency==Enums.FeeFrequency.OneTime?"مرة واحدة":"شهري").FontSize(7).DirectionFromRightToLeft();
                            t.Cell().Element(CellBody).AlignRight().Text($"{calc:N2} {PdfMasterTemplate.CurrencySymbol}").FontSize(7).DirectionFromRightToLeft();
                        }
                    });
                }

                // الشروط - بدون ترقيم رقمي لتجنب انقلاب، نستخدم شرطة
                if(!string.IsNullOrWhiteSpace(clauses))
                {
                    col.Item().PaddingTop(8).AlignRight().Text("الشروط والأحكام").FontSize(11).Bold().DirectionFromRightToLeft();
                    var lines=clauses.Split('\n',StringSplitOptions.RemoveEmptyEntries).Take(10);
                    foreach(var line in lines)
                    {
                        var clean=line.Trim();
                        if(string.IsNullOrWhiteSpace(clean)) continue;
                        // إزالة الأرقام من البداية لتجنب انقلاب
                        clean = System.Text.RegularExpressions.Regex.Replace(clean, @"^\d+[\-\.\)]\s*", "");
                        col.Item().PaddingTop(2).AlignRight().Text($"- {clean}").FontSize(8).LineHeight(1.3f).DirectionFromRightToLeft();
                    }
                }

                // مسافة تدفع التوقيعات للأسفل ثابتة
                col.Item().Extend();

                // التوقيعات ثابتة في الأسفل
                col.Item().PaddingTop(25).Row(row=>
                {
                    row.RelativeItem().Column(c=>
                    {
                        c.Item().AlignCenter().Text(sigLandlord).FontSize(9).Bold().DirectionFromRightToLeft();
                        c.Item().PaddingTop(30).LineHorizontal(0.6f).LineColor("#000");
                        c.Item().PaddingTop(4).AlignCenter().Text("التوقيع والختم").FontSize(7).FontColor("#666").DirectionFromRightToLeft();
                    });
                    row.ConstantItem(80);
                    row.RelativeItem().Column(c=>
                    {
                        c.Item().AlignCenter().Text(sigTenant).FontSize(9).Bold().DirectionFromRightToLeft();
                        c.Item().PaddingTop(30).LineHorizontal(0.6f).LineColor("#000");
                        c.Item().PaddingTop(4).AlignCenter().Text("التوقيع").FontSize(7).FontColor("#666").DirectionFromRightToLeft();
                    });
                });

                if(showWitnesses)
                {
                    col.Item().PaddingTop(18).Row(row=>
                    {
                        row.RelativeItem().Column(c=>{ c.Item().AlignCenter().Text("الشاهد الأول").FontSize(8).Bold().DirectionFromRightToLeft(); c.Item().PaddingTop(24).LineHorizontal(0.4f).LineColor("#000"); });
                        row.ConstantItem(80);
                        row.RelativeItem().Column(c=>{ c.Item().AlignCenter().Text("الشاهد الثاني").FontSize(8).Bold().DirectionFromRightToLeft(); c.Item().PaddingTop(24).LineHorizontal(0.4f).LineColor("#000"); });
                    });
                }

                if(!string.IsNullOrWhiteSpace(footerNote))
                {
                    col.Item().PaddingTop(10).AlignCenter().Text(footerNote).FontSize(6.5f).FontColor("#666").DirectionFromRightToLeft();
                }
            });
        }

        private IContainer CellHeader(IContainer c)=>c.BorderBottom(0.6f).BorderColor("#000").PaddingVertical(3).PaddingHorizontal(4).Background("#EEE");
        private IContainer CellBody(IContainer c)=>c.BorderBottom(0.2f).BorderColor("#DDD").PaddingVertical(3).PaddingHorizontal(4);
    }
}

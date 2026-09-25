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
                "monthly" => "شهري", "quarterly" => "ربع سنوي",
                "semiannually" => "نصف سنوي", "semi_annually" => "نصف سنوي",
                "annually" => "سنوي", "annual" => "سنوي", "yearly" => "سنوي",
                "1" => "شهري", "2" => "ربع سنوي", "3" => "نصف سنوي", "4" => "سنوي",
                _ => cycle
            };
        }

        private string FormatDuration(int months)
        {
            if (months <= 0) return "غير محددة";
            if (months < 12) return $"{months} شهر";
            int y = months / 12; int r = months % 12;
            string yl = y switch { 1 => "سنة", 2 => "سنتان", 3 => "ثلاث سنوات", _ => $"{y} سنوات" };
            return r == 0 ? yl : $"{yl} و {r} شهر";
        }

        private string TranslateActivity(string act, string trade)
        {
            if (!string.IsNullOrWhiteSpace(trade)) return trade;
            if (string.IsNullOrWhiteSpace(act)) return "تجاري";
            var a = act.Trim();
            if (a.Equals("Other", StringComparison.OrdinalIgnoreCase)) return "تجاري";
            if (a.Equals("Restaurant", StringComparison.OrdinalIgnoreCase)) return "مطعم";
            if (a.Equals("Cafe", StringComparison.OrdinalIgnoreCase)) return "مقهى";
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
                    page.Margin(18); // هامش صغير لاستغلال المساحة
                    page.DefaultTextStyle(x=>x.FontFamily("Arial").FontSize(11).FontColor("#000"));

                    // إطار جمالي رفيع واحد فقط - بدون فراغات
                    page.Content().Border(1f).BorderColor("#000").Padding(14)
                        .Element(c=>BuildUX(c, contract, intro, clauses, sigLandlord, sigTenant, footerNote, showWitnesses, title, dateStr));
                });
            });

            return document.GeneratePdf();
        }

        private void BuildUX(IContainer container, Contract contract, string intro, string clauses, string sigLandlord, string sigTenant, string footerNote, bool showWitnesses, string title, string dateStr)
        {
            string rentAr = TranslateRentCycle(contract.RentCycle.ToString());
            int months = Math.Max(1,(int)((contract.EndDate-contract.StartDate).TotalDays/30));
            string duration = FormatDuration(months);
            string activity = TranslateActivity(contract.ActivityType.ToString(), contract.TradeName ?? "");
            string unitNo = contract.Unit?.UnitNumber ?? "-";

            container.Column(col=>
            {
                col.Spacing(3); // بدون فراغات - 3 فقط

                // ===== Header مضغوط بدون فراغات =====
                col.Item().Row(row=>
                {
                    row.RelativeItem().Column(c=>
                    {
                        c.Item().AlignRight().Text(PdfMasterTemplate.CompanyName).FontSize(12).Bold().DirectionFromRightToLeft();
                        c.Item().AlignRight().Text($"هاتف {PdfMasterTemplate.CompanyPhone} - {PdfMasterTemplate.CompanyEmail}").FontSize(8).FontColor("#333").DirectionFromRightToLeft();
                    });
                    row.ConstantItem(140).AlignCenter().Column(c=>
                    {
                        c.Item().AlignCenter().Text(title).FontSize(15).Bold().DirectionFromRightToLeft();
                        c.Item().AlignCenter().Text(contract.ContractNumber).FontSize(10).Bold().FontColor("#222").DirectionFromRightToLeft();
                        c.Item().AlignCenter().Text(dateStr).FontSize(8).FontColor("#555").DirectionFromRightToLeft();
                    });
                    row.RelativeItem().AlignLeft().Column(c=>
                    {
                        if(PdfMasterTemplate.ShowLogo)
                        {
                            try{
                                string webRoot=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"wwwroot");
                                if(!Directory.Exists(webRoot)) webRoot=Path.Combine(Directory.GetCurrentDirectory(),"wwwroot");
                                string lp=PdfMasterTemplate.GetLogoPhysicalPath(webRoot);
                                if(!string.IsNullOrWhiteSpace(lp)&&File.Exists(lp)) c.Item().AlignLeft().Width(38).Image(lp).FitArea();
                            }catch{}
                        }
                    });
                });

                // مقدمة مضغوطة
                if(!string.IsNullOrWhiteSpace(intro))
                {
                    col.Item().PaddingTop(4).AlignRight().Text(intro).FontSize(10).LineHeight(1.3f).DirectionFromRightToLeft();
                }

                // ===== أطراف العقد - مفصل وواضح بدون فراغات =====
                col.Item().PaddingTop(6).Background("#F5F5F5").PaddingVertical(2).PaddingHorizontal(6).AlignRight().Text("أطراف العقد").FontSize(11).Bold().DirectionFromRightToLeft();

                col.Item().PaddingTop(3).Row(row=>
                {
                    // يمين: المؤجر
                    row.RelativeItem().Column(c=>
                    {
                        c.Item().AlignRight().Text("الطرف الأول - المؤجر").FontSize(9).Bold().FontColor("#000").DirectionFromRightToLeft();
                        c.Spacing(1);
                        c.Item().AlignRight().Text(PdfMasterTemplate.CompanyName).FontSize(10).Bold().DirectionFromRightToLeft();
                        c.Item().AlignRight().Text($"السجل التجاري: {PdfMasterTemplate.CompanyTaxNumber ?? "-"}").FontSize(8).DirectionFromRightToLeft();
                        c.Item().AlignRight().Text($"الهاتف: {PdfMasterTemplate.CompanyPhone}").FontSize(8).DirectionFromRightToLeft();
                        c.Item().AlignRight().Text($"العنوان: {PdfMasterTemplate.CompanyAddress}").FontSize(8).DirectionFromRightToLeft();
                    });
                    row.ConstantItem(10);
                    // يسار: المستأجر
                    row.RelativeItem().Column(c=>
                    {
                        c.Item().AlignRight().Text("الطرف الثاني - المستأجر").FontSize(9).Bold().FontColor("#000").DirectionFromRightToLeft();
                        c.Spacing(1);
                        c.Item().AlignRight().Text(contract.Tenant?.FullName ?? "-").FontSize(10).Bold().DirectionFromRightToLeft();
                        c.Item().AlignRight().Text($"رقم الهوية: {contract.Tenant?.NationalId ?? "-"}").FontSize(8).DirectionFromRightToLeft();
                        c.Item().AlignRight().Text($"رقم الهاتف: {contract.Tenant?.Phone ?? "-"}").FontSize(8).DirectionFromRightToLeft();
                        c.Item().AlignRight().Text($"النشاط: {activity}").FontSize(8).DirectionFromRightToLeft();
                    });
                });

                // ===== بيانات المحل: رقم ونشاط فقط - واضح ومفصل =====
                col.Item().PaddingTop(6).Background("#F5F5F5").PaddingVertical(2).PaddingHorizontal(6).AlignRight().Text("بيانات المحل المؤجر").FontSize(11).Bold().DirectionFromRightToLeft();
                col.Item().PaddingTop(3).Row(row=>
                {
                    row.RelativeItem().AlignRight().Column(c=>
                    {
                        c.Item().AlignRight().Text($"رقم المحل: {unitNo}").FontSize(10).Bold().DirectionFromRightToLeft();
                    });
                    row.RelativeItem().AlignRight().Column(c=>
                    {
                        c.Item().AlignRight().Text($"نوع النشاط: {activity}").FontSize(10).Bold().DirectionFromRightToLeft();
                    });
                });

                // ===== المدة والقيمة في سطرين واضحين =====
                col.Item().PaddingTop(6).Background("#F5F5F5").PaddingVertical(2).PaddingHorizontal(6).AlignRight().Text("المدة والقيمة المالية").FontSize(11).Bold().DirectionFromRightToLeft();

                col.Item().PaddingTop(3).Column(c=>
                {
                    c.Spacing(2);
                    c.Item().AlignRight().Text($"يبدأ العقد بتاريخ {contract.StartDate:yyyy/MM/dd} وينتهي بتاريخ {contract.EndDate:yyyy/MM/dd}").FontSize(10).DirectionFromRightToLeft();
                    c.Item().AlignRight().Text($"مدة العقد {duration} - دورة السداد {rentAr} - التجديد {(contract.AutoRenew?"تلقائي":"يدوي")}").FontSize(10).DirectionFromRightToLeft();
                    c.Item().AlignRight().Text($"قيمة الإيجار {contract.RentAmount:N2} {PdfMasterTemplate.CurrencySymbol} لكل {rentAr} - العربون {contract.DepositAmount:N2} {PdfMasterTemplate.CurrencySymbol}" + (contract.AnnualIncreasePercentage.HasValue?$" - الزيادة {contract.AnnualIncreasePercentage}%":"")).FontSize(10).Bold().DirectionFromRightToLeft();
                });

                // بنود إضافية مضغوطة
                if(contract.ContractItems.Any())
                {
                    col.Item().PaddingTop(5).Background("#F5F5F5").PaddingVertical(2).PaddingHorizontal(6).AlignRight().Text("بنود إضافية").FontSize(10).Bold().DirectionFromRightToLeft();
                    col.Item().PaddingTop(2).Column(c=>
                    {
                        c.Spacing(1);
                        foreach(var item in contract.ContractItems)
                        {
                            c.Item().AlignRight().Text($"{item.ItemName}: {item.Amount:N2} {PdfMasterTemplate.CurrencySymbol}" + (!string.IsNullOrWhiteSpace(item.Notes)?$" - {item.Notes}":"")).FontSize(9).DirectionFromRightToLeft();
                        }
                    });
                }

                // رسوم مضغوطة
                if(contract.ContractFees.Any())
                {
                    col.Item().PaddingTop(5).Background("#F5F5F5").PaddingVertical(2).PaddingHorizontal(6).AlignRight().Text("الرسوم").FontSize(10).Bold().DirectionFromRightToLeft();
                    col.Item().PaddingTop(2).Table(t=>
                    {
                        t.ColumnsDefinition(c=>{ c.RelativeColumn(3); c.RelativeColumn(1); c.RelativeColumn(1.5f); });
                        t.Cell().Element(HCell).AlignRight().Text("البيان").FontSize(8).Bold().DirectionFromRightToLeft();
                        t.Cell().Element(HCell).AlignRight().Text("الدورية").FontSize(8).Bold().DirectionFromRightToLeft();
                        t.Cell().Element(HCell).AlignRight().Text("المبلغ").FontSize(8).Bold().DirectionFromRightToLeft();
                        foreach(var fee in contract.ContractFees)
                        {
                            decimal calc=fee.CalculateActualAmount(contract.RentAmount, contract.RentAmount*months);
                            t.Cell().Element(BCell).AlignRight().Text(fee.FeeName).FontSize(8).DirectionFromRightToLeft();
                            t.Cell().Element(BCell).AlignRight().Text(fee.Frequency==Enums.FeeFrequency.OneTime?"مرة واحدة":"شهري").FontSize(8).DirectionFromRightToLeft();
                            t.Cell().Element(BCell).AlignRight().Text($"{calc:N2} {PdfMasterTemplate.CurrencySymbol}").FontSize(8).Bold().DirectionFromRightToLeft();
                        }
                    });
                }

                // الشروط - بدون أرقام، نقط فقط، خط كبير وواضح
                if(!string.IsNullOrWhiteSpace(clauses))
                {
                    col.Item().PaddingTop(6).Background("#F5F5F5").PaddingVertical(2).PaddingHorizontal(6).AlignRight().Text("الشروط والأحكام").FontSize(11).Bold().DirectionFromRightToLeft();
                    col.Item().PaddingTop(2).Column(c=>
                    {
                        c.Spacing(2);
                        var lines=clauses.Split('\n',StringSplitOptions.RemoveEmptyEntries).Take(10);
                        foreach(var line in lines)
                        {
                            var clean=System.Text.RegularExpressions.Regex.Replace(line.Trim(), @"^\d+[\-\.\)]\s*", "");
                            if(string.IsNullOrWhiteSpace(clean)) continue;
                            c.Item().AlignRight().Text($"• {clean}").FontSize(9).LineHeight(1.3f).DirectionFromRightToLeft();
                        }
                    });
                }

                // ===== التوقيعات ثابتة في الأسفل بدون فراغات كبيرة =====
                col.Item().PaddingTop(12).Row(row=>
                {
                    row.RelativeItem().Column(c=>
                    {
                        c.Item().AlignCenter().Text("الطرف الأول المؤجر").FontSize(10).Bold().DirectionFromRightToLeft();
                        c.Item().PaddingTop(2).AlignCenter().Text(PdfMasterTemplate.CompanyName).FontSize(8).DirectionFromRightToLeft();
                        c.Item().PaddingTop(18).LineHorizontal(0.7f).LineColor("#000");
                        c.Item().PaddingTop(3).AlignCenter().Text("التوقيع والختم").FontSize(7).FontColor("#555").DirectionFromRightToLeft();
                    });
                    row.ConstantItem(40);
                    row.RelativeItem().Column(c=>
                    {
                        c.Item().AlignCenter().Text("الطرف الثاني المستأجر").FontSize(10).Bold().DirectionFromRightToLeft();
                        c.Item().PaddingTop(2).AlignCenter().Text(contract.Tenant?.FullName ?? "").FontSize(8).DirectionFromRightToLeft();
                        c.Item().PaddingTop(18).LineHorizontal(0.7f).LineColor("#000");
                        c.Item().PaddingTop(3).AlignCenter().Text("التوقيع").FontSize(7).FontColor("#555").DirectionFromRightToLeft();
                    });
                });

                if(showWitnesses)
                {
                    col.Item().PaddingTop(10).Row(row=>
                    {
                        row.RelativeItem().Column(c=>{ c.Item().AlignCenter().Text("الشاهد الأول").FontSize(9).Bold().DirectionFromRightToLeft(); c.Item().PaddingTop(16).LineHorizontal(0.5f).LineColor("#000"); c.Item().PaddingTop(2).AlignCenter().Text("الاسم والتوقيع").FontSize(6).FontColor("#666").DirectionFromRightToLeft(); });
                        row.ConstantItem(40);
                        row.RelativeItem().Column(c=>{ c.Item().AlignCenter().Text("الشاهد الثاني").FontSize(9).Bold().DirectionFromRightToLeft(); c.Item().PaddingTop(16).LineHorizontal(0.5f).LineColor("#000"); c.Item().PaddingTop(2).AlignCenter().Text("الاسم والتوقيع").FontSize(6).FontColor("#666").DirectionFromRightToLeft(); });
                    });
                }

                if(!string.IsNullOrWhiteSpace(footerNote))
                {
                    col.Item().PaddingTop(6).AlignCenter().Text(footerNote).FontSize(7).FontColor("#666").DirectionFromRightToLeft();
                }
            });
        }

        private IContainer HCell(IContainer c)=>c.BorderBottom(0.6f).BorderColor("#000").PaddingVertical(2).PaddingHorizontal(4).Background("#EEE");
        private IContainer BCell(IContainer c)=>c.BorderBottom(0.2f).BorderColor("#DDD").PaddingVertical(2).PaddingHorizontal(4);
    }
}

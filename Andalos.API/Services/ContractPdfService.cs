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
                _ => cycle // لو عربي أصلاً يبقى كما هو
            };
        }

        private string FormatDuration(int months)
        {
            if (months <= 0) return "غير محددة";
            if (months < 12) return $"{months} شهر";
            int years = months / 12;
            int remMonths = months % 12;
            // دعم سنتان و 3 سنين
            string yearLabel = years switch
            {
                1 => "سنة واحدة",
                2 => "سنتان",
                3 => "3 سنوات",
                _ => $"{years} سنوات"
            };
            if (remMonths == 0) return yearLabel;
            return $"{yearLabel} و {remMonths} شهر";
        }

        private string TranslateRentCycle(object cycleObj)
        {
            if (cycleObj == null) return "شهري";
            return TranslateRentCycle(cycleObj.ToString() ?? "");
        }

        private string TranslateFrequency(Enums.FeeFrequency freq)
        {
            return freq == Enums.FeeFrequency.OneTime ? "مرة واحدة" : "شهري";
        }

        private string TranslateValueType(Enums.FeeValueType vt)
        {
            return vt == Enums.FeeValueType.Fixed ? "مبلغ ثابت" : "نسبة مئوية";
        }

        public async Task<byte[]> GenerateContractPdfAsync(int contractId)
        {
            var companyInfo = await _settings.GetCompanyInfoAsync();
            var allSettings = await _settings.GetAllSettingsDictionaryAsync();
            var showLogo = await _settings.GetValueAsync<bool>(SettingKeys.PdfShowLogo, true);
            var headerEnabled = await _settings.GetValueAsync<bool>(SettingKeys.PdfHeaderEnabled, true);
            var footerEnabled = await _settings.GetValueAsync<bool>(SettingKeys.PdfFooterEnabled, true);
            var companyInfoInHeader = await _settings.GetValueAsync<bool>(SettingKeys.PdfCompanyInfoInHeader, true);
            PdfMasterTemplate.ConfigureFromCompanyInfo(companyInfo, showLogo, headerEnabled, footerEnabled, companyInfoInHeader);
            PdfMasterTemplate.ConfigureFromSettingsDictionary(allSettings);

            var contract = await _db.Contracts.Include(c=>c.Tenant).Include(c=>c.Unit).Include(c=>c.ContractItems.Where(i=>i.IsActive)).Include(c=>c.ContractFees.Where(f=>f.IsActive)).FirstOrDefaultAsync(c=>c.Id==contractId);
            if(contract==null) throw new KeyNotFoundException("العقد غير موجود");

            var templateTitle = await _settings.GetValueAsync(SettingKeys.ContractTemplateTitle) ?? "عقد إيجار";
            var intro = await _settings.GetValueAsync(SettingKeys.ContractTemplateIntro) ?? "";
            var landlordLabel = await _settings.GetValueAsync(SettingKeys.ContractLandlordLabel) ?? "الطرف الأول (المؤجر)";
            var tenantLabel = await _settings.GetValueAsync(SettingKeys.ContractTenantLabel) ?? "الطرف الثاني (المستأجر)";
            var unitSectionTitle = await _settings.GetValueAsync(SettingKeys.ContractUnitSectionTitle) ?? "بيانات المحل";
            var termsSectionTitle = await _settings.GetValueAsync(SettingKeys.ContractTermsSectionTitle) ?? "مدة الإيجار";
            var paymentSectionTitle = await _settings.GetValueAsync(SettingKeys.ContractPaymentSectionTitle) ?? "القيمة المالية";
            var clauses = await _settings.GetValueAsync(SettingKeys.ContractClauses) ?? "";
            var signatureLandlord = await _settings.GetValueAsync(SettingKeys.ContractSignatureLandlord) ?? "الطرف الأول";
            var signatureTenant = await _settings.GetValueAsync(SettingKeys.ContractSignatureTenant) ?? "الطرف الثاني";
            var footerNote = await _settings.GetValueAsync(SettingKeys.ContractFooterNote) ?? "";
            var showWitnesses = await _settings.GetValueAsync<bool>(SettingKeys.ContractShowWitnesses, false);
            var showHijriDate = await _settings.GetValueAsync<bool>(SettingKeys.ContractShowHijriDate, false);
            var dateFormat = await _settings.GetValueAsync(SettingKeys.SystemDateFormat, "DD/MM/YYYY");
            string formattedDate = dateFormat.ToUpper() switch { "DD/MM/YYYY" => contract.StartDate.ToString("dd/MM/yyyy"), "MM/DD/YYYY" => contract.StartDate.ToString("MM/dd/yyyy"), "YYYY-MM-DD" => contract.StartDate.ToString("yyyy-MM-dd"), _ => contract.StartDate.ToString("yyyy/MM/dd") };
            string hijriDate = ""; if(showHijriDate){ try{ var hijri=new System.Globalization.HijriCalendar(); hijriDate=$"{hijri.GetDayOfMonth(contract.StartDate):00}/{hijri.GetMonth(contract.StartDate):00}/{hijri.GetYear(contract.StartDate)} هـ"; }catch{ hijriDate=""; } }
            intro = intro.Replace("{Date}", formattedDate).Replace("{HijriDate}", hijriDate);
            var graceDays = await _settings.GetValueAsync(SettingKeys.RentGraceDays) ?? "5"; clauses = clauses.Replace("{GraceDays}", graceDays);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(25); // هامش خارجي
                    page.DefaultTextStyle(x=>x.FontFamily("Arial").FontSize(9.5f).FontColor("#111").DirectionFromRightToLeft());

                    // تسطير جمالي للعقد - إطار مزدوج حول الصفحة
                    page.Content().Border(1.2f).BorderColor("#000").Padding(2).Border(0.4f).BorderColor("#888").Padding(12)
                        .Element(content => BuildCleanContent(content, contract, intro, landlordLabel, tenantLabel, unitSectionTitle, termsSectionTitle, paymentSectionTitle, clauses, signatureLandlord, signatureTenant, footerNote, showWitnesses, templateTitle, formattedDate, hijriDate));

                    page.Footer().PaddingTop(4).Element(BuildCleanFooter);
                });
            });

            return document.GeneratePdf();
        }

        private void BuildCleanFooter(IContainer container)
        {
            if(!PdfMasterTemplate.FooterEnabled){ container.Height(0); return; }
            container.Column(col=>
            {
                col.Item().PaddingTop(2).LineHorizontal(0.4f).LineColor("#AAA");
                col.Item().PaddingTop(3).Row(row=>
                {
                    row.RelativeItem().AlignRight().Text($"{PdfMasterTemplate.CompanyPhone} | {PdfMasterTemplate.CompanyEmail} | {PdfMasterTemplate.CompanyAddress}").FontSize(6f).FontColor("#777").DirectionFromRightToLeft();
                    row.ConstantItem(70).AlignLeft().Text(t=>{ t.Span("صفحة ").FontSize(6f); t.CurrentPageNumber().FontSize(6f).Bold(); t.Span(" من ").FontSize(6f); t.TotalPages().FontSize(6f).Bold(); });
                });
            });
        }

        private void BuildCleanContent(IContainer container, Contract contract, string intro, string landlordLabel, string tenantLabel, string unitTitle, string termsTitle, string paymentTitle, string clauses, string sigLandlord, string sigTenant, string footerNote, bool showWitnesses, string mainTitle, string formattedDate, string hijriDate)
        {
            // ترجمة دورة الإيجار للعربية
            string rentCycleAr = TranslateRentCycle(contract.RentCycle);

            container.Column(col=>
            {
                col.Spacing(5);

                // ===== Header داخل الإطار - RTL كامل من اليمين =====
                col.Item().Row(row=>
                {
                    // يمين: الشعار + الشركة
                    row.RelativeItem().Column(c=>
                    {
                        c.Item().Row(r=>
                        {
                            if(PdfMasterTemplate.ShowLogo)
                            {
                                try{
                                    string webRoot=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"wwwroot");
                                    if(!Directory.Exists(webRoot)) webRoot=Path.Combine(Directory.GetCurrentDirectory(),"wwwroot");
                                    string logoPath=PdfMasterTemplate.GetLogoPhysicalPath(webRoot);
                                    if(!string.IsNullOrWhiteSpace(logoPath)&&File.Exists(logoPath))
                                    {
                                        r.ConstantItem(38).Image(logoPath).FitArea();
                                        r.ConstantItem(6);
                                    }
                                }catch{}
                            }
                            r.RelativeItem().Column(info=>
                            {
                                info.Item().AlignRight().Text(PdfMasterTemplate.CompanyName).FontSize(12).Bold().FontColor("#000").DirectionFromRightToLeft();
                                info.Item().AlignRight().Text($"{PdfMasterTemplate.CompanyPhone} | {PdfMasterTemplate.CompanyEmail}").FontSize(7f).FontColor("#444").DirectionFromRightToLeft();
                                info.Item().AlignRight().Text(PdfMasterTemplate.CompanyAddress).FontSize(6.5f).FontColor("#666").DirectionFromRightToLeft();
                            });
                        });
                    });

                    // يسار: عنوان العقد (لكن محاذاة لليمين لأنه RTL)
                    row.ConstantItem(170).Column(c=>
                    {
                        c.Item().AlignRight().Text(mainTitle).FontSize(15).Bold().FontColor("#000").DirectionFromRightToLeft();
                        c.Item().PaddingTop(2).AlignRight().Text($"رقم العقد: {contract.ContractNumber}").FontSize(9).Bold().FontColor("#222").DirectionFromRightToLeft();
                        c.Item().AlignRight().Text($"التاريخ: {formattedDate}" + (!string.IsNullOrWhiteSpace(hijriDate) ? $" - {hijriDate}" : "")).FontSize(7f).FontColor("#555").DirectionFromRightToLeft();
                    });
                });

                col.Item().PaddingTop(6).LineHorizontal(0.8f).LineColor("#000");

                // مقدمة
                if(!string.IsNullOrWhiteSpace(intro))
                {
                    col.Item().PaddingTop(4).Text(intro).FontSize(9).LineHeight(1.5f).FontColor("#222").AlignRight().DirectionFromRightToLeft();
                }

                // أطراف العقد - RTL
                col.Item().PaddingTop(4).Element(c=>CleanSectionTitle(c,"أطراف العقد"));
                col.Item().Row(row=>
                {
                    row.RelativeItem().Column(c=>
                    {
                        c.Item().AlignRight().Text(landlordLabel).FontSize(8).Bold().FontColor("#000").DirectionFromRightToLeft();
                        c.Item().PaddingTop(2).AlignRight().Text(PdfMasterTemplate.CompanyName).FontSize(9).Bold().DirectionFromRightToLeft();
                        c.Item().AlignRight().Text($"هاتف: {PdfMasterTemplate.CompanyPhone}").FontSize(8).FontColor("#333").DirectionFromRightToLeft();
                        if(!string.IsNullOrWhiteSpace(PdfMasterTemplate.CompanyTaxNumber))
                            c.Item().AlignRight().Text($"الرقم الضريبي: {PdfMasterTemplate.CompanyTaxNumber}").FontSize(7.5f).FontColor("#555").DirectionFromRightToLeft();
                    });
                    row.ConstantItem(15);
                    row.RelativeItem().Column(c=>
                    {
                        c.Item().AlignRight().Text(tenantLabel).FontSize(8).Bold().FontColor("#000").DirectionFromRightToLeft();
                        c.Item().PaddingTop(2).AlignRight().Text(contract.Tenant?.FullName??"-").FontSize(9).Bold().DirectionFromRightToLeft();
                        c.Item().AlignRight().Text($"الهوية: {contract.Tenant?.NationalId??"-"} | الهاتف: {contract.Tenant?.Phone??"-"}").FontSize(8).FontColor("#333").DirectionFromRightToLeft();
                        var actDisplay = contract.ActivityType.ToString();
                        if(!string.IsNullOrWhiteSpace(contract.TradeName))
                            c.Item().AlignRight().Text($"الاسم التجاري: {contract.TradeName} | النشاط: {actDisplay}").FontSize(8).FontColor("#333").DirectionFromRightToLeft();
                        else
                            c.Item().AlignRight().Text($"النشاط: {actDisplay}").FontSize(8).FontColor("#333").DirectionFromRightToLeft();
                    });
                });

                col.Item().PaddingTop(3).LineHorizontal(0.3f).LineColor("#DDD");

                // بيانات المحل - سطر واحد RTL
                col.Item().Element(c=>CleanSectionTitle(c,unitTitle));
                var activityDisplay = !string.IsNullOrWhiteSpace(contract.TradeName) ? contract.TradeName : contract.ActivityType.ToString();
                col.Item().AlignRight().Text($"رقم المحل: {contract.Unit?.UnitNumber??"-"} | المساحة: {contract.Unit?.Area??0:N0} م² | المبنى: {contract.Unit?.Building??"-"} - الطابق: {contract.Unit?.Floor??"-"} | النشاط: {activityDisplay}").FontSize(8.5f).LineHeight(1.4f).DirectionFromRightToLeft();

                // المدة - مترجم مع دعم سنتان و 3 سنوات
                var durationMonths=Math.Max(1,(int)((contract.EndDate-contract.StartDate).TotalDays/30));
                var durationText=FormatDuration(durationMonths);
                col.Item().Element(c=>CleanSectionTitle(c,termsTitle));
                col.Item().AlignRight().Text($"من تاريخ: {contract.StartDate:yyyy/MM/dd} إلى تاريخ: {contract.EndDate:yyyy/MM/dd} | المدة: {durationText} | دورة السداد: {rentCycleAr} | التجديد التلقائي: {(contract.AutoRenew?"نعم":"لا")}").FontSize(8.5f).DirectionFromRightToLeft();

                // القيمة المالية - عربي نظيف
                col.Item().Element(c=>CleanSectionTitle(c,paymentTitle));
                col.Item().AlignRight().Text($"قيمة الإيجار: {contract.RentAmount:N2} {PdfMasterTemplate.CurrencySymbol} / {rentCycleAr} | قيمة العربون: {contract.DepositAmount:N2} {PdfMasterTemplate.CurrencySymbol}" + (contract.AnnualIncreasePercentage.HasValue?$" | نسبة الزيادة السنوية: {contract.AnnualIncreasePercentage}% ":"")).FontSize(9f).Bold().DirectionFromRightToLeft();

                // البنود الإضافية
                if(contract.ContractItems.Any())
                {
                    col.Item().Element(c=>CleanSectionTitle(c,"البنود الإضافية"));
                    foreach(var (item,idx) in contract.ContractItems.Select((v,i)=>(v,i)))
                    {
                        col.Item().AlignRight().Text($"{idx+1}. {item.ItemName} - {item.Amount:N2} {PdfMasterTemplate.CurrencySymbol}" + (!string.IsNullOrWhiteSpace(item.Notes)?$" ({item.Notes})":"")).FontSize(8).FontColor("#222").DirectionFromRightToLeft();
                    }
                }

                // الرسوم - جدول RTL نظيف
                if(contract.ContractFees.Any())
                {
                    col.Item().Element(c=>CleanSectionTitle(c,"الرسوم والعمولات"));
                    int totalMonths=Math.Max(1,(int)((contract.EndDate-contract.StartDate).TotalDays/30)); decimal totalValue=contract.RentAmount*totalMonths;
                    col.Item().Table(table=>
                    {
                        table.ColumnsDefinition(cols=>{ cols.ConstantColumn(18); cols.RelativeColumn(3); cols.RelativeColumn(1.3f); cols.RelativeColumn(1.3f); cols.RelativeColumn(1.4f); cols.RelativeColumn(1.4f); });
                        table.Cell().Element(CleanHeaderCell).Text("#").FontSize(7).Bold().AlignRight().DirectionFromRightToLeft();
                        table.Cell().Element(CleanHeaderCell).Text("اسم الرسم").FontSize(7).Bold().AlignRight().DirectionFromRightToLeft();
                        table.Cell().Element(CleanHeaderCell).Text("النوع").FontSize(7).Bold().AlignRight().DirectionFromRightToLeft();
                        table.Cell().Element(CleanHeaderCell).Text("الدورية").FontSize(7).Bold().AlignRight().DirectionFromRightToLeft();
                        table.Cell().Element(CleanHeaderCell).Text("القيمة").FontSize(7).Bold().AlignRight().DirectionFromRightToLeft();
                        table.Cell().Element(CleanHeaderCell).Text("المبلغ المحسوب").FontSize(7).Bold().AlignRight().DirectionFromRightToLeft();
                        foreach(var (fee,i) in contract.ContractFees.Select((v,idx)=>(v,idx)))
                        {
                            string vt=TranslateValueType(fee.ValueType); string fr=TranslateFrequency(fee.Frequency); string input=fee.ValueType==Enums.FeeValueType.Percentage?$"{fee.Value}%" : $"{fee.Value:N2} {PdfMasterTemplate.CurrencySymbol}"; decimal calc=fee.CalculateActualAmount(contract.RentAmount,totalValue);
                            table.Cell().Element(CleanDataCell).Text($"{i+1}").FontSize(7).AlignRight().DirectionFromRightToLeft();
                            table.Cell().Element(CleanDataCell).Text(fee.FeeName).FontSize(7).AlignRight().DirectionFromRightToLeft();
                            table.Cell().Element(CleanDataCell).Text(vt).FontSize(7).AlignRight().DirectionFromRightToLeft();
                            table.Cell().Element(CleanDataCell).Text(fr).FontSize(7).AlignRight().DirectionFromRightToLeft();
                            table.Cell().Element(CleanDataCell).Text(input).FontSize(7).AlignRight().DirectionFromRightToLeft();
                            table.Cell().Element(CleanDataCell).Text($"{calc:N2} {PdfMasterTemplate.CurrencySymbol}").FontSize(7).Bold().AlignRight().DirectionFromRightToLeft();
                        }
                    });
                    var totalOne=contract.ContractFees.Where(f=>f.Frequency==Enums.FeeFrequency.OneTime).Sum(f=>f.CalculateActualAmount(contract.RentAmount,totalValue));
                    var totalMonthly=contract.ContractFees.Where(f=>f.Frequency==Enums.FeeFrequency.Monthly).Sum(f=>f.CalculateActualAmount(contract.RentAmount,totalValue));
                    col.Item().PaddingTop(2).AlignRight().Text($"الإجمالي: رسوم لمرة واحدة {totalOne:N2} {PdfMasterTemplate.CurrencySymbol} | رسوم شهرية {totalMonthly:N2} {PdfMasterTemplate.CurrencySymbol}").FontSize(7.5f).Bold().FontColor("#000").DirectionFromRightToLeft();
                }

                // البنود القانونية
                if(!string.IsNullOrWhiteSpace(clauses))
                {
                    col.Item().Element(c=>CleanSectionTitle(c,"البنود والشروط"));
                    var lines=clauses.Split('\n',StringSplitOptions.RemoveEmptyEntries); int num=1;
                    foreach(var line in lines)
                    {
                        var trimmed=line.Trim(); if(string.IsNullOrWhiteSpace(trimmed)) continue;
                        col.Item().AlignRight().Text($"{num}. {trimmed}").FontSize(8).LineHeight(1.4f).FontColor("#222").DirectionFromRightToLeft();
                        num++; if(num>10) break;
                    }
                }

                // التوقيعات - RTL
                col.Item().PaddingTop(14).Row(row=>
                {
                    row.RelativeItem().Column(c=>{ c.Item().AlignCenter().Text(sigLandlord).FontSize(9).Bold().DirectionFromRightToLeft(); c.Item().PaddingTop(30).LineHorizontal(0.6f).LineColor("#000"); c.Item().PaddingTop(3).AlignCenter().Text("التوقيع والختم").FontSize(7).FontColor("#666").DirectionFromRightToLeft(); });
                    row.ConstantItem(60);
                    row.RelativeItem().Column(c=>{ c.Item().AlignCenter().Text(sigTenant).FontSize(9).Bold().DirectionFromRightToLeft(); c.Item().PaddingTop(30).LineHorizontal(0.6f).LineColor("#000"); c.Item().PaddingTop(3).AlignCenter().Text("التوقيع").FontSize(7).FontColor("#666").DirectionFromRightToLeft(); });
                });

                if(showWitnesses)
                {
                    col.Item().PaddingTop(12).Row(row=>{
                        row.RelativeItem().Column(c=>{ c.Item().AlignCenter().Text("الشاهد الأول").FontSize(8).Bold().DirectionFromRightToLeft(); c.Item().PaddingTop(22).LineHorizontal(0.5f).LineColor("#000"); c.Item().PaddingTop(2).AlignCenter().Text("الاسم: ............. التوقيع: .............").FontSize(6).FontColor("#666"); });
                        row.ConstantItem(60);
                        row.RelativeItem().Column(c=>{ c.Item().AlignCenter().Text("الشاهد الثاني").FontSize(8).Bold().DirectionFromRightToLeft(); c.Item().PaddingTop(22).LineHorizontal(0.5f).LineColor("#000"); c.Item().PaddingTop(2).AlignCenter().Text("الاسم: ............. التوقيع: .............").FontSize(6).FontColor("#666"); });
                    });
                }

                if(!string.IsNullOrWhiteSpace(footerNote))
                {
                    col.Item().PaddingTop(10).BorderTop(0.4f).BorderColor("#AAA").PaddingTop(4).AlignCenter().Text(footerNote).FontSize(7).Italic().FontColor("#666").DirectionFromRightToLeft();
                }
            });
        }

        private void CleanSectionTitle(IContainer container, string title)
        {
            container.PaddingTop(7).PaddingBottom(2).Column(col=>
            {
                col.Item().AlignRight().Text(title).FontSize(10).Bold().FontColor("#000").DirectionFromRightToLeft();
                col.Item().PaddingTop(2).LineHorizontal(0.6f).LineColor("#000");
            });
        }

        private IContainer CleanHeaderCell(IContainer container)
        {
            return container.BorderBottom(0.8f).BorderColor("#000").PaddingVertical(3).PaddingHorizontal(4).Background("#F5F5F5");
        }

        private IContainer CleanDataCell(IContainer container)
        {
            return container.BorderBottom(0.25f).BorderColor("#DDD").PaddingVertical(3).PaddingHorizontal(4);
        }
    }
}

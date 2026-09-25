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
            int remMonths = months % 12;
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

        private string CleanArabic(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            // إزالة الأحرف الغير عربية المشوشة والاحتفاظ بالنص نظيف
            return input.Trim();
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
                    page.Margin(25);
                    page.DefaultTextStyle(x=>x.FontFamily("Arial").FontSize(9.5f).FontColor("#111").DirectionFromRightToLeft());

                    // إطار جمالي مزخرف حول العقد كله
                    page.Content().Border(1.2f).BorderColor("#000").Padding(2).Border(0.5f).BorderColor("#666").Padding(14)
                        .Element(content => BuildContent(content, contract, intro, landlordLabel, tenantLabel, clauses, signatureLandlord, signatureTenant, footerNote, showWitnesses, templateTitle, formattedDate, hijriDate));

                    page.Footer().PaddingTop(3).Element(c=>BuildFooter(c));
                });
            });

            return document.GeneratePdf();
        }

        private void BuildFooter(IContainer container)
        {
            if(!PdfMasterTemplate.FooterEnabled){ container.Height(0); return; }
            container.Column(col=>
            {
                col.Item().PaddingTop(2).LineHorizontal(0.3f).LineColor("#BBB");
                col.Item().PaddingTop(2).Row(row=>
                {
                    row.RelativeItem().AlignRight().Text($"{PdfMasterTemplate.CompanyPhone} | {PdfMasterTemplate.CompanyEmail}").FontSize(6f).FontColor("#888").DirectionFromRightToLeft();
                    row.ConstantItem(60).AlignLeft().Text(t=>{ t.Span("صفحة ").FontSize(6f); t.CurrentPageNumber().FontSize(6f).Bold(); t.Span(" من ").FontSize(6f); t.TotalPages().FontSize(6f).Bold(); });
                });
            });
        }

        private void BuildContent(IContainer container, Contract contract, string intro, string landlordLabel, string tenantLabel, string clauses, string sigLandlord, string sigTenant, string footerNote, bool showWitnesses, string mainTitle, string formattedDate, string hijriDate)
        {
            string rentCycleAr = TranslateRentCycle(contract.RentCycle.ToString());
            var durationMonths=Math.Max(1,(int)((contract.EndDate-contract.StartDate).TotalDays/30));
            var durationText=FormatDuration(durationMonths);

            // تنظيف النشاط - عربي فقط - ActivityType enum غير nullable
            string activityClean = !string.IsNullOrWhiteSpace(contract.TradeName) ? contract.TradeName : CleanArabic(contract.ActivityType.ToString());
            // إزالة كلمة Other الإنجليزية
            if(activityClean.Equals("Other", StringComparison.OrdinalIgnoreCase)) activityClean = "نشاط تجاري";
            if(activityClean.Equals("Restaurant", StringComparison.OrdinalIgnoreCase)) activityClean = "مطعم";
            if(activityClean.Equals("Cafe", StringComparison.OrdinalIgnoreCase)) activityClean = "مقهى";

            string unitNumberClean = contract.Unit?.UnitNumber ?? "-";

            container.Column(col=>
            {
                col.Spacing(5);

                // ===== Header داخل الإطار - يمين الشركة ويسار العنوان لكن كله RTL =====
                col.Item().Row(row=>
                {
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
                                        r.ConstantItem(36).Image(logoPath).FitArea();
                                        r.ConstantItem(6);
                                    }
                                }catch{}
                            }
                            r.RelativeItem().Column(info=>
                            {
                                info.Item().AlignRight().Text(PdfMasterTemplate.CompanyName).FontSize(11).Bold().FontColor("#000").DirectionFromRightToLeft();
                                info.Item().AlignRight().Text(PdfMasterTemplate.CompanyPhone).FontSize(7f).FontColor("#444").DirectionFromRightToLeft();
                            });
                        });
                    });
                    row.ConstantItem(160).Column(c=>
                    {
                        c.Item().AlignRight().Text(mainTitle).FontSize(14).Bold().FontColor("#000").DirectionFromRightToLeft();
                        c.Item().PaddingTop(1).AlignRight().Text($"رقم العقد {contract.ContractNumber}").FontSize(8.5f).Bold().FontColor("#222").DirectionFromRightToLeft();
                        c.Item().AlignRight().Text($"التاريخ {formattedDate}").FontSize(7f).FontColor("#555").DirectionFromRightToLeft();
                    });
                });

                col.Item().PaddingTop(5).LineHorizontal(0.6f).LineColor("#000");

                if(!string.IsNullOrWhiteSpace(intro))
                {
                    col.Item().PaddingTop(3).AlignRight().Text(intro).FontSize(8.5f).LineHeight(1.5f).FontColor("#222").DirectionFromRightToLeft();
                }

                // ===== أطراف العقد: الأول على اليمين، الثاني على اليسار =====
                col.Item().PaddingTop(6).Element(c=>SectionTitle(c,"أطراف العقد"));

                col.Item().Row(row=>
                {
                    // يمين: الطرف الأول (المؤجر)
                    row.RelativeItem().Column(c=>
                    {
                        c.Item().AlignRight().Text(landlordLabel).FontSize(8).Bold().FontColor("#000").DirectionFromRightToLeft();
                        c.Item().PaddingTop(2).AlignRight().Text(PdfMasterTemplate.CompanyName).FontSize(9).Bold().DirectionFromRightToLeft();
                        c.Item().AlignRight().Text($"هاتف {PdfMasterTemplate.CompanyPhone}").FontSize(7.5f).FontColor("#333").DirectionFromRightToLeft();
                    });

                    // وسط: مسافة
                    row.ConstantItem(20);

                    // يسار: الطرف الثاني (المستأجر)
                    row.RelativeItem().Column(c=>
                    {
                        c.Item().AlignRight().Text(tenantLabel).FontSize(8).Bold().FontColor("#000").DirectionFromRightToLeft();
                        c.Item().PaddingTop(2).AlignRight().Text(contract.Tenant?.FullName??"-").FontSize(9).Bold().DirectionFromRightToLeft();
                        c.Item().AlignRight().Text($"هوية {contract.Tenant?.NationalId??"-"}").FontSize(7.5f).FontColor("#333").DirectionFromRightToLeft();
                        c.Item().AlignRight().Text($"هاتف {contract.Tenant?.Phone??"-"}").FontSize(7.5f).FontColor("#333").DirectionFromRightToLeft();
                    });
                });

                // ===== بيانات المحل: رقم المحل والنشاط فقط (بدون خطوط) =====
                col.Item().PaddingTop(6).Element(c=>SectionTitle(c,"بيانات المحل"));
                col.Item().Row(row=>
                {
                    row.RelativeItem().AlignRight().Text($"رقم المحل {unitNumberClean}").FontSize(9).Bold().DirectionFromRightToLeft();
                    row.RelativeItem().AlignRight().Text($"النشاط {activityClean}").FontSize(9).Bold().DirectionFromRightToLeft();
                });

                // ===== المدة =====
                col.Item().PaddingTop(5).Element(c=>SectionTitle(c,"مدة الإيجار"));
                col.Item().AlignRight().Text($"يبدأ من {contract.StartDate:yyyy/MM/dd} حتى {contract.EndDate:yyyy/MM/dd}").FontSize(8.5f).DirectionFromRightToLeft();
                col.Item().AlignRight().Text($"المدة {durationText} - دورة السداد {rentCycleAr} - التجديد {(contract.AutoRenew?"تلقائي":"يدوي")}").FontSize(8.5f).DirectionFromRightToLeft();

                // ===== القيمة المالية =====
                col.Item().PaddingTop(5).Element(c=>SectionTitle(c,"القيمة المالية"));
                col.Item().AlignRight().Text($"قيمة الإيجار {contract.RentAmount:N2} {PdfMasterTemplate.CurrencySymbol} لكل {rentCycleAr}").FontSize(9).Bold().DirectionFromRightToLeft();
                col.Item().AlignRight().Text($"قيمة العربون {contract.DepositAmount:N2} {PdfMasterTemplate.CurrencySymbol}" + (contract.AnnualIncreasePercentage.HasValue?$" - نسبة الزيادة السنوية {contract.AnnualIncreasePercentage}%":"")).FontSize(8.5f).DirectionFromRightToLeft();

                // البنود الإضافية
                if(contract.ContractItems.Any())
                {
                    col.Item().PaddingTop(4).Element(c=>SectionTitle(c,"بنود إضافية"));
                    foreach(var item in contract.ContractItems)
                    {
                        col.Item().AlignRight().Text($"- {item.ItemName} {item.Amount:N2} {PdfMasterTemplate.CurrencySymbol}" + (!string.IsNullOrWhiteSpace(item.Notes)?$" - {item.Notes}":"")).FontSize(8).DirectionFromRightToLeft();
                    }
                }

                // الرسوم
                if(contract.ContractFees.Any())
                {
                    col.Item().PaddingTop(4).Element(c=>SectionTitle(c,"الرسوم"));
                    int totalMonths=Math.Max(1,(int)((contract.EndDate-contract.StartDate).TotalDays/30)); decimal totalValue=contract.RentAmount*totalMonths;
                    col.Item().Table(table=>
                    {
                        table.ColumnsDefinition(cols=>{ cols.RelativeColumn(3); cols.RelativeColumn(1.5f); cols.RelativeColumn(1.5f); cols.RelativeColumn(2); });
                        table.Cell().Element(HeaderCell).Text("اسم الرسم").FontSize(7).Bold().AlignRight().DirectionFromRightToLeft();
                        table.Cell().Element(HeaderCell).Text("النوع").FontSize(7).Bold().AlignRight().DirectionFromRightToLeft();
                        table.Cell().Element(HeaderCell).Text("الدورية").FontSize(7).Bold().AlignRight().DirectionFromRightToLeft();
                        table.Cell().Element(HeaderCell).Text("المبلغ").FontSize(7).Bold().AlignRight().DirectionFromRightToLeft();
                        foreach(var fee in contract.ContractFees)
                        {
                            string vt=fee.ValueType==Enums.FeeValueType.Fixed?"مبلغ ثابت":"نسبة"; string fr=fee.Frequency==Enums.FeeFrequency.OneTime?"مرة واحدة":"شهري"; decimal calc=fee.CalculateActualAmount(contract.RentAmount,totalValue);
                            table.Cell().Element(DataCell).Text(fee.FeeName).FontSize(7).AlignRight().DirectionFromRightToLeft();
                            table.Cell().Element(DataCell).Text(vt).FontSize(7).AlignRight().DirectionFromRightToLeft();
                            table.Cell().Element(DataCell).Text(fr).FontSize(7).AlignRight().DirectionFromRightToLeft();
                            table.Cell().Element(DataCell).Text($"{calc:N2} {PdfMasterTemplate.CurrencySymbol}").FontSize(7).Bold().AlignRight().DirectionFromRightToLeft();
                        }
                    });
                }

                // البنود والشروط - بدون أرقام لتجنب مشكلة RTL
                if(!string.IsNullOrWhiteSpace(clauses))
                {
                    col.Item().PaddingTop(5).Element(c=>SectionTitle(c,"الشروط والأحكام"));
                    var lines=clauses.Split('\n',StringSplitOptions.RemoveEmptyEntries);
                    foreach(var line in lines.Take(8))
                    {
                        var trimmed=CleanArabic(line);
                        if(string.IsNullOrWhiteSpace(trimmed)) continue;
                        // استخدام نقطة • بدل رقم لتجنب مشكلة الأرقام بعد الكلام
                        col.Item().AlignRight().Text($"• {trimmed}").FontSize(8).LineHeight(1.4f).FontColor("#222").DirectionFromRightToLeft();
                    }
                }

                // ===== Spacer يدفع التوقيعات للأسفل ثابتة =====
                col.Item().Extend();

                // ===== التوقيعات ثابتة في الأسفل =====
                col.Item().PaddingTop(20).LineHorizontal(0.5f).LineColor("#000");
                col.Item().PaddingTop(8).Row(row=>
                {
                    // يمين: الطرف الأول
                    row.RelativeItem().Column(c=>
                    {
                        c.Item().AlignCenter().Text(sigLandlord).FontSize(9).Bold().DirectionFromRightToLeft();
                        c.Item().PaddingTop(30).LineHorizontal(0.5f).LineColor("#000");
                        c.Item().PaddingTop(3).AlignCenter().Text("التوقيع والختم").FontSize(7).FontColor("#666").DirectionFromRightToLeft();
                    });
                    row.ConstantItem(80);
                    // يسار: الطرف الثاني
                    row.RelativeItem().Column(c=>
                    {
                        c.Item().AlignCenter().Text(sigTenant).FontSize(9).Bold().DirectionFromRightToLeft();
                        c.Item().PaddingTop(30).LineHorizontal(0.5f).LineColor("#000");
                        c.Item().PaddingTop(3).AlignCenter().Text("التوقيع").FontSize(7).FontColor("#666").DirectionFromRightToLeft();
                    });
                });

                if(showWitnesses)
                {
                    col.Item().PaddingTop(15).Row(row=>
                    {
                        row.RelativeItem().Column(c=>{ c.Item().AlignCenter().Text("الشاهد الأول").FontSize(8).Bold().DirectionFromRightToLeft(); c.Item().PaddingTop(22).LineHorizontal(0.4f).LineColor("#000"); });
                        row.ConstantItem(80);
                        row.RelativeItem().Column(c=>{ c.Item().AlignCenter().Text("الشاهد الثاني").FontSize(8).Bold().DirectionFromRightToLeft(); c.Item().PaddingTop(22).LineHorizontal(0.4f).LineColor("#000"); });
                    });
                }

                if(!string.IsNullOrWhiteSpace(footerNote))
                {
                    col.Item().PaddingTop(8).AlignCenter().Text(footerNote).FontSize(6.5f).Italic().FontColor("#666").DirectionFromRightToLeft();
                }
            });
        }

        private void SectionTitle(IContainer container, string title)
        {
            container.Column(col=>
            {
                col.Item().AlignRight().Text(title).FontSize(10).Bold().FontColor("#000").DirectionFromRightToLeft();
                col.Item().PaddingTop(1).LineHorizontal(0.6f).LineColor("#000");
            });
        }

        private IContainer HeaderCell(IContainer container)
        {
            return container.BorderBottom(0.6f).BorderColor("#000").PaddingVertical(3).PaddingHorizontal(4).Background("#F2F2F2");
        }

        private IContainer DataCell(IContainer container)
        {
            return container.BorderBottom(0.2f).BorderColor("#DDD").PaddingVertical(2.5f).PaddingHorizontal(4);
        }
    }
}

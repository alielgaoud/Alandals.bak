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
            var document = Document.Create(container => { container.Page(page => { page.Size(PageSizes.A4); page.Margin(35); page.DefaultTextStyle(x=>x.FontFamily("Arial").FontSize(9.5f).FontColor("#111").DirectionFromRightToLeft()); page.Header().Element(c=>BuildCleanHeader(c, templateTitle, contract.ContractNumber, formattedDate, hijriDate)); page.Content().PaddingTop(10).Element(content=>BuildCleanContent(content, contract, intro, landlordLabel, tenantLabel, unitSectionTitle, termsSectionTitle, paymentSectionTitle, clauses, signatureLandlord, signatureTenant, footerNote, showWitnesses)); page.Footer().PaddingTop(6).Element(BuildCleanFooter); }); });
            return document.GeneratePdf();
        }
        private void BuildCleanHeader(IContainer container, string title, string number, string date, string hijri)
        {
            container.Column(col=>{ col.Item().Row(row=>{ row.RelativeItem().Column(c=>{ c.Item().Row(r=>{ if(PdfMasterTemplate.ShowLogo){ try{ string webRoot=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"wwwroot"); if(!Directory.Exists(webRoot)) webRoot=Path.Combine(Directory.GetCurrentDirectory(),"wwwroot"); string logoPath=PdfMasterTemplate.GetLogoPhysicalPath(webRoot); if(!string.IsNullOrWhiteSpace(logoPath)&&File.Exists(logoPath)){ r.ConstantItem(42).Image(logoPath).FitArea(); r.ConstantItem(8); } }catch{} } r.RelativeItem().Column(info=>{ info.Item().Text(PdfMasterTemplate.CompanyName).FontSize(13).Bold().FontColor("#000"); info.Item().PaddingTop(2).Text($"{PdfMasterTemplate.CompanyPhone} | {PdfMasterTemplate.CompanyEmail}").FontSize(7.5f).FontColor("#444"); info.Item().Text(PdfMasterTemplate.CompanyAddress).FontSize(7).FontColor("#666"); }); }); }); row.ConstantItem(180).AlignLeft().Column(c=>{ c.Item().AlignLeft().Text(title).FontSize(16).Bold().FontColor("#000"); c.Item().PaddingTop(2).AlignLeft().Text($"رقم: {number}").FontSize(9).Bold().FontColor("#222"); c.Item().AlignLeft().Text($"التاريخ: {date}"+(!string.IsNullOrWhiteSpace(hijri)?$" - {hijri}":"")).FontSize(7.5f).FontColor("#555"); }); }); col.Item().PaddingTop(8).LineHorizontal(0.7f).LineColor("#000"); });
        }
        private void BuildCleanFooter(IContainer container){ if(!PdfMasterTemplate.FooterEnabled){ container.Height(0); return; } container.Column(col=>{ col.Item().LineHorizontal(0.5f).LineColor("#999"); col.Item().PaddingTop(4).Row(row=>{ row.RelativeItem().Text($"{PdfMasterTemplate.CompanyPhone} | {PdfMasterTemplate.CompanyEmail} | {PdfMasterTemplate.CompanyAddress}").FontSize(6.5f).FontColor("#777"); row.ConstantItem(80).AlignLeft().Text(t=>{ t.Span("صفحة ").FontSize(6.5f); t.CurrentPageNumber().FontSize(6.5f).Bold(); t.Span(" من ").FontSize(6.5f); t.TotalPages().FontSize(6.5f).Bold(); }); }); }); }
        private void BuildCleanContent(IContainer container, Contract contract, string intro, string landlordLabel, string tenantLabel, string unitTitle, string termsTitle, string paymentTitle, string clauses, string sigLandlord, string sigTenant, string footerNote, bool showWitnesses)
        {
            container.Column(col=>{
                col.Spacing(6);
                if(!string.IsNullOrWhiteSpace(intro)){ col.Item().Text(intro).FontSize(9).LineHeight(1.5f).FontColor("#222").AlignRight(); }
                col.Item().PaddingTop(4).Element(c=>CleanSectionTitle(c,"أطراف العقد"));
                col.Item().Row(row=>{
                    row.RelativeItem().Column(c=>{ c.Item().Text(landlordLabel).FontSize(8).Bold().FontColor("#000"); c.Item().PaddingTop(2).Text(PdfMasterTemplate.CompanyName).FontSize(9).Bold(); c.Item().Text($"هاتف: {PdfMasterTemplate.CompanyPhone}").FontSize(8).FontColor("#333"); if(!string.IsNullOrWhiteSpace(PdfMasterTemplate.CompanyTaxNumber)) c.Item().Text($"الرقم الضريبي: {PdfMasterTemplate.CompanyTaxNumber}").FontSize(7.5f).FontColor("#555"); });
                    row.ConstantItem(20);
                    row.RelativeItem().Column(c=>{ c.Item().Text(tenantLabel).FontSize(8).Bold().FontColor("#000"); c.Item().PaddingTop(2).Text(contract.Tenant?.FullName??"-").FontSize(9).Bold(); c.Item().Text($"هوية: {contract.Tenant?.NationalId??"-"} | هاتف: {contract.Tenant?.Phone??"-"}").FontSize(8).FontColor("#333"); if(!string.IsNullOrWhiteSpace(contract.TradeName)) c.Item().Text($"الاسم التجاري: {contract.TradeName} - النشاط: {contract.ActivityType}").FontSize(8).FontColor("#333"); });
                });
                col.Item().PaddingTop(2).LineHorizontal(0.3f).LineColor("#DDD");
                col.Item().Element(c=>CleanSectionTitle(c,unitTitle));
                col.Item().Text($"المحل رقم: {contract.Unit?.UnitNumber??"-"} | المساحة: {contract.Unit?.Area??0:N0} م² | المبنى: {contract.Unit?.Building??"-"} - الطابق: {contract.Unit?.Floor??"-"} | الاسم التجاري: {contract.TradeName??"-"}").FontSize(8.5f).LineHeight(1.4f);
                var durationMonths=Math.Max(1,(int)((contract.EndDate-contract.StartDate).TotalDays/30)); var durationText=durationMonths>=12?$"{durationMonths/12} سنة"+(durationMonths%12>0?$" و {durationMonths%12} شهر":""):$"{durationMonths} شهر";
                col.Item().Element(c=>CleanSectionTitle(c,termsTitle));
                col.Item().Text($"من: {contract.StartDate:yyyy/MM/dd} إلى: {contract.EndDate:yyyy/MM/dd} | المدة: {durationText} | دورة الدفع: {contract.RentCycle} | التجديد التلقائي: {(contract.AutoRenew?"نعم":"لا")} | الحالة: {contract.Status}").FontSize(8.5f);
                col.Item().Element(c=>CleanSectionTitle(c,paymentTitle));
                col.Item().Text($"الإيجار: {contract.RentAmount:N2} {PdfMasterTemplate.CurrencySymbol} / {contract.RentCycle} | العربون: {contract.DepositAmount:N2} {PdfMasterTemplate.CurrencySymbol}"+(contract.AnnualIncreasePercentage.HasValue?$" | الزيادة السنوية: {contract.AnnualIncreasePercentage}% ":"")).FontSize(8.5f).Bold();
                if(contract.ContractItems.Any()){
                    col.Item().Element(c=>CleanSectionTitle(c,"البنود الإضافية"));
                    foreach(var (item,idx) in contract.ContractItems.Select((v,i)=>(v,i))){
                        col.Item().Text($"{idx+1}. {item.ItemName} - {item.Amount:N2} {PdfMasterTemplate.CurrencySymbol}"+(!string.IsNullOrWhiteSpace(item.Notes)?$" ({item.Notes})":"")).FontSize(8).FontColor("#222");
                    }
                }
                if(contract.ContractFees.Any()){
                    col.Item().Element(c=>CleanSectionTitle(c,"الرسوم والعمولات"));
                    int totalMonths=Math.Max(1,(int)((contract.EndDate-contract.StartDate).TotalDays/30)); decimal totalValue=contract.RentAmount*totalMonths;
                    col.Item().Table(table=>{
                        table.ColumnsDefinition(cols=>{ cols.ConstantColumn(20); cols.RelativeColumn(3); cols.RelativeColumn(1.2f); cols.RelativeColumn(1.2f); cols.RelativeColumn(1.5f); cols.RelativeColumn(1.5f); });
                        table.Cell().Element(CleanHeaderCell).Text("#").FontSize(7).Bold(); table.Cell().Element(CleanHeaderCell).Text("اسم الرسم").FontSize(7).Bold(); table.Cell().Element(CleanHeaderCell).Text("النوع").FontSize(7).Bold(); table.Cell().Element(CleanHeaderCell).Text("الدورية").FontSize(7).Bold(); table.Cell().Element(CleanHeaderCell).Text("القيمة").FontSize(7).Bold(); table.Cell().Element(CleanHeaderCell).Text("المبلغ").FontSize(7).Bold();
                        foreach(var (fee,i) in contract.ContractFees.Select((v,idx)=>(v,idx))){
                            string vt=fee.ValueType==Enums.FeeValueType.Fixed?"ثابت":"نسبة"; string fr=fee.Frequency==Enums.FeeFrequency.OneTime?"مرة واحدة":"شهري"; string input=fee.ValueType==Enums.FeeValueType.Percentage?$"{fee.Value}%":$"{fee.Value:N2}"; decimal calc=fee.CalculateActualAmount(contract.RentAmount,totalValue);
                            table.Cell().Element(CleanDataCell).Text($"{i+1}").FontSize(7); table.Cell().Element(CleanDataCell).Text(fee.FeeName).FontSize(7); table.Cell().Element(CleanDataCell).Text(vt).FontSize(7); table.Cell().Element(CleanDataCell).Text(fr).FontSize(7); table.Cell().Element(CleanDataCell).Text(input).FontSize(7); table.Cell().Element(CleanDataCell).Text($"{calc:N2}").FontSize(7).Bold();
                        }
                    });
                    var totalOne=contract.ContractFees.Where(f=>f.Frequency==Enums.FeeFrequency.OneTime).Sum(f=>f.CalculateActualAmount(contract.RentAmount,totalValue));
                    var totalMonthly=contract.ContractFees.Where(f=>f.Frequency==Enums.FeeFrequency.Monthly).Sum(f=>f.CalculateActualAmount(contract.RentAmount,totalValue));
                    col.Item().PaddingTop(2).Text($"الإجمالي: لمرة واحدة {totalOne:N2} {PdfMasterTemplate.CurrencySymbol} | شهري {totalMonthly:N2} {PdfMasterTemplate.CurrencySymbol}").FontSize(7.5f).Bold().FontColor("#000");
                }
                if(!string.IsNullOrWhiteSpace(clauses)){
                    col.Item().Element(c=>CleanSectionTitle(c,"البنود والشروط"));
                    var lines=clauses.Split('\n',StringSplitOptions.RemoveEmptyEntries); int num=1;
                    foreach(var line in lines){ var trimmed=line.Trim(); if(string.IsNullOrWhiteSpace(trimmed)) continue; col.Item().Text($"{num}. {trimmed}").FontSize(8).LineHeight(1.4f).FontColor("#222"); num++; if(num>12) break; }
                }
                col.Item().PaddingTop(12).Row(row=>{
                    row.RelativeItem().Column(c=>{ c.Item().AlignCenter().Text(sigLandlord).FontSize(9).Bold(); c.Item().PaddingTop(28).LineHorizontal(0.5f).LineColor("#000"); c.Item().PaddingTop(3).AlignCenter().Text("التوقيع والختم").FontSize(7).FontColor("#666"); });
                    row.ConstantItem(50);
                    row.RelativeItem().Column(c=>{ c.Item().AlignCenter().Text(sigTenant).FontSize(9).Bold(); c.Item().PaddingTop(28).LineHorizontal(0.5f).LineColor("#000"); c.Item().PaddingTop(3).AlignCenter().Text("التوقيع").FontSize(7).FontColor("#666"); });
                });
                if(showWitnesses){
                    col.Item().PaddingTop(10).Row(row=>{ row.RelativeItem().Column(c=>{ c.Item().AlignCenter().Text("الشاهد الأول").FontSize(8).Bold(); c.Item().PaddingTop(20).LineHorizontal(0.5f).LineColor("#000"); }); row.ConstantItem(50); row.RelativeItem().Column(c=>{ c.Item().AlignCenter().Text("الشاهد الثاني").FontSize(8).Bold(); c.Item().PaddingTop(20).LineHorizontal(0.5f).LineColor("#000"); }); });
                }
                if(!string.IsNullOrWhiteSpace(footerNote)){ col.Item().PaddingTop(8).BorderTop(0.3f).BorderColor("#CCC").PaddingTop(4).Text(footerNote).FontSize(7).Italic().FontColor("#666").AlignCenter(); }
            });
        }
        private void CleanSectionTitle(IContainer container, string title){ container.PaddingTop(6).PaddingBottom(2).Column(col=>{ col.Item().Text(title).FontSize(9.5f).Bold().FontColor("#000"); col.Item().PaddingTop(1).LineHorizontal(0.5f).LineColor("#000"); }); }
        private IContainer CleanHeaderCell(IContainer container){ return container.BorderBottom(0.7f).BorderColor("#000").PaddingVertical(3).PaddingHorizontal(3).AlignRight(); }
        private IContainer CleanDataCell(IContainer container){ return container.BorderBottom(0.2f).BorderColor("#DDD").PaddingVertical(2.5f).PaddingHorizontal(3).AlignRight(); }
    }
}

using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.DTOs.Reports;
using Andalos.API.Enums;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Andalos.API.Services
{
    public class DemandLetterPdfService
    {
        private readonly AppDbContext _db;
        private readonly ISettingService _settings;

        public DemandLetterPdfService(AppDbContext db, ISettingService settings)
        {
            _db = db;
            _settings = settings;
        }

        public async Task<byte[]> GenerateAsync(GenerateDemandLetterDto dto)
        {
            var companyInfo = await _settings.GetCompanyInfoAsync();
            var showLogo = await _settings.GetValueAsync<bool>(SettingKeys.PdfShowLogo, true);
            var headerEnabled = await _settings.GetValueAsync<bool>(SettingKeys.PdfHeaderEnabled, true);
            var footerEnabled = await _settings.GetValueAsync<bool>(SettingKeys.PdfFooterEnabled, true);
            PdfMasterTemplate.ConfigureFromCompanyInfo(companyInfo, showLogo, headerEnabled, footerEnabled);

            var data = await BuildDataAsync(dto);

            var document = Document.Create(container =>
            {
                PdfMasterTemplate.BuildPage(
                    container,
                    "خطاب مطالبة مالية",
                    "", // 👈 إرسال قيمة فارغة لإخفاء "رقم المستند" من الترويسة
                    content => BuildContent(content, data)
                );
            });

            return document.GeneratePdf();
        }

        private async Task<DemandLetterDataDto> BuildDataAsync(GenerateDemandLetterDto dto)
        {
            var tenant = await _db.Tenants
                .FirstOrDefaultAsync(t => t.Id == dto.TenantId && t.IsActive)
                ?? throw new KeyNotFoundException("المستأجر غير موجود");

            var today = DateTime.Today;
            var fromDate = (dto.FromDate ?? today.AddMonths(-6)).Date;
            var toDate = (dto.ToDate ?? today).Date.AddDays(1).AddTicks(-1);
            var dueDate = (dto.DueDate ?? today.AddDays(7)).Date;

            var contractsQuery = _db.Contracts
                .Include(c => c.Unit)
                .Include(c => c.ContractFees)
                .Where(c => c.TenantId == dto.TenantId && c.IsActive);

            if (dto.ContractId.HasValue)
                contractsQuery = contractsQuery.Where(c => c.Id == dto.ContractId.Value);

            var contracts = await contractsQuery
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            if (!contracts.Any())
                throw new InvalidOperationException("لا يوجد عقد مرتبط بهذا المستأجر ضمن هذه الفترة");

            var mainContract = contracts.FirstOrDefault(c => c.Status == ContractStatus.Active)
                               ?? contracts.First();

            var items = new List<DemandLetterItemDto>();

            // ===== 1) الإيجارات المستحقة =====
            if (dto.IncludeRent)
            {
                foreach (var contract in contracts.Where(c =>
                             c.Status == ContractStatus.Active ||
                             c.Status == ContractStatus.Expired ||
                             c.Status == ContractStatus.Renewed))
                {
                    var start = contract.StartDate.Date < fromDate ? fromDate : contract.StartDate.Date;
                    var end = contract.EndDate.Date > toDate.Date ? toDate.Date : contract.EndDate.Date;
                    if (end > today) end = today;

                    int durationMonths = Math.Max(1, (int)((contract.EndDate - contract.StartDate).TotalDays / 30));
                    decimal totalContractValue = contract.RentAmount * durationMonths;
                    decimal monthlyFees = 0;

                    if (dto.IncludeContractFees)
                    {
                        monthlyFees = contract.ContractFees
                            .Where(f => f.Frequency == FeeFrequency.Monthly)
                            .Sum(f => f.CalculateActualAmount(contract.RentAmount, totalContractValue));
                    }

                    var current = new DateTime(start.Year, start.Month, 1);
                    var endMonth = new DateTime(end.Year, end.Month, 1);

                    while (current <= endMonth)
                    {
                        var amount = contract.RentAmount + monthlyFees;
                        var desc = monthlyFees > 0
                            ? $"إيجار شهر {current:MM/yyyy} + رسوم شهرية"
                            : $"إيجار شهر {current:MM/yyyy}";

                        items.Add(new DemandLetterItemDto
                        {
                            Category = "إيجار",
                            Description = $"{desc} — عقد {contract.ContractNumber}",
                            Date = current,
                            Amount = amount,
                            Reference = contract.ContractNumber
                        });

                        current = current.AddMonths(1);
                    }

                    // رسوم مرة واحدة (إن وُجدت في الفترة)
                    if (dto.IncludeContractFees)
                    {
                        foreach (var fee in contract.ContractFees.Where(f => f.Frequency == FeeFrequency.OneTime))
                        {
                            if (contract.StartDate.Date >= fromDate && contract.StartDate.Date <= toDate.Date)
                            {
                                var feeAmount = fee.CalculateActualAmount(contract.RentAmount, totalContractValue);
                                items.Add(new DemandLetterItemDto
                                {
                                    Category = "رسوم عقد",
                                    Description = $"{fee.FeeName}",
                                    Date = contract.StartDate,
                                    Amount = feeAmount,
                                    Reference = contract.ContractNumber
                                });
                            }
                        }
                    }
                }
            }

            // ===== 2) المصروفات المحملة =====
            if (dto.IncludeChargedExpenses)
            {
                var expenses = await _db.Expenses
                    .Include(e => e.Unit)
                    .Where(e => e.TenantId == dto.TenantId
                                && e.IsChargedToTenant
                                && e.IsActive
                                && e.ExpenseDate >= fromDate
                                && e.ExpenseDate <= toDate)
                    .OrderBy(e => e.ExpenseDate)
                    .ToListAsync();

                foreach (var exp in expenses)
                {
                    items.Add(new DemandLetterItemDto
                    {
                        Category = "مصروفات أخرى",
                        Description = $"{exp.Description}",
                        Date = exp.ExpenseDate,
                        Amount = exp.Amount,
                        Reference = exp.ExpenseNumber
                    });
                }
            }

            items = items.OrderBy(i => i.Date).ThenBy(i => i.Category).ToList();

            decimal total = items.Sum(i => i.Amount);

            string periodDescription = dto.PeriodDescription;
            if (string.IsNullOrWhiteSpace(periodDescription))
            {
                periodDescription = $"الفترة من {fromDate:yyyy/MM/dd} إلى {toDate:yyyy/MM/dd}";
            }

            return new DemandLetterDataDto
            {
                TenantName = tenant.FullName,
                TenantPhone = tenant.Phone,
                NationalId = tenant.NationalId,

                ContractNumber = mainContract.ContractNumber,
                UnitNumber = mainContract.Unit?.UnitNumber,
                TradeName = mainContract.TradeName,
                ContractStartDate = mainContract.StartDate,
                ContractEndDate = mainContract.EndDate,

                LetterDate = today,
                DueDate = dueDate,
                PeriodDescription = periodDescription,

                Items = items,
                TotalAmount = total,
                TotalAmountInWords = TafqeetHelper.ToArabicWords(total),
                ExtraNote = dto.ExtraNote
            };
        }

        private void BuildContent(IContainer container, DemandLetterDataDto data)
        {
            // 👈 تطبيق الـ RTL الكامل للمحتوى لضمان ترتيب الجمل من اليمين
            container.ContentFromRightToLeft().Column(col =>
            {
                // 👈 تقليل المسافات العامة لضمان بقائها في صفحة واحدة
                col.Spacing(10);

                // ===== 1) الترويسة الداخلية للخطاب =====
                col.Item().Row(row =>
                {
                    row.RelativeItem().AlignRight().Column(c =>
                    {
                        c.Item().Text($"التاريخ:  {data.LetterDate:yyyy/MM/dd} م").FontSize(11).Bold().FontColor("#1F2937");
                        c.Item().PaddingTop(4).Text("الرقم الإشاري:  ........................................").FontSize(11).Bold().FontColor("#1F2937");
                    });
                });

                // ===== 2) المرسل إليه =====
                // 👈 تعديل المرسل إليه: استخدام الاسم التجاري للمحل إن وجد، وإلا اسم المستأجر
                string addressedTo = !string.IsNullOrWhiteSpace(data.TradeName) ? data.TradeName : data.TenantName;

                col.Item().PaddingTop(10).AlignRight()
                    .Text($"السادة / {addressedTo}")
                    .FontSize(15).Bold().FontColor("#000000");

                col.Item().AlignRight()
                    .Text("بعد التحية،،،")
                    .FontSize(13).Bold();

                // ===== 3) نص الخطاب الأول (الإشارة للعقد) =====
                col.Item().PaddingTop(5).AlignRight().Text(text =>
                {
                    text.Span("بـالإشــــــارة ").ExtraBold().FontSize(13);
                    text.Span($"إلى العقد الإيجار رقم ({data.ContractNumber ?? "-"}) لسنة {data.ContractStartDate?.Year.ToString() ?? "-"}م ").FontSize(12);
                    text.Span($"الخاص بمحل رقم ({data.UnitNumber ?? "-"}) ").FontSize(12);
                    // 👈 إزالة الاسم التجاري من هنا لأنه أصبح في (السادة / ...)
                    text.Span($"نود إحاطتكم علماً بأن {data.PeriodDescription} قد انتهت.").FontSize(12);
                });

                // ===== 4) نص الخطاب الثاني (المطالبة المالية) =====
                col.Item().AlignRight().Text(text =>
                {
                    text.Span("وبـنــــــــــاءً ").ExtraBold().FontSize(13);
                    text.Span($"عليه ووفقاً لبنود العقد المبرم بيننا، نأمل منكم التفضل بسداد قيمة {data.PeriodDescription} ").FontSize(12);
                    text.Span($"حيث تبلغ القيمة المستحقة ( {data.TotalAmount:N3} ) ").FontSize(12).Bold();
                    text.Span($"{data.TotalAmountInWords}").FontSize(12);
                });

                // ===== 5) جدول تفصيلي للتوضيح =====
                if (data.Items.Any())
                {
                    col.Item().PaddingTop(5).Element(t =>
                    {
                        t.Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(30);   // #
                                c.RelativeColumn(1.2f); // النوع
                                c.RelativeColumn(3.5f); // البيان
                                c.RelativeColumn(1.2f); // التاريخ
                                c.ConstantColumn(80);   // المبلغ
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderCell).Text("ت");
                                h.Cell().Element(HeaderCell).Text("النوع");
                                h.Cell().Element(HeaderCell).Text("البيان");
                                h.Cell().Element(HeaderCell).Text("التاريخ");
                                h.Cell().Element(HeaderCell).Text("المبلغ");
                            });

                            int i = 1;
                            foreach (var item in data.Items)
                            {
                                table.Cell().Element(DataCell).AlignCenter().Text(i.ToString());
                                table.Cell().Element(DataCell).AlignRight().Text(item.Category);
                                table.Cell().Element(DataCell).AlignRight().Text(item.Description);
                                table.Cell().Element(DataCell).AlignCenter().Text(item.Date?.ToString("yyyy/MM/dd") ?? "-");
                                table.Cell().Element(DataCell).AlignCenter().Text($"{item.Amount:N3}");
                                i++;
                            }
                        });
                    });
                }

                // ===== 6) تنبيه الاستحقاق =====
                col.Item().PaddingTop(10).AlignRight().Text(text =>
                {
                    text.Span("عـلــمــــــــاً ").ExtraBold().FontSize(13);
                    text.Span($"بأن تاريخ الاستحقاق هو {data.DueDate:yyyy/MM/dd}م، لذا نأمل منكم اتخاذ ما يلزم حيال السداد.").FontSize(12);
                });

                if (!string.IsNullOrWhiteSpace(data.ExtraNote))
                {
                    col.Item().AlignRight()
                        .Text($"ملاحظة: {data.ExtraNote}")
                        .FontSize(11).Italic();
                }

                // ===== 7) الختام =====
                col.Item().PaddingTop(15).AlignCenter()
                    .Text("شاكرين حسن تعاونكم...")
                    .FontSize(13).Bold();

                col.Item().PaddingTop(5).AlignCenter()
                    .Text("والسلام عليكم")
                    .FontSize(13).Bold();

                // ===== 8) التوقيع والختم المفرغ =====
                // 👈 تقليل المسافة العلوية للتوقيع لضمان احتواء المستند في صفحة واحدة
                col.Item().PaddingTop(30).Row(row =>
                {
                    row.ConstantItem(250).Column(c =>
                    {
                        c.Item().AlignCenter().Text("الإدارة المالية / مدير التشغيل").FontSize(11).Bold();
                        c.Item().PaddingTop(40)
                            .BorderBottom(1)
                            .BorderColor(PdfMasterTemplate.Black)
                            .Height(1);
                        c.Item().PaddingTop(5).AlignCenter().Text("التوقيع والختم الرسمي").FontSize(10).FontColor(PdfMasterTemplate.Gray);
                    });

                    row.RelativeItem();
                });
            });
        }

        private static IContainer HeaderCell(IContainer container)
        {
            return container
                .Background("#f3f4f6")
                .Border(0.5f)
                .BorderColor(PdfMasterTemplate.Black)
                .Padding(6)
                .AlignCenter()
                .AlignMiddle()
                .DefaultTextStyle(x => x.FontSize(10).Bold());
        }

        private static IContainer DataCell(IContainer container)
        {
            return container
                .Border(0.5f)
                .BorderColor(PdfMasterTemplate.Black)
                .Padding(6)
                .AlignMiddle()
                .DefaultTextStyle(x => x.FontSize(9));
        }
    }
}
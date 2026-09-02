using Andalos.API.Data;
using Andalos.API.Enums;
using Andalos.API.Helpers;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Andalos.API.Services
{
    public class ComplaintReportPdfService
    {
        private readonly AppDbContext _db;

        public ComplaintReportPdfService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<byte[]> GenerateComplaintsReportPdfAsync(
            int? tenantId,
            string? status,
            DateTime? fromDate,
            DateTime? toDate,
            bool summaryOnly = false) // 👈 خيار التقرير التجميعي
        {
            var query = _db.Complaints
                .Include(c => c.Tenant)
                .Include(c => c.User)
                .Include(c => c.Replies)
                    .ThenInclude(r => r.RepliedByUser)
                .Where(c => c.IsActive);

            if (tenantId.HasValue)
                query = query.Where(c => c.TenantId == tenantId.Value);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ComplaintStatus>(status, true, out var parsedStatus))
                query = query.Where(c => c.Status == parsedStatus);

            if (fromDate.HasValue)
                query = query.Where(c => c.SubmittedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(c => c.SubmittedAt <= toDate.Value);

            var complaints = await query
                .OrderByDescending(c => c.SubmittedAt)
                .ToListAsync();

            // بناء عنوان التقرير حسب الفلاتر ونوع التقرير
            string reportTitle = summaryOnly ? "تقرير الشكاوى التجميعي" : "تقرير الشكاوى والردود التفصيلي";

            string filterInfo = "جميع الشكاوى";
            if (tenantId.HasValue || !string.IsNullOrEmpty(status) || fromDate.HasValue || toDate.HasValue)
            {
                var parts = new List<string>();
                if (tenantId.HasValue)
                {
                    var tenantName = complaints.FirstOrDefault()?.Tenant?.FullName ?? $"مستأجر رقم {tenantId}";
                    parts.Add($"المستأجر: {tenantName}");
                }
                if (!string.IsNullOrEmpty(status))
                    parts.Add($"الحالة: {GetStatusLabel(status)}");
                if (fromDate.HasValue)
                    parts.Add($"من: {fromDate.Value:yyyy/MM/dd}");
                if (toDate.HasValue)
                    parts.Add($"إلى: {toDate.Value:yyyy/MM/dd}");

                filterInfo = string.Join(" | ", parts);
            }

            var document = Document.Create(container =>
            {
                PdfMasterTemplate.BuildPage(
                    container,
                    reportTitle,
                    filterInfo,
                    content => BuildComplaintsContent(content, complaints, summaryOnly)
                );
            });

            return document.GeneratePdf();
        }

        private void BuildComplaintsContent(IContainer container, List<Complaint> complaints, bool summaryOnly)
        {
            container.ContentFromRightToLeft().Column(col =>
            {
                col.Spacing(12);

                // ===== ملخص إحصائي =====
                int totalCount = complaints.Count;
                int newCount = complaints.Count(c => c.Status == ComplaintStatus.New);
                int inProgressCount = complaints.Count(c => c.Status == ComplaintStatus.InProgress);
                int resolvedCount = complaints.Count(c => c.Status == ComplaintStatus.Resolved);
                int closedCount = complaints.Count(c => c.Status == ComplaintStatus.Closed);

                col.Item().Row(row =>
                {
                    row.RelativeItem().Element(c => SummaryCard(c, "إجمالي الشكاوى", totalCount.ToString(), "#1F2937"));
                    row.ConstantItem(6);
                    row.RelativeItem().Element(c => SummaryCard(c, "جديدة", newCount.ToString(), "#374151"));
                    row.ConstantItem(6);
                    row.RelativeItem().Element(c => SummaryCard(c, "قيد المعالجة", inProgressCount.ToString(), "#4B5563"));
                    row.ConstantItem(6);
                    row.RelativeItem().Element(c => SummaryCard(c, "تم الحل", resolvedCount.ToString(), "#111827"));
                    row.ConstantItem(6);
                    row.RelativeItem().Element(c => SummaryCard(c, "مغلقة", closedCount.ToString(), "#6B7280"));
                });

                if (!complaints.Any())
                {
                    col.Item().PaddingVertical(30).AlignCenter()
                        .Text("لا توجد شكاوى مطابقة لمعايير البحث المحددة")
                        .FontSize(12).FontColor(PdfMasterTemplate.Gray);
                    return;
                }

                // ===== جدول الشكاوى =====
                col.Item().PaddingTop(10).Element(tableContainer =>
                {
                    tableContainer.Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(30);   // ت
                            columns.RelativeColumn(2);    // الموضوع
                            columns.RelativeColumn(1.5f); // المستأجر
                            columns.RelativeColumn(1);    // الحالة
                            columns.RelativeColumn(1);    // التاريخ
                            columns.ConstantColumn(35);   // الردود
                        });

                        // ===== رأس الجدول =====
                        table.Header(header =>
                        {
                            header.Cell().Element(CellHeaderStyle).AlignRight().Text("ت").Bold();
                            header.Cell().Element(CellHeaderStyle).AlignRight().Text("الموضوع").Bold();
                            header.Cell().Element(CellHeaderStyle).AlignRight().Text("المستأجر").Bold();
                            header.Cell().Element(CellHeaderStyle).AlignCenter().Text("الحالة").Bold();
                            header.Cell().Element(CellHeaderStyle).AlignCenter().Text("التاريخ").Bold();
                            header.Cell().Element(CellHeaderStyle).AlignCenter().Text("الردود").Bold();
                        });

                        // ===== بيانات الجدول =====
                        int index = 1;
                        foreach (var c in complaints)
                        {
                            var bgColor = index % 2 == 0 ? "#F9FAFB" : "#FFFFFF";

                            table.Cell().Element(cell => CellStyle(cell, bgColor)).AlignRight().Text(index.ToString()).FontSize(9);
                            table.Cell().Element(cell => CellStyle(cell, bgColor)).AlignRight().Text(c.Subject).FontSize(9);
                            table.Cell().Element(cell => CellStyle(cell, bgColor)).AlignRight().Text(c.Tenant?.FullName ?? "-").FontSize(9);
                            table.Cell().Element(cell => CellStyle(cell, bgColor)).Element(statusCell =>
                            {
                                StatusBadge(statusCell, c.Status);
                            });
                            table.Cell().Element(cell => CellStyle(cell, bgColor)).AlignCenter().Text(c.SubmittedAt.ToString("yyyy/MM/dd")).FontSize(9);
                            table.Cell().Element(cell => CellStyle(cell, bgColor)).AlignCenter().Text(c.Replies.Count.ToString()).FontSize(9);

                            index++;
                        }
                    });
                });

                // ===== إذا كان التقرير تجميعي فقط، نتوقف هنا بدون عرض التفاصيل والردود =====
                if (summaryOnly) return;

                // ===== تفاصيل الشكاوى والردود (تظهر فقط في التقرير التفصيلي) =====
                col.Item().PaddingTop(15).PaddingBottom(4).BorderBottom(1.5f).BorderColor("#374151")
                    .AlignRight()
                    .Text("سجل التفاصيل والردود الإدارية")
                    .FontSize(12).Bold().FontColor("#1F2937");

                foreach (var c in complaints)
                {
                    col.Item().PaddingTop(8).Element(complaintBox =>
                    {
                        complaintBox.Border(1).BorderColor("#D1D5DB").Padding(10).Column(cb =>
                        {
                            cb.Spacing(6);

                            // رأس الشكوى
                            cb.Item().Row(row =>
                            {
                                row.RelativeItem().AlignRight().Text(c.Subject)
                                    .FontSize(11).Bold().FontColor("#1F2937");
                                row.ConstantItem(90).AlignLeft().Element(statusCell =>
                                {
                                    StatusBadge(statusCell, c.Status);
                                });
                            });

                            // معلومات الشكوى - 👈 استخدام FullName للمستخدم
                            string senderName = !string.IsNullOrEmpty(c.User?.FullName) ? c.User.FullName : (c.User?.UserName ?? "-");

                            cb.Item().Row(row =>
                            {
                                row.RelativeItem().AlignRight().Text($"المستأجر: {c.Tenant?.FullName ?? "-"}")
                                    .FontSize(9).FontColor(PdfMasterTemplate.Gray);
                                row.RelativeItem().AlignRight().Text($"أرسلها: {senderName}")
                                    .FontSize(9).FontColor(PdfMasterTemplate.Gray);
                                row.RelativeItem().AlignLeft()
                                    .Text(c.SubmittedAt.ToString("yyyy/MM/dd HH:mm"))
                                    .FontSize(9).FontColor(PdfMasterTemplate.Gray);
                            });

                            // وصف الشكوى
                            cb.Item().Background("#F9FAFB").Border(0.5f).BorderColor("#E5E7EB").Padding(8)
                                .AlignRight()
                                .Text(c.Description).FontSize(9).FontColor("#374151");

                            // الردود المتسلسلة
                            if (c.Replies.Any())
                            {
                                cb.Item().PaddingTop(4).AlignRight().Text("↓ الردود والقرارات المتخذة:")
                                    .FontSize(9).Bold().FontColor("#4B5563");

                                foreach (var r in c.Replies.OrderBy(x => x.RepliedAt))
                                {
                                    cb.Item().PaddingRight(10).BorderRight(2).BorderColor("#9CA3AF").PaddingRight(10).Column(rc =>
                                    {
                                        rc.Spacing(2);
                                        rc.Item().Row(rr =>
                                        {
                                            rr.RelativeItem().AlignRight().Text($"← {r.RepliedByUser?.FullName ?? "الإدارة"}")
                                                .FontSize(9).Bold().FontColor("#1F2937");
                                            rr.RelativeItem().AlignLeft()
                                                .Text(r.RepliedAt.ToString("yyyy/MM/dd HH:mm"))
                                                .FontSize(8).FontColor(PdfMasterTemplate.Gray);
                                        });
                                        rc.Item().AlignRight().Text(r.ReplyText).FontSize(9).FontColor("#4B5563");
                                    });
                                }
                            }
                            else
                            {
                                cb.Item().PaddingTop(2).AlignRight()
                                    .Text("لا توجد ردود مسجلة على هذه الشكوى حتى الآن.")
                                    .FontSize(8).Italic().FontColor(PdfMasterTemplate.Gray);
                            }
                        });
                    });
                }
            });
        }

        // ===== مكونات التصميم =====

        private static void SummaryCard(IContainer container, string title, string value, string color)
        {
            container.Background("#F9FAFB").Border(1).BorderColor("#E5E7EB")
                .Padding(8).Column(col =>
                {
                    col.Spacing(2);
                    col.Item().Text(value).FontSize(15).Bold().FontColor(color).AlignCenter();
                    col.Item().Text(title).FontSize(8).FontColor(PdfMasterTemplate.Gray).AlignCenter();
                });
        }

        private static IContainer CellHeaderStyle(IContainer container)
        {
            return container.Background("#374151").Padding(6)
                .DefaultTextStyle(x => x.FontColor(Colors.White).FontSize(9));
        }

        private static IContainer CellStyle(IContainer container, string bgColor)
        {
            return container.Background(bgColor).Padding(5).BorderBottom(0.5f).BorderColor("#E5E7EB")
                .DefaultTextStyle(x => x.FontColor("#374151"));
        }

        private static void StatusBadge(IContainer container, ComplaintStatus status)
        {
            var label = status switch
            {
                ComplaintStatus.New => "جديدة",
                ComplaintStatus.InProgress => "قيد المعالجة",
                ComplaintStatus.Resolved => "تم الحل",
                ComplaintStatus.Closed => "مغلقة",
                _ => "غير معروف"
            };

            container.Border(0.5f).BorderColor("#9CA3AF").Background("#F9FAFB").PaddingHorizontal(6).PaddingVertical(2)
                .AlignCenter().AlignMiddle()
                .Text(label).FontSize(8).Bold().FontColor("#374151");
        }

        private static string GetStatusLabel(string status)
        {
            return status.ToLower() switch
            {
                "new" => "جديدة",
                "inprogress" => "قيد المعالجة",
                "resolved" => "تم الحل",
                "closed" => "مغلقة",
                _ => status
            };
        }
    }
}
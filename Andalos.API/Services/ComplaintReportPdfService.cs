using Andalos.API.Data;
using Andalos.API.Enums;
using Andalos.API.Helpers;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Andalos.API.Services
{
    /// <summary>
    /// إنشاء تقارير الشكاوى بصيغة PDF
    /// التصميم: Minimal / Professional / Black & White
    /// </summary>
    public class ComplaintReportPdfService
    {
        private readonly AppDbContext _db;

        public ComplaintReportPdfService(AppDbContext db)
        {
            _db = db;
        }


        // =========================================================
        // إنشاء تقرير الشكاوى
        // =========================================================
        public async Task<byte[]> GenerateComplaintsReportPdfAsync(
            int? tenantId,
            string? status,
            DateTime? fromDate,
            DateTime? toDate,
            bool summaryOnly = false)
        {
            // -----------------------------------------------------
            // Query
            // -----------------------------------------------------
            var query = _db.Complaints
                .Include(c => c.Tenant)
                .Include(c => c.User)
                .Include(c => c.Replies)
                    .ThenInclude(r => r.RepliedByUser)
                .Where(c => c.IsActive);

            // -----------------------------------------------------
            // Filters
            // -----------------------------------------------------
            if (tenantId.HasValue)
            {
                query = query.Where(c =>
                    c.TenantId == tenantId.Value);
            }

            if (!string.IsNullOrEmpty(status) &&
                Enum.TryParse<ComplaintStatus>(
                    status,
                    true,
                    out var parsedStatus))
            {
                query = query.Where(c =>
                    c.Status == parsedStatus);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(c =>
                    c.SubmittedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(c =>
                    c.SubmittedAt <= toDate.Value);
            }

            // -----------------------------------------------------
            // Load complaints
            // -----------------------------------------------------
            var complaints = await query
                .OrderByDescending(c => c.SubmittedAt)
                .ToListAsync();

            // -----------------------------------------------------
            // Report title
            // -----------------------------------------------------
            string reportTitle = summaryOnly
                ? "تقرير الشكاوى التجميعي"
                : "تقرير الشكاوى والردود التفصيلي";

            // -----------------------------------------------------
            // Filter information
            // -----------------------------------------------------
            string filterInfo = BuildFilterInfo(
                complaints,
                tenantId,
                status,
                fromDate,
                toDate);

            // -----------------------------------------------------
            // Create PDF
            // -----------------------------------------------------
            var document = Document.Create(container =>
            {
                PdfMasterTemplate.BuildPage(
                    container,
                    reportTitle,
                    filterInfo,
                    content =>
                        BuildComplaintsContent(
                            content,
                            complaints,
                            summaryOnly)
                );
            });

            return document.GeneratePdf();
        }


        // =========================================================
        // بناء معلومات الفلاتر
        // =========================================================
        private static string BuildFilterInfo(
            List<Complaint> complaints,
            int? tenantId,
            string? status,
            DateTime? fromDate,
            DateTime? toDate)
        {
            if (!tenantId.HasValue &&
                string.IsNullOrEmpty(status) &&
                !fromDate.HasValue &&
                !toDate.HasValue)
            {
                return "جميع الشكاوى";
            }

            var parts = new List<string>();

            // المستأجر
            if (tenantId.HasValue)
            {
                var tenantName =
                    complaints
                        .FirstOrDefault()
                        ?.Tenant
                        ?.FullName
                    ?? $"مستأجر رقم {tenantId.Value}";

                parts.Add(
                    $"المستأجر: {tenantName}");
            }

            // الحالة
            if (!string.IsNullOrEmpty(status))
            {
                parts.Add(
                    $"الحالة: {GetStatusLabel(status)}");
            }

            // من
            if (fromDate.HasValue)
            {
                parts.Add(
                    $"من: {fromDate.Value:yyyy/MM/dd}");
            }

            // إلى
            if (toDate.HasValue)
            {
                parts.Add(
                    $"إلى: {toDate.Value:yyyy/MM/dd}");
            }

            return parts.Count > 0
                ? string.Join("   |   ", parts)
                : "جميع الشكاوى";
        }


        // =========================================================
        // محتوى التقرير
        // =========================================================
        private static void BuildComplaintsContent(
            IContainer container,
            List<Complaint> complaints,
            bool summaryOnly)
        {
            container
                .ContentFromRightToLeft()
                .Column(col =>
                {
                    col.Spacing(14);

                    // =================================================
                    // الإحصائيات
                    // =================================================

                    int totalCount =
                        complaints.Count;

                    int newCount =
                        complaints.Count(c =>
                            c.Status == ComplaintStatus.New);

                    int inProgressCount =
                        complaints.Count(c =>
                            c.Status == ComplaintStatus.InProgress);

                    int resolvedCount =
                        complaints.Count(c =>
                            c.Status == ComplaintStatus.Resolved);

                    int closedCount =
                        complaints.Count(c =>
                            c.Status == ComplaintStatus.Closed);


                    col.Item()
                        .Row(row =>
                        {
                            SummaryCard(
                                row.RelativeItem(),
                                "إجمالي الشكاوى",
                                totalCount);

                            row.ConstantItem(6);

                            SummaryCard(
                                row.RelativeItem(),
                                "جديدة",
                                newCount);

                            row.ConstantItem(6);

                            SummaryCard(
                                row.RelativeItem(),
                                "قيد المعالجة",
                                inProgressCount);

                            row.ConstantItem(6);

                            SummaryCard(
                                row.RelativeItem(),
                                "تم الحل",
                                resolvedCount);

                            row.ConstantItem(6);

                            SummaryCard(
                                row.RelativeItem(),
                                "مغلقة",
                                closedCount);
                        });


                    // =================================================
                    // لا توجد شكاوى
                    // =================================================

                    if (!complaints.Any())
                    {
                        col.Item()
                            .PaddingVertical(45)
                            .AlignCenter()
                            .Column(empty =>
                            {
                                empty.Item()
                                    .Text("لا توجد شكاوى")
                                    .FontFamily("Arial")
                                    .FontSize(14)
                                    .Bold()
                                    .FontColor(
                                        PdfMasterTemplate.Black);

                                empty.Item()
                                    .PaddingTop(6)
                                    .Text(
                                        "لا توجد شكاوى مطابقة لمعايير البحث المحددة.")
                                    .FontFamily("Arial")
                                    .FontSize(9)
                                    .FontColor(
                                        PdfMasterTemplate.Black);
                            });

                        return;
                    }


                    // =================================================
                    // عنوان القائمة
                    // =================================================

                    col.Item()
                        .Element(c =>
                            PdfMasterTemplate.SectionTitle(
                                c,
                                "قائمة الشكاوى"));


                    // =================================================
                    // جدول الشكاوى
                    // =================================================

                    col.Item()
                        .Table(table =>
                        {
                            // -----------------------------------------
                            // الأعمدة
                            // -----------------------------------------

                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.ConstantColumn(40);
                            });


                            // -----------------------------------------
                            // Header
                            // -----------------------------------------

                            table.Header(header =>
                            {
                                header.Cell()
                                    .Element(CellHeaderStyle)
                                    .AlignCenter()
                                    .Text("ت");

                                header.Cell()
                                    .Element(CellHeaderStyle)
                                    .AlignRight()
                                    .Text("الموضوع");

                                header.Cell()
                                    .Element(CellHeaderStyle)
                                    .AlignRight()
                                    .Text("المستأجر");

                                header.Cell()
                                    .Element(CellHeaderStyle)
                                    .AlignCenter()
                                    .Text("الحالة");

                                header.Cell()
                                    .Element(CellHeaderStyle)
                                    .AlignCenter()
                                    .Text("التاريخ");

                                header.Cell()
                                    .Element(CellHeaderStyle)
                                    .AlignCenter()
                                    .Text("الردود");
                            });


                            // -----------------------------------------
                            // Rows
                            // -----------------------------------------

                            int index = 1;

                            foreach (var complaint in complaints)
                            {
                                // الرقم
                                table.Cell()
                                    .Element(CellStyle)
                                    .AlignCenter()
                                    .Text(index.ToString())
                                    .FontFamily("Arial")
                                    .FontSize(8.5f)
                                    .FontColor(
                                        PdfMasterTemplate.Black);


                                // الموضوع
                                table.Cell()
                                    .Element(CellStyle)
                                    .AlignRight()
                                    .Text(
                                        complaint.Subject ?? "-")
                                    .FontFamily("Arial")
                                    .FontSize(8.5f)
                                    .FontColor(
                                        PdfMasterTemplate.Black);


                                // المستأجر
                                table.Cell()
                                    .Element(CellStyle)
                                    .AlignRight()
                                    .Text(
                                        complaint.Tenant?.FullName
                                        ?? "-")
                                    .FontFamily("Arial")
                                    .FontSize(8.5f)
                                    .FontColor(
                                        PdfMasterTemplate.Black);


                                // الحالة
                                table.Cell()
                                    .Element(CellStyle)
                                    .Element(statusContainer =>
                                    {
                                        StatusBadge(
                                            statusContainer,
                                            complaint.Status);
                                    });


                                // التاريخ
                                table.Cell()
                                    .Element(CellStyle)
                                    .AlignCenter()
                                    .Text(
                                        complaint.SubmittedAt
                                            .ToString("yyyy/MM/dd"))
                                    .FontFamily("Arial")
                                    .FontSize(8.5f)
                                    .FontColor(
                                        PdfMasterTemplate.Black);


                                // عدد الردود
                                table.Cell()
                                    .Element(CellStyle)
                                    .AlignCenter()
                                    .Text(
                                        complaint.Replies.Count
                                            .ToString())
                                    .FontFamily("Arial")
                                    .FontSize(8.5f)
                                    .Bold()
                                    .FontColor(
                                        PdfMasterTemplate.Black);


                                index++;
                            }
                        });


                    // =================================================
                    // التقرير التجميعي
                    // =================================================

                    if (summaryOnly)
                    {
                        return;
                    }


                    // =================================================
                    // عنوان التفاصيل
                    // =================================================

                    col.Item()
                        .PaddingTop(10)
                        .Element(c =>
                            PdfMasterTemplate.SectionTitle(
                                c,
                                "سجل التفاصيل والردود الإدارية"));


                    // =================================================
                    // تفاصيل كل شكوى
                    // =================================================

                    foreach (var complaint in complaints)
                    {
                        col.Item()
                            .PaddingTop(6)
                            .Border(0.8f)
                            .BorderColor(
                                PdfMasterTemplate.Black)
                            .Background(
                                PdfMasterTemplate.White)
                            .Padding(11)
                            .Column(cb =>
                            {
                                cb.Spacing(7);


                                // -------------------------------------
                                // عنوان الشكوى والحالة
                                // -------------------------------------

                                cb.Item()
                                    .Row(row =>
                                    {
                                        row.RelativeItem()
                                            .AlignRight()
                                            .Text(
                                                complaint.Subject
                                                ?? "-")
                                            .FontFamily("Arial")
                                            .FontSize(11)
                                            .Bold()
                                            .FontColor(
                                                PdfMasterTemplate.Black);


                                        row.ConstantItem(100)
                                            .AlignLeft()
                                            .Element(statusContainer =>
                                            {
                                                StatusBadge(
                                                    statusContainer,
                                                    complaint.Status);
                                            });
                                    });


                                // -------------------------------------
                                // خط فاصل
                                // -------------------------------------

                                cb.Item()
                                    .LineHorizontal(0.5f)
                                    .LineColor(
                                        PdfMasterTemplate.Black);


                                // -------------------------------------
                                // بيانات الشكوى
                                // -------------------------------------

                                string senderName =
                                    !string.IsNullOrEmpty(
                                        complaint.User?.FullName)
                                    ? complaint.User!.FullName
                                    : complaint.User?.UserName
                                      ?? "-";


                                cb.Item()
                                    .Row(row =>
                                    {
                                        row.RelativeItem()
                                            .AlignRight()
                                            .Text(
                                                $"المستأجر: {complaint.Tenant?.FullName ?? "-"}")
                                            .FontFamily("Arial")
                                            .FontSize(8.5f)
                                            .FontColor(
                                                PdfMasterTemplate.Black);


                                        row.RelativeItem()
                                            .AlignRight()
                                            .Text(
                                                $"أرسلها: {senderName}")
                                            .FontFamily("Arial")
                                            .FontSize(8.5f)
                                            .FontColor(
                                                PdfMasterTemplate.Black);


                                        row.ConstantItem(110)
                                            .AlignLeft()
                                            .Text(
                                                complaint.SubmittedAt
                                                    .ToString(
                                                        "yyyy/MM/dd HH:mm"))
                                            .FontFamily("Arial")
                                            .FontSize(8.5f)
                                            .FontColor(
                                                PdfMasterTemplate.Black);
                                    });


                                // -------------------------------------
                                // وصف الشكوى
                                // -------------------------------------

                                cb.Item()
                                    .Border(0.5f)
                                    .BorderColor(
                                        PdfMasterTemplate.Black)
                                    .Background(
                                        PdfMasterTemplate.White)
                                    .Padding(9)
                                    .AlignRight()
                                    .Text(
                                        complaint.Description ?? "-")
                                    .FontFamily("Arial")
                                    .FontSize(9)
                                    .FontColor(
                                        PdfMasterTemplate.Black);


                                // =====================================
                                // الردود
                                // =====================================

                                if (complaint.Replies.Any())
                                {
                                    cb.Item()
                                        .PaddingTop(3)
                                        .AlignRight()
                                        .Text(
                                            "الردود والقرارات الإدارية")
                                        .FontFamily("Arial")
                                        .FontSize(9)
                                        .Bold()
                                        .FontColor(
                                            PdfMasterTemplate.Black);


                                    foreach (var reply in complaint.Replies
                                                 .OrderBy(x => x.RepliedAt))
                                    {
                                        // ---------------------------------
                                        // كل رد
                                        // ---------------------------------

                                        cb.Item()
                                            .PaddingTop(3)
                                            .BorderRight(1.2f)
                                            .BorderColor(
                                                PdfMasterTemplate.Black)
                                            .PaddingRight(9)
                                            .Column(replyColumn =>
                                            {
                                                replyColumn.Spacing(3);


                                                // -------------------------
                                                // صاحب الرد + التاريخ
                                                // -------------------------

                                                replyColumn.Item()
                                                    .Row(row =>
                                                    {
                                                        row.RelativeItem()
                                                            .AlignRight()
                                                            .Text(
                                                                reply.RepliedByUser?.FullName
                                                                ?? "الإدارة")
                                                            .FontFamily("Arial")
                                                            .FontSize(8.5f)
                                                            .Bold()
                                                            .FontColor(
                                                                PdfMasterTemplate.Black);


                                                        row.ConstantItem(110)
                                                            .AlignLeft()
                                                            .Text(
                                                                reply.RepliedAt
                                                                    .ToString(
                                                                        "yyyy/MM/dd HH:mm"))
                                                            .FontFamily("Arial")
                                                            .FontSize(7.5f)
                                                            .FontColor(
                                                                PdfMasterTemplate.Black);
                                                    });


                                                // -------------------------
                                                // نص الرد
                                                // -------------------------

                                                replyColumn.Item()
                                                    .AlignRight()
                                                    .Text(
                                                        reply.ReplyText
                                                        ?? "-")
                                                    .FontFamily("Arial")
                                                    .FontSize(8.5f)
                                                    .FontColor(
                                                        PdfMasterTemplate.Black);
                                            });
                                    }
                                }
                                else
                                {
                                    cb.Item()
                                        .PaddingTop(2)
                                        .AlignRight()
                                        .Text(
                                            "لا توجد ردود مسجلة على هذه الشكوى حتى الآن.")
                                        .FontFamily("Arial")
                                        .FontSize(8)
                                        .Italic()
                                        .FontColor(
                                            PdfMasterTemplate.Black);
                                }
                            });
                    }
                });
        }


        // =========================================================
        // بطاقة الإحصائيات
        // =========================================================
        private static void SummaryCard(
            IContainer container,
            string title,
            int value)
        {
            container
                .Border(0.7f)
                .BorderColor(
                    PdfMasterTemplate.Black)
                .Background(
                    PdfMasterTemplate.White)
                .PaddingVertical(9)
                .PaddingHorizontal(6)
                .Column(col =>
                {
                    col.Spacing(3);

                    col.Item()
                        .AlignCenter()
                        .Text(value.ToString())
                        .FontFamily("Arial")
                        .FontSize(16)
                        .Bold()
                        .FontColor(
                            PdfMasterTemplate.Black);

                    col.Item()
                        .AlignCenter()
                        .Text(title)
                        .FontFamily("Arial")
                        .FontSize(7.5f)
                        .FontColor(
                            PdfMasterTemplate.Black);
                });
        }


        // =========================================================
        // رأس خلية الجدول
        // =========================================================
        private static IContainer CellHeaderStyle(
            IContainer container)
        {
            return container
                .Background(
                    PdfMasterTemplate.Black)
                .Border(0.5f)
                .BorderColor(
                    PdfMasterTemplate.Black)
                .PaddingVertical(7)
                .PaddingHorizontal(6)
                .AlignMiddle()
                .DefaultTextStyle(x =>
                    x
                        .FontFamily("Arial")
                        .FontSize(8.5f)
                        .Bold()
                        .FontColor(
                            PdfMasterTemplate.White));
        }


        // =========================================================
        // خلية بيانات الجدول
        // =========================================================
        private static IContainer CellStyle(
            IContainer container)
        {
            return container
                .Background(
                    PdfMasterTemplate.White)
                .BorderBottom(0.5f)
                .BorderColor(
                    PdfMasterTemplate.Black)
                .PaddingVertical(6)
                .PaddingHorizontal(6)
                .AlignMiddle()
                .DefaultTextStyle(x =>
                    x
                        .FontFamily("Arial")
                        .FontSize(8.5f)
                        .FontColor(
                            PdfMasterTemplate.Black));
        }


        // =========================================================
        // Badge الحالة
        // =========================================================
        private static void StatusBadge(
            IContainer container,
            ComplaintStatus status)
        {
            string label = status switch
            {
                ComplaintStatus.New =>
                    "جديدة",

                ComplaintStatus.InProgress =>
                    "قيد المعالجة",

                ComplaintStatus.Resolved =>
                    "تم الحل",

                ComplaintStatus.Closed =>
                    "مغلقة",

                _ =>
                    "غير معروف"
            };


            container
                .Border(0.7f)
                .BorderColor(
                    PdfMasterTemplate.Black)
                .Background(
                    PdfMasterTemplate.White)
                .PaddingHorizontal(7)
                .PaddingVertical(3)
                .AlignCenter()
                .AlignMiddle()
                .Text(label)
                .FontFamily("Arial")
                .FontSize(7.5f)
                .Bold()
                .FontColor(
                    PdfMasterTemplate.Black);
        }


        // =========================================================
        // ترجمة الحالة
        // =========================================================
        private static string GetStatusLabel(
            string status)
        {
            return status.ToLowerInvariant() switch
            {
                "new" =>
                    "جديدة",

                "inprogress" =>
                    "قيد المعالجة",

                "resolved" =>
                    "تم الحل",

                "closed" =>
                    "مغلقة",

                _ =>
                    status
            };
        }
    }
}
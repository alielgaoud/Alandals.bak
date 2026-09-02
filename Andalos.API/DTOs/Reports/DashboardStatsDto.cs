namespace Andalos.API.DTOs.Reports
{
    public class DashboardStatsDto
    {
        // 🏪 إحصائيات المحلات ونسبة الإشغال
        public int TotalUnits { get; set; }
        public int RentedUnits { get; set; }
        public int VacantUnits { get; set; }
        public int MaintenanceUnits { get; set; }
        public decimal OccupancyRate { get; set; } // نسبة الإشغال %

        // 📑 إحصائيات العقود
        public int ActiveContractsCount { get; set; }
        public int ExpiringSoonContractsCount { get; set; } // عقود تنتهي خلال 30 يوم

        // 💰 الإحصائيات المالية للشهر الحالي
        public decimal ThisMonthRevenue { get; set; }
        public decimal ThisMonthExpenses { get; set; }
        public decimal ThisMonthNetIncome { get; set; } // صافي الدخل

        // 📈 الإحصائيات المالية للسنة الحالية
        public decimal YearToDateRevenue { get; set; }
        public decimal YearToDateExpenses { get; set; }
        public decimal YearToDateNetIncome { get; set; }

        // ⚠️ إجمالي المتأخرات غير المحصلة
        public decimal TotalOverdueAmount { get; set; }

        // 👥 إحصائيات الزوار لليوم
        public int TodayPassesCreated { get; set; }
        public int TodayScansAllowed { get; set; }
        public int TodayScansRejected { get; set; }

        // 🛠️ طلبات الصيانة المعلقة
        public int PendingMaintenanceCount { get; set; }

        // 💬 إحصائيات الشكاوى
        public int NewComplaintsCount { get; set; }        // الشكاوى الجديدة (لم تُقرأ/تُعالج بعد)
        public int UnresolvedComplaintsCount { get; set; } // الشكاوى غير المعالجة (جديدة + قيد المعالجة)
    }
    public class MonthlyFinancialBarDto
    {
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal Expenses { get; set; }
        public decimal NetProfit { get; set; }
    }

    public class OverdueReportItemDto
    {
        public int ContractId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public string TenantPhone { get; set; } = string.Empty;
        public string UnitNumber { get; set; } = string.Empty;
        public decimal MonthlyRent { get; set; }
        public decimal TotalDue { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal RemainingAmount { get; set; }
        public DateTime ContractStartDate { get; set; }
        public DateTime ContractEndDate { get; set; }
        public DateTime? LastPaymentDate { get; set; }
    }
    public class UnitOccupancyReportDto
    {
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        public string? UnitName { get; set; }
        public string ActivityType { get; set; } = string.Empty; // 👈 نوع النشاط
        public string Status { get; set; } = string.Empty;
        public decimal Area { get; set; }
        public string? CurrentTenantName { get; set; }
        public string? CurrentTenantPhone { get; set; }
        public decimal? CurrentRentAmount { get; set; }
        public DateTime? ContractEndDate { get; set; }
    }

    public class DailyVisitorTrafficDto
    {
        public DateTime Date { get; set; }
        public int TotalPasses { get; set; }
        public int AllowedEntries { get; set; }
        public int RejectedEntries { get; set; }
    }    // ===== تقرير الإيرادات التفصيلي =====
    public class RevenueReportItemDto
    {
        public int PaymentId { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public string ContractNumber { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public string UnitNumber { get; set; } = string.Empty;
        public string PaymentType { get; set; } = string.Empty; // إيجار، كهرباء، إلخ...
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty; // نقدي، تحويل، شيك...
        public string? ReferenceNumber { get; set; }
        public DateTime PaymentDate { get; set; }
        public string? Notes { get; set; }
    }

    // ===== تقرير المصروفات التفصيلي =====
    public class ExpenseReportItemDto
    {
        public int ExpenseId { get; set; }
        public string ExpenseNumber { get; set; } = string.Empty;
        public string? UnitNumber { get; set; } // قد يكون مصروفاً عاماً بدون محل
        public string? UnitName { get; set; }
        public string? TenantName { get; set; } // في حال كان محملاً على مستأجر
        public bool IsChargedToTenant { get; set; }
        public string ExpenseType { get; set; } = string.Empty; // صيانة، كهرباء مجمع، إلخ...
        public decimal Amount { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string? PaidTo { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? InvoiceNumber { get; set; }
        public string? AttachmentUrl { get; set; }
    }
}
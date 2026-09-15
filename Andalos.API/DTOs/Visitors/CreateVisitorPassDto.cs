using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Visitors
{
    public class CreateVisitorPassDto
    {
        [Required(ErrorMessage = "اسم الزائر مطلوب")]
        [MaxLength(150)]
        public string VisitorName { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم هاتف الزائر مطلوب")]
        [MaxLength(20)]
        public string VisitorPhone { get; set; } = string.Empty;

        public string? NationalId { get; set; }

        public VisitorType VisitorType { get; set; } = VisitorType.Customer;

        public int? UnitId { get; set; } // null إذا كان الزائر للإدارة

        [Required(ErrorMessage = "تاريخ الزيارة مطلوب")]
        public DateTime ValidDate { get; set; } = DateTime.Today;

        public int MaxEntries { get; set; } = 1;

        public string? Purpose { get; set; }

        public string? Notes { get; set; }
    }
    public class VisitorPassResponseDto
    {
        public int Id { get; set; }
        public string PassCode { get; set; } = string.Empty;
        public string VisitorName { get; set; } = string.Empty;
        public string VisitorPhone { get; set; } = string.Empty;
        public string? NationalId { get; set; }
        public string VisitorType { get; set; } = string.Empty;
        public int? UnitId { get; set; }
        public string? UnitNumber { get; set; }
        public string? UnitName { get; set; }
        public DateTime ValidDate { get; set; }
        public int MaxEntries { get; set; }
        public int UsedCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Purpose { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    public class ScanPassDto
    {
        [Required(ErrorMessage = "رمز الباركود مطلوب")]
        public string PassCode { get; set; } = string.Empty;

        public string GateName { get; set; } = "البوابة الرئيسية";
    }

    public class ScanResultDto
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? VisitorName { get; set; }
        public string? VisitorPhone { get; set; }
        public string? VisitorType { get; set; }
        public string? DestinationUnit { get; set; } // اسم المحل المقصود أو الإدارة
        public string? Purpose { get; set; }
        public DateTime ScanTime { get; set; } = DateTime.Now;
        public int RemainingEntries { get; set; }
    }

    public class EntryLogResponseDto
    {
        public int Id { get; set; }
        public string PassCode { get; set; } = string.Empty;
        public string VisitorName { get; set; } = string.Empty;
        public string? DestinationUnit { get; set; }
        public DateTime ScanTime { get; set; }
        public string GateName { get; set; } = string.Empty;
        public string? ScannedBy { get; set; }
        public bool IsAllowed { get; set; }
        public string? RejectReason { get; set; }
    }  // 1. الحارس يقبض الكاش ويُصدر تصريحاً مدفوعاً بـ QR
    public class CreatePaidVisitorPassDto
    {
        [Required(ErrorMessage = "اسم الزائر مطلوب")]
        [MaxLength(150)]
        public string VisitorName { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم هاتف الزائر مطلوب")]
        [MaxLength(20)]
        public string VisitorPhone { get; set; } = string.Empty;

        public string? NationalId { get; set; }

        [Range(0.01, 100000, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
        public decimal Amount { get; set; } = 50; // المبلغ الافتراضي 50 د.ل

        public string GateName { get; set; } = "البوابة الرئيسية";
        public string? Purpose { get; set; }
        public string? Notes { get; set; }
    }

    // 2. المحل يصور الـ QR ويخصم قيمة المشتريات
    public class ProcessPassPurchaseDto
    {
        [Required(ErrorMessage = "رمز كود التصريح مطلوب")]
        public string PassCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "قيمة المشتريات مطلوبة")]
        [Range(0.01, 100000, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
        public decimal PurchaseAmount { get; set; }
        // 👈 جديد: إمكانية تحديد المستأجر للاختبار في Swagger
        public int? TenantId { get; set; }

        public int? UnitId { get; set; } // المحل الذي تمت فيه العملية
    }

    // نتيجة الخصم في المحل
    public class PassPurchaseResultDto
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public string VisitorName { get; set; } = string.Empty;
        public decimal TotalPurchaseAmount { get; set; } // إجمالي فاتورة الشراء
        public decimal ChargedFromPass { get; set; }    // القيمة المخصومة من الـ QR
        public decimal CashDifferenceToPay { get; set; } // الباقي المطلوب كاش من الزائر للمحل
        public decimal PassRemainingBalance { get; set; } // الرصيد المتبقي بالـ QR بعد العملية
        public DateTime TransactionTime { get; set; } = DateTime.Now;
    }

    // 3. ملخص مستحقات المحل لدى الإدارة
    public class TenantPassBalanceDto
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string? TradeName { get; set; }
        public decimal TotalUnsettledAmount { get; set; } // إجمالي المبالغ غير المسددة للمحل
        public int UnsettledTransactionsCount { get; set; } // عدد المبيعات
        public DateTime? LastTransactionDate { get; set; }
    }

    // 4. إجراء تسوية وتسديد مبالغ المحل
    public class ProcessSettlementDto
    {
        [Required]
        public int TenantId { get; set; }

        public SettlementMethod SettlementMethod { get; set; } = SettlementMethod.Cash; // Cash / BankTransfer / RentDeduction

        public string? Notes { get; set; }
    }

    public class SettlementResponseDto
    {
        public int SettlementId { get; set; }
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public decimal TotalSettledAmount { get; set; }
        public string SettlementMethod { get; set; } = string.Empty;
        public int SettledTransactionsCount { get; set; }
        public DateTime SettlementDate { get; set; }
    }

    // 5. ملخص شفت/عهدة الحارس بالبوابة
    public class GatekeeperShiftSummaryDto
    {
        public int ShiftId { get; set; }
        public int UserId { get; set; }
        public string GatekeeperName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int TotalPassesIssued { get; set; }
        public decimal TotalCashCollected { get; set; } // إجمالي الكاش في الصندوق
        public bool IsHandedOver { get; set; }
        public DateTime? HandedOverAt { get; set; }
    }
}
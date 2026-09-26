using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Maintenance
{
    public class CreateMaintenanceRequestDto
    {
        [Required(ErrorMessage = "المحل مطلوب")]
        public int UnitId { get; set; }

        public int? TenantId { get; set; }

        public MaintenanceType Type { get; set; } = MaintenanceType.Electrical;

        public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;

        [Required(ErrorMessage = "وصف العطل مطلوب")]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        public decimal Cost { get; set; } = 0;

        public string? Notes { get; set; }
    }

    public class UpdateMaintenanceStatusDto
    {
        [Required]
        public MaintenanceStatus Status { get; set; }

        public decimal Cost { get; set; }

        public string? Notes { get; set; }

        // 👈 جديد: تحميل المستأجر تلقائياً عند الإكمال
        public bool BilledToTenant { get; set; } = false; // تحميل المستأجر بمبلغ؟
        public decimal BilledAmount { get; set; } = 0; // المبلغ المحمّل (مثلاً 1500 بينما التكلفة 1000)
        public bool RecordCostExpense { get; set; } = true; // تسجيل تكلفة الصيانة (1000) كمصروف تلقائياً
    }

    // 👈 جديد: تحميل المستأجر يدوياً (إجراء مستقل في أي وقت)
    public class ChargeMaintenanceDto
    {
        [Required(ErrorMessage = "مبلغ التحميل مطلوب")]
        [Range(0.01, double.MaxValue, ErrorMessage = "مبلغ التحميل يجب أن يكون أكبر من صفر")]
        public decimal BilledAmount { get; set; } // المبلغ المحمّل على المستأجر (مثلاً 1500)

        public bool RecordCostExpense { get; set; } = true; // تسجيل تكلفة الصيانة (Cost) كمصروف تلقائياً

        public string? Notes { get; set; }
    }

    // 👈 جديد: سداد التحميل المستحق (تحصيل الإيراد)
    public class SettleChargeDto
    {
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

        public string? ReferenceNumber { get; set; } // رقم التحويل/الشيك إن وجد

        public DateTime PaymentDate { get; set; } = DateTime.Now;

        public string? Notes { get; set; }
    }

    // 👈 جديد: عرض تفاصيل التحميل
    public class TenantChargeDto
    {
        public int Id { get; set; }
        public string ChargeNumber { get; set; } = string.Empty;
        public int TenantId { get; set; }
        public string? TenantName { get; set; }
        public int? UnitId { get; set; }
        public string? UnitNumber { get; set; }
        public int? MaintenanceRequestId { get; set; }
        public string? RequestNumber { get; set; } // رقم طلب الصيانة المرتبط
        public decimal Amount { get; set; } // المبلغ المحمّل
        public decimal SettledAmount { get; set; } // المسدد
        public decimal RemainingAmount => Amount - SettledAmount; // المتبقي
        public bool IsSettled { get; set; }
        public string? SettlementReceiptNumber { get; set; }
        public DateTime ChargeDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    public class MaintenanceResponseDto
    {
        public int Id { get; set; }
        public string RequestNumber { get; set; } = string.Empty;
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public int? TenantId { get; set; }
        public string? TenantName { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime? CompletionDate { get; set; }
        public string? Notes { get; set; }

        // 👈 جديد: معلومات التحميل على المستأجر
        public bool BilledToTenant { get; set; } = false;
        public decimal BilledAmount { get; set; } = 0;
        public decimal? ChargeSettledAmount { get; set; } // الجزء المسدد من التحميل
        public bool ChargeIsSettled { get; set; } = false; // هل سُدد التحميل بالكامل
        public decimal? ProfitAmount { get; set; } // الربح (المحمّل - التكلفة) — للعرض فقط
    }
}
using Andalos.API.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Andalos.API.Models
{
    /// <summary>
    /// متعلقات المستأجر (التحميلات المالية)
    /// مثال: صيانة زجاج كلفت للإدارة 1000 د.ل وتم تحميل المستأجر 1500 د.ل
    /// التكلفة (1000) تُسجل كمصروف، والمبلغ المحمّل (1500) يُسجل هنا كإيراد مستحق عند السداد
    /// </summary>
    public class TenantCharge : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string ChargeNumber { get; set; } = string.Empty; // رقم التحميل (مثال: CHG-2026-00001)

        [Required]
        public int TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public int? UnitId { get; set; }
        public Unit? Unit { get; set; }

        public int? ContractId { get; set; } // العقد الساري وقت التحميل (اختياري)

                // 👈 ربط اختياري بطلب الصيانة إذا كان التحميل ناتجاً عن صيانة
                public int? MaintenanceRequestId { get; set; }
                public MaintenanceRequest? MaintenanceRequest { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; } // المبلغ المحمّل على المستأجر (1500)

        [Column(TypeName = "decimal(18,2)")]
        public decimal SettledAmount { get; set; } = 0; // الجزء المسدد فعلاً

        [Required]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime ChargeDate { get; set; } = DateTime.Now;

        public bool IsSettled { get; set; } = false; // هل سُدّد بالكامل

        [MaxLength(50)]
        public string? SettlementReceiptNumber { get; set; } // رقم إيصال السداد

        [MaxLength(300)]
        public string? Notes { get; set; }
    }
}

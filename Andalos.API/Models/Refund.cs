using Andalos.API.Common;
using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class Refund : BaseEntity
    {
        [Required]
        [MaxLength(30)]
        public string RefundNumber { get; set; } = string.Empty; // رقم سند المرتجع التلقائي (RFD-2026-00001)

        [Required]
        public int ContractId { get; set; }
        public Contract? Contract { get; set; }

        public int TenantId { get; set; }
        public int UnitId { get; set; }

        // 👈 اختياري: لربط المرتجع بالسند الأصلي الذي حدث فيه الخطأ
        public int? OriginalPaymentId { get; set; }
        public Payment? OriginalPayment { get; set; }

        public RefundType RefundType { get; set; } = RefundType.Overpayment;

        [Required]
        public decimal Amount { get; set; } // القيمة المالية المرتجعة

        public PaymentMethod RefundMethod { get; set; } = PaymentMethod.Cash; // طريقة إرجاع الأموال

        [Required]
        public DateTime RefundDate { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "يجب ذكر سبب المرتجع بالتفصيل لتوثيقه")]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty; // سبب الإرجاع

        [MaxLength(300)]
        public string? Notes { get; set; }
    }
}
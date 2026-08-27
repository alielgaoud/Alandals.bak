using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Refunds
{
    public class CreateRefundDto
    {
        [Required(ErrorMessage = "العقد مطلوب")]
        public int ContractId { get; set; }

        public int? OriginalPaymentId { get; set; } // اختياري

        public RefundType RefundType { get; set; } = RefundType.Overpayment;

        [Required(ErrorMessage = "المبلغ المرتجع مطلوب")]
        [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
        public decimal Amount { get; set; }

        public PaymentMethod RefundMethod { get; set; } = PaymentMethod.Cash;

        public DateTime RefundDate { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "سبب الارتجاع مطلوب لتبرير العملية")]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public string? Notes { get; set; }
    }
    public class RefundResponseDto
    {
        public int Id { get; set; }
        public string RefundNumber { get; set; } = string.Empty;
        public int ContractId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        public string? OriginalReceiptNumber { get; set; } // رقم الإيصال الأصلي المرتبط به
        public string RefundType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string RefundMethod { get; set; } = string.Empty;
        public DateTime RefundDate { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
}
using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Payments
{
    public class CreatePaymentDto
    {
        [Required(ErrorMessage = "العقد مطلوب")]
        public int ContractId { get; set; }

        [Required(ErrorMessage = "نوع الدفعة مطلوب")]
        public PaymentType PaymentType { get; set; }

        [Required(ErrorMessage = "المبلغ مطلوب")]
        [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
        public decimal Amount { get; set; }

        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

        public string? ReferenceNumber { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.Now;

        public string? Notes { get; set; }
    }
    public class PaymentResponseDto
    {
        public int Id { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public int ContractId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        public string PaymentType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? ReferenceNumber { get; set; }
        public DateTime PaymentDate { get; set; }
        public string? Notes { get; set; }
    }
    public class PaymentSummaryDto
    {
        public int ContractId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public string UnitNumber { get; set; } = string.Empty;
        public decimal TotalDue { get; set; }      // إجمالي المستحق
        public decimal TotalPaid { get; set; }     // إجمالي المدفوع
        public decimal Remaining { get; set; }     // المتبقي
    }
}
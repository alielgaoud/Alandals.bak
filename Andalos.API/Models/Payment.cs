using Andalos.API.Common;
using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class Payment : BaseEntity
    {
        [Required]
        [MaxLength(30)]
        public string ReceiptNumber { get; set; } = string.Empty; // رقم الإيصال التلقائي

        [Required]
        public int ContractId { get; set; }
        public Contract? Contract { get; set; }

        // نضعها مباشرة لتسهيل التقارير بدون Join
        public int TenantId { get; set; }
        public int UnitId { get; set; }

        public PaymentType PaymentType { get; set; } = PaymentType.Rent;

        [Required]
        public decimal Amount { get; set; }

        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

        [MaxLength(100)]
        public string? ReferenceNumber { get; set; } // رقم التحويل البنكي أو الشيك

        [Required]
        public DateTime PaymentDate { get; set; } = DateTime.Now;

        [MaxLength(300)]
        public string? Notes { get; set; }
    }
}
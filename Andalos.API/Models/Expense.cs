using Andalos.API.Common;
using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class Expense : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string ExpenseNumber { get; set; } = string.Empty;

        public int? UnitId { get; set; }
        public Unit? Unit { get; set; }

        // 👈 الجديد: هل المصروف محمل على حساب المستأجر؟
        public bool IsChargedToTenant { get; set; } = false;

        // 👈 الجديد: المستأجر المستهدف بالتحميل
        public int? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public ExpenseType ExpenseType { get; set; } = ExpenseType.Other;

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public DateTime ExpenseDate { get; set; } = DateTime.Now;

        [MaxLength(150)]
        public string? PaidTo { get; set; }

        [Required]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? InvoiceNumber { get; set; }
    }
}
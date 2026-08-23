using Andalos.API.Common;
using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class Expense : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string ExpenseNumber { get; set; } = string.Empty; // رقم السند (مثال: EXP-2026-00001)

        public int? UnitId { get; set; } // اختياري: إذا كان المصروف خاص بمحل معين (إذا null = مصروف عام للمجمع)
        public Unit? Unit { get; set; }

        public ExpenseType ExpenseType { get; set; } = ExpenseType.Other;

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public DateTime ExpenseDate { get; set; } = DateTime.Now;

        [MaxLength(150)]
        public string? PaidTo { get; set; } // المدفوع له (اسم الفني، الشركة، الجهة)

        [Required]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty; // بيان المصروف

        [MaxLength(100)]
        public string? InvoiceNumber { get; set; } // رقم الفاتورة الورقية إن وُجدت
    }
}
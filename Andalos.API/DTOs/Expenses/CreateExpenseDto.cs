using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Expenses
{
    public class CreateExpenseDto
    {
        public int? UnitId { get; set; }
        public ExpenseType ExpenseType { get; set; } = ExpenseType.Other;

        [Required(ErrorMessage = "المبلغ مطلوب")]
        [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
        public decimal Amount { get; set; }

        public DateTime ExpenseDate { get; set; } = DateTime.Now;

        [MaxLength(150)]
        public string? PaidTo { get; set; }

        [Required(ErrorMessage = "بيان المصروف مطلوب")]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        public string? InvoiceNumber { get; set; }

        // 👈 الجديد
        public bool IsChargedToTenant { get; set; } = false;
    }
    public class ExpenseResponseDto
    {
        public int Id { get; set; }
        public string ExpenseNumber { get; set; } = string.Empty;
        public int? UnitId { get; set; }
        public string? UnitNumber { get; set; }
        public string? UnitName { get; set; }
        public string ExpenseType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string? PaidTo { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? InvoiceNumber { get; set; }

        // 👈 الجديد
        public bool IsChargedToTenant { get; set; }
        public int? TenantId { get; set; }
        public string? TenantName { get; set; }
    }
}
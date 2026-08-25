using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Tenants
{
    public class CreateTenantDto
    {
        [Required(ErrorMessage = "اسم المستأجر مطلوب")]
        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم الهوية أو جواز السفر مطلوب")]
        [MaxLength(50)]
        public string NationalId { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        public string? ContactPerson { get; set; }

        public string? Notes { get; set; }
    }
    public class UpdateTenantDto
    {
        [Required(ErrorMessage = "اسم المستأجر مطلوب")]
        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم الهوية مطلوب")]
        [MaxLength(50)]
        public string NationalId { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        public string? ContactPerson { get; set; }

        public string? Notes { get; set; }
    }
    public class TenantResponseDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string NationalId { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }  
    // 👈 إضافة الكلاس المفقود هنا لحل كل أخطاء البناء السبعة
    public class TenantFinancialSummaryDto
    {
        public decimal TotalRequiredRent { get; set; } // إجمالي الإيجار المستحق تاريخياً
        public decimal TotalPaidAmount { get; set; }   // إجمالي المبالغ المدفوعة فعلياً
        public decimal RemainingBalance { get; set; }  // المتبقي غير المدفوع (المتأخرات)
        public int TotalContractsCount { get; set; }     // إجمالي العقود
        public int ActiveContractsCount { get; set; }    // العقود النشطة حالياً
    }
}
using Andalos.API.DTOs.Contracts;
using Andalos.API.DTOs.Payments;
using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Portal
{
    // 1. كشف الحساب الشامل للمستأجر
    public class TenantAccountStatementDto
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public int TotalUnitsRented { get; set; }
        public decimal TotalRequiredRent { get; set; } // إجمالي المطلوب
        public decimal TotalPaidAmount { get; set; }   // إجمالي المدفوع
        public decimal RemainingBalance { get; set; }  // المتبقي عليه
        public List<ContractResponseDto> ActiveContracts { get; set; } = new();
        public List<PaymentResponseDto> PaymentHistory { get; set; } = new();
    }

    // 2. رفع طلب صيانة من المستأجر
    public class TenantCreateMaintenanceDto
    {
        [Required(ErrorMessage = "يرجى تحديد المحل")]
        public int UnitId { get; set; }

        public MaintenanceType Type { get; set; } = MaintenanceType.Electrical;

        public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;

        [Required(ErrorMessage = "وصف المشكلة مطلوب")]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;
    }

    // 3. إنشاء باركود زائر خاص بمحل المستأجر
    public class TenantCreatePassDto
    {
        [Required(ErrorMessage = "يرجى تحديد المحل")]
        public int UnitId { get; set; }

        [Required(ErrorMessage = "اسم الزائر مطلوب")]
        public string VisitorName { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم هاتف الزائر مطلوب")]
        public string VisitorPhone { get; set; } = string.Empty;

        public VisitorType VisitorType { get; set; } = VisitorType.Customer;

        [Required(ErrorMessage = "تاريخ الزيارة مطلوب")]
        public DateTime ValidDate { get; set; } = DateTime.Today;

        public int MaxEntries { get; set; } = 1;

        public string? Purpose { get; set; }
    }

    // 4. إنشاء حساب مستخدم لمستأجر (خاص بالأدمن)
    public class CreateTenantUserAccountDto
    {
        [Required]
        public int TenantId { get; set; }

        [Required]
        [EmailAddress]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;
    }
}
using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Contracts
{
    public class CreateContractDto
    {
        [Required(ErrorMessage = "المستأجر مطلوب")]
        public int TenantId { get; set; }

        [Required(ErrorMessage = "المحل مطلوب")]
        public int UnitId { get; set; }

        [Required(ErrorMessage = "تاريخ بدء العقد مطلوب")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "تاريخ انتهاء العقد مطلوب")]
        public DateTime EndDate { get; set; }

        [Required(ErrorMessage = "قيمة الإيجار مطلوبة")]
        [Range(0.01, double.MaxValue, ErrorMessage = "يجب أن تكون قيمة الإيجار أكبر من صفر")]
        public decimal RentAmount { get; set; }

        public RentCycle RentCycle { get; set; } = RentCycle.Monthly;

        public decimal DepositAmount { get; set; }

        public bool AutoRenew { get; set; } = false;

        public string? Notes { get; set; }

        // بنود اختيارية إضافية يمكن تمريرها عند الإنشاء
        public List<CreateContractItemDto> ExtraItems { get; set; } = new();
    }

    public class CreateContractItemDto
    {
        [Required]
        [MaxLength(150)]
        public string ItemName { get; set; } = string.Empty;

        [Required]
        public decimal Amount { get; set; }

        public string? Notes { get; set; }
    }
    public class ContractResponseDto
    {
        public int Id { get; set; }
        public string ContractNumber { get; set; } = string.Empty;

        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string TenantPhone { get; set; } = string.Empty;

        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal RentAmount { get; set; }
        public string RentCycle { get; set; } = string.Empty;
        public decimal DepositAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool AutoRenew { get; set; }
        public string? Notes { get; set; }
        public List<ContractItemDto> ExtraItems { get; set; } = new();
        public List<ContractDocumentDto> Documents { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class ContractItemDto
    {
        public int Id { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? Notes { get; set; }
    }

    public class ContractDocumentDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string? FileType { get; set; }
    }
}
using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Contracts
{
    public class UpdateContractDto
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

        public decimal? AnnualIncreasePercentage { get; set; }

        public ActivityType ActivityType { get; set; } = ActivityType.Other;

        [MaxLength(100)]
        public string? TradeName { get; set; }

        public bool AutoRenew { get; set; } = false;

        public string? Notes { get; set; }

        // الرسوم والعمولات - تعديل كامل
        public List<UpdateContractFeeDto> ContractFees { get; set; } = new();

        // البنود الإضافية - تعديل كامل
        public List<UpdateContractItemDto> ExtraItems { get; set; } = new();
    }

    public class UpdateContractFeeDto
    {
        // إذا كان null أو 0 => رسم جديد، إذا موجود => تعديل
        public int? Id { get; set; }

        [Required(ErrorMessage = "اسم الرسم مطلوب")]
        [MaxLength(150)]
        public string FeeName { get; set; } = string.Empty;

        [Required]
        public FeeValueType ValueType { get; set; } = FeeValueType.Fixed;

        [Required]
        public FeeFrequency Frequency { get; set; } = FeeFrequency.OneTime;

        [Required]
        [Range(0.01, 1000000, ErrorMessage = "القيمة يجب أن تكون أكبر من صفر")]
        public decimal Value { get; set; }

        public string? Notes { get; set; }
    }

    public class UpdateContractItemDto
    {
        public int? Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string ItemName { get; set; } = string.Empty;

        [Required]
        public decimal Amount { get; set; }

        public string? Notes { get; set; }
    }
}

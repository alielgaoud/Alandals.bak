using Andalos.API.Common;
using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Andalos.API.Models
{
    public class ContractFee : BaseEntity
    {
        [Required]
        public int ContractId { get; set; }
        public Contract? Contract { get; set; }

        [Required]
        [MaxLength(150)]
        public string FeeName { get; set; } = string.Empty; // اسم الرسم (مثال: عمولة مكتب، رسوم توثيق، صيانة شهرياً)

        [Required]
        public FeeValueType ValueType { get; set; } = FeeValueType.Fixed;

        [Required]
        public FeeFrequency Frequency { get; set; } = FeeFrequency.OneTime;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Value { get; set; } // القيمة المدخلة (مثال: 150 أو 5 لـ 5%)

        [MaxLength(250)]
        public string? Notes { get; set; }

        // ===== دالة مساعدة لحساب القيمة الفعلية بالدينار =====
        public decimal CalculateActualAmount(decimal monthlyRent, decimal totalContractValue)
        {
            if (Frequency == FeeFrequency.OneTime)
            {
                // إذا كانت مرة واحدة: النسبة تُحسب من إجمالي قيمة العقد
                return ValueType == FeeValueType.Fixed
                    ? Value
                    : Math.Round(totalContractValue * (Value / 100m), 2);
            }
            else
            {
                // إذا كانت شهرية: النسبة تُحسب من الإيجار الشهري
                return ValueType == FeeValueType.Fixed
                    ? Value
                    : Math.Round(monthlyRent * (Value / 100m), 2);
            }
        }
    }
}
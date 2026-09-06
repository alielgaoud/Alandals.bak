using Andalos.API.Common;
using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Andalos.API.Models
{
    public class Contract : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string ContractNumber { get; set; } = string.Empty; // رقم العقد التلقائي (مثال: CTR-2026-0001)

        [Required]
        public int TenantId { get; set; }
        public Tenant? Tenant { get; set; } // علاقة مع المستأجر

        [Required]
        public int UnitId { get; set; }
        public Unit? Unit { get; set; } // علاقة مع المحل

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public decimal RentAmount { get; set; } // قيمة الإيجار الدوري

        public RentCycle RentCycle { get; set; } = RentCycle.Monthly;

        public decimal DepositAmount { get; set; } // قيمة العربون / الضمان

        public ContractStatus Status { get; set; } = ContractStatus.Pending;

        public bool AutoRenew { get; set; } = false;

        [MaxLength(500)]
        public string? Notes { get; set; }
        // 👈 نسبة الزيادة السنوية المحددة للعقد (%)
        [Column(TypeName = "decimal(18,2)")]
        public decimal? AnnualIncreasePercentage { get; set; }

        // 👈 معرّف العقد السابق (لربط سلسلة التجديدات بنفس المحل)
        public int? ParentContractId { get; set; }
        public Contract? ParentContract { get; set; }

        // المرفقات والبنود الإضافية
        // أضف هذه الخاصية في كلاس Contract
        public ICollection<ContractFee> ContractFees { get; set; } = new List<ContractFee>();
        public ICollection<ContractItem> ContractItems { get; set; } = new List<ContractItem>();
        public ICollection<ContractDocument> ContractDocuments { get; set; } = new List<ContractDocument>();
    }
}
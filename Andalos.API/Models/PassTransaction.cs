using Andalos.API.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Andalos.API.Models
{
    public class PassTransaction : BaseEntity
    {
        [Required]
        public int VisitorPassId { get; set; }
        public VisitorPass? VisitorPass { get; set; }

        public int? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public int? UnitId { get; set; }
        public Unit? Unit { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; } // المبلغ المخصوم من التصريح

        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

        public bool IsSettled { get; set; } = false; // هل سددت الإدارة هذا المبلغ للمحل؟
        public int? SettlementId { get; set; }
        public TenantSettlement? Settlement { get; set; }
    }
}
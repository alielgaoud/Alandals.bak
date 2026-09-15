using Andalos.API.Common;
using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Andalos.API.Models
{
    public class TenantSettlement : BaseEntity
    {
        [Required]
        public int TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; } // إجمالي المبلغ الذي سُدد للمحل

        public DateTime SettlementDate { get; set; } = DateTime.UtcNow;

        public SettlementMethod SettlementMethod { get; set; } = SettlementMethod.Cash;

        public int ProcessedByUserId { get; set; }
        public User? ProcessedByUser { get; set; }

        public ICollection<PassTransaction> Transactions { get; set; } = new List<PassTransaction>();
    }
}
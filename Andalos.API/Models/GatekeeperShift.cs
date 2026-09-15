using Andalos.API.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Andalos.API.Models
{
    public class GatekeeperShift : BaseEntity
    {
        [Required]
        public int UserId { get; set; } // الحارس
        public User? User { get; set; }

        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; } // تُسجل عند إنهاء الشفت

        public int TotalPassesIssued { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCashCollected { get; set; } = 0; // إجمالي الأموال التي قبضها

        public bool IsHandedOver { get; set; } = false; // هل تم تسليم النقدية للإدارة؟
        public DateTime? HandedOverAt { get; set; }
    }
}
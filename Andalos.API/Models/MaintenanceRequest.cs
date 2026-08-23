using Andalos.API.Common;
using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class MaintenanceRequest : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string RequestNumber { get; set; } = string.Empty; // رقم الطلب (مثال: MNT-2026-0001)

        [Required]
        public int UnitId { get; set; }
        public Unit? Unit { get; set; }

        public int? TenantId { get; set; } // اختياري في حال تقديم الطلب من قبل المستأجر
        public Tenant? Tenant { get; set; }

        public MaintenanceType Type { get; set; } = MaintenanceType.Electrical;

        public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;

        public MaintenanceStatus Status { get; set; } = MaintenanceStatus.New;

        [Required]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty; // وصف العطل

        public decimal Cost { get; set; } = 0; // تكلفة الصيانة

        public DateTime RequestDate { get; set; } = DateTime.Now;

        public DateTime? CompletionDate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
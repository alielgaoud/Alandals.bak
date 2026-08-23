using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Maintenance
{
    public class CreateMaintenanceRequestDto
    {
        [Required(ErrorMessage = "المحل مطلوب")]
        public int UnitId { get; set; }

        public int? TenantId { get; set; }

        public MaintenanceType Type { get; set; } = MaintenanceType.Electrical;

        public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;

        [Required(ErrorMessage = "وصف العطل مطلوب")]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        public decimal Cost { get; set; } = 0;

        public string? Notes { get; set; }
    }

    public class UpdateMaintenanceStatusDto
    {
        [Required]
        public MaintenanceStatus Status { get; set; }

        public decimal Cost { get; set; }

        public string? Notes { get; set; }
    }
    public class MaintenanceResponseDto
    {
        public int Id { get; set; }
        public string RequestNumber { get; set; } = string.Empty;
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public int? TenantId { get; set; }
        public string? TenantName { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime? CompletionDate { get; set; }
        public string? Notes { get; set; }
    }
}
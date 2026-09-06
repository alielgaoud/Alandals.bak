using Andalos.API.Common;
using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class Unit : BaseEntity
    {
        [Required]
        [MaxLength(20)]
        public string UnitNumber { get; set; } = string.Empty;

        public UnitStatus Status { get; set; } = UnitStatus.Vacant;

        public decimal Area { get; set; } = 0;

        [MaxLength(50)]
        public string? Floor { get; set; }

        [MaxLength(50)]
        public string? Building { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public decimal? ElectricityMeterStart { get; set; }
    }
}
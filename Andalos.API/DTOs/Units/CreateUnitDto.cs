using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Units
{
    public class CreateUnitDto
    {
        [Required(ErrorMessage = "رقم المحل مطلوب")]
        [MaxLength(20)]
        public string UnitNumber { get; set; } = string.Empty;

        public decimal Area { get; set; } = 0;

        public string? Floor { get; set; }

        public string? Building { get; set; }

        public string? Description { get; set; }

        public string? Notes { get; set; }

        public decimal? ElectricityMeterStart { get; set; }
    }

    public class UpdateUnitDto
    {
        public UnitStatus Status { get; set; }

        public decimal Area { get; set; }

        public string? Floor { get; set; }

        public string? Building { get; set; }

        public string? Description { get; set; }

        public string? Notes { get; set; }

        public decimal? ElectricityMeterStart { get; set; }
    }

    public class UnitResponseDto
    {
        public int Id { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Area { get; set; }
        public string? Floor { get; set; }
        public string? Building { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }
        public decimal? ElectricityMeterStart { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
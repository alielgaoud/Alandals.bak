using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Units
{
    public class CreateUnitDto
    {
        [Required(ErrorMessage = "رقم المحل مطلوب")]
        [MaxLength(20)]
        public string UnitNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "اسم المحل مطلوب")]
        [MaxLength(100)]
        public string UnitName { get; set; } = string.Empty;

        public UnitType UnitType { get; set; } = UnitType.Shop;

        public decimal Area { get; set; } = 0;

        public string? Floor { get; set; }

        public string? Building { get; set; }

        public string? Description { get; set; }

        public string? Notes { get; set; }

        public decimal? ElectricityMeterStart { get; set; }

        public decimal? WaterMeterStart { get; set; }
    }
    public class UpdateUnitDto
    {
        [Required]
        [MaxLength(100)]
        public string UnitName { get; set; } = string.Empty;

        public UnitType UnitType { get; set; }

        public UnitStatus Status { get; set; }

        public decimal Area { get; set; }

        public string? Floor { get; set; }

        public string? Building { get; set; }

        public string? Description { get; set; }

        public string? Notes { get; set; }

        public decimal? ElectricityMeterStart { get; set; }

        public decimal? WaterMeterStart { get; set; }
    }
    public class UnitResponseDto
    {
        public int Id { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public string UnitType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Area { get; set; }
        public string? Floor { get; set; }
        public string? Building { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }
        public decimal? ElectricityMeterStart { get; set; }
        public decimal? WaterMeterStart { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
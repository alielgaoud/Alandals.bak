using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Units
{
    public class CreateUnitDto
    {
        [Required(ErrorMessage = "رقم المحل مطلوب")]
        [MaxLength(20)]
        public string UnitNumber { get; set; } = string.Empty;

        // 👈 تم حذف UnitName من الإنشاء (يتحدد لاحقاً مع المستأجر)

        public ActivityType ActivityType { get; set; } = ActivityType.Other; // 👈 نوع النشاط

        public decimal Area { get; set; } = 0;

        public string? Floor { get; set; }

        public string? Building { get; set; }

        public string? Description { get; set; }

        public string? Notes { get; set; }

        public decimal? ElectricityMeterStart { get; set; }

        // 👈 تم حذف WaterMeterStart
    }

    public class UpdateUnitDto
    {
        // 👈 الاسم اختياري في التعديل (يمكن تغييره عند تغيير المستأجر)
        [MaxLength(100)]
        public string? UnitName { get; set; }

        public ActivityType ActivityType { get; set; } // 👈 نوع النشاط

        public UnitStatus Status { get; set; }

        public decimal Area { get; set; }

        public string? Floor { get; set; }

        public string? Building { get; set; }

        public string? Description { get; set; }

        public string? Notes { get; set; }

        public decimal? ElectricityMeterStart { get; set; }

        // 👈 تم حذف WaterMeterStart
    }

    public class UnitResponseDto
    {
        public int Id { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        public string? UnitName { get; set; } // 👈 اختياري
        public string ActivityType { get; set; } = string.Empty; // 👈 نوع النشاط
        public string Status { get; set; } = string.Empty;
        public decimal Area { get; set; }
        public string? Floor { get; set; }
        public string? Building { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }
        public decimal? ElectricityMeterStart { get; set; }
        // 👈 تم حذف WaterMeterStart
        public DateTime CreatedAt { get; set; }
    }
}
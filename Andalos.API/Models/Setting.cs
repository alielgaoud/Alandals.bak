using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class Setting
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string SettingKey { get; set; } = string.Empty;

        public string? SettingValue { get; set; }

        [Required]
        [MaxLength(100)]
        public string SettingGroup { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? SettingSubGroup { get; set; }

        [MaxLength(50)]
        public string DataType { get; set; } = "String";
        // String, Number, Boolean, Dropdown, Percentage, Color, Time, Date

        [Required]
        [MaxLength(200)]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public string? DefaultValue { get; set; }

        public bool IsRequired { get; set; } = false;

        public bool IsEncrypted { get; set; } = false;

        public int SortOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? UpdatedBy { get; set; }
    }
}
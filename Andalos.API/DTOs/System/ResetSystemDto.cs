using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.System
{
    public class ResetSystemDto
    {
        [Required(ErrorMessage = "كلمة مرور مدير النظام الرئيسي مطلوبة للتأكيد")]
        public string SuperAdminPassword { get; set; } = string.Empty;

        public bool ResetSettingsToDefault { get; set; } = false; // خيار اختياري لإعادة الإعدادات للقيم الافتراضية
    }
}
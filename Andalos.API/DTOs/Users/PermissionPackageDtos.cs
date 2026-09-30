using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Users
{
    public class PermissionModuleDto
    {
        public string ModuleKey { get; set; } = string.Empty;   // Maintenance
        public string ModuleName { get; set; } = string.Empty;  // الصيانة
    }

    public class CreatePermissionPackageDto
    {
        [Required(ErrorMessage = "اسم الباقة مطلوب")]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required(ErrorMessage = "يجب إرسال permissionKeys صراحة")]
        public List<string>? PermissionKeys { get; set; }
    }

    public class UpdatePermissionPackageDto
    {
        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
        public List<string> Modules { get; set; } = new();
        public bool IsActive { get; set; } = true;
    }

    public class PermissionPackageResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public List<string> Modules { get; set; } = new();
        public List<string> ModuleNames { get; set; } = new(); // أسماء عربية
        public List<string> PermissionKeys { get; set; } = new();
    }

    public class AssignPackagesToUserDto
    {
        [Range(1, int.MaxValue)]
        public int UserId { get; set; }

        [Required]
        public List<int>? PackageIds { get; set; }

        [Required]
        public int? ExpectedVersion { get; set; }
    }
}
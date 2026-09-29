using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Users
{
    public class CreateUserByAdminDto
    {
        [Required(ErrorMessage = "اسم الموظف مطلوب")]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "اسم المستخدم للدخول مطلوب")]
        [MaxLength(100)]
        public string UserName { get; set; } = string.Empty; // 👈 تم التحديث

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [MinLength(12, ErrorMessage = "كلمة المرور يجب ألا تقل عن 12 خانة")]
        public string Password { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Phone { get; set; }

        public UserRole Role { get; set; } = UserRole.Admin;
    }
    public class UpdateUserDto
    {
        [Required(ErrorMessage = "اسم الموظف مطلوب")]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "اسم المستخدم مطلوب")]
        [MaxLength(100)]
        public string UserName { get; set; } = string.Empty; // 👈 تم التحديث

        [MaxLength(20)]
        public string? Phone { get; set; }

        public UserRole Role { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class AdminResetPasswordDto
    {
        [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة")]
        [MinLength(12, ErrorMessage = "كلمة المرور يجب ألا تقل عن 12 خانة")]
        public string NewPassword { get; set; } = string.Empty;
    }
    public class UserResponseDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty; // 👈 تم التحديث
        public string? Phone { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
        public int FailedLoginAttempts { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    public class AssignUserPermissionsDto
    {
        public int UserId { get; set; }
        [Required] public List<string>? Permissions { get; set; }
        public long? ExpectedVersion { get; set; }
    }

    public class UserPermissionsResponseDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public List<string> GrantedPermissions { get; set; } = new(); // verified DIRECT only; PUT replaces these only.
        public List<string> EffectivePermissions { get; set; } = new();
        public List<string> PackagePermissions { get; set; } = new();
        public List<string> LegacyPermissions { get; set; } = new();
        public bool ReconciliationRequired { get; set; }
        public long PermissionsVersion { get; set; }
    }
    public class TenantStaffResponseDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new(); // قائمة الصلاحيات الخاصة بالموظف
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

}
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
        [MinLength(6, ErrorMessage = "كلمة المرور يجب ألا تقل عن 6 خانات")]
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
        [MinLength(6, ErrorMessage = "كلمة المرور يجب ألا تقل عن 6 خانات")]
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
}
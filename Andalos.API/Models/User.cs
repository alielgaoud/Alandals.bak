using Andalos.API.Common;
using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class User : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Phone { get; set; }

        public UserRole Role { get; set; } = UserRole.Admin;

        // 👈 جديد: في حال كان المستخدم مستأجراً، يرتبط بملفه
        public int? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public bool IsLocked { get; set; } = false;
        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LastLoginAt { get; set; }
        public DateTime? LockoutEnd { get; set; }
    }
}
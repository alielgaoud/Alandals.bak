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
        public string UserName { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Phone { get; set; }

        public UserRole Role { get; set; } = UserRole.Admin;

        public int? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        [MaxLength(32)]
        public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
        public long PermissionsVersion { get; set; } = 1;
        public bool PermissionsReconciled { get; set; } = true;
        public bool RequiresPasswordChange { get; set; }

        public bool IsLocked { get; set; } = false;
        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LastLoginAt { get; set; }
        public DateTime? LockoutEnd { get; set; }

        // 👈 جديد: قائمة الصلاحيات التفصيلية الممنوحة للمستخدم
        public ICollection<UserPermission> Permissions { get; set; } = new List<UserPermission>();
    }
}
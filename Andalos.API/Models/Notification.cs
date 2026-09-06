using Andalos.API.Common;
using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class Notification : BaseEntity
    {
        // المستقبل (إما مستخدم إداري أو مستأجر)
        public int? UserId { get; set; }
        public User? User { get; set; }

        public int? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        // مجموعة إشعارات (مثل: كل الإدارة، كل المستأجرين)
        [MaxLength(50)]
        public string? TargetGroup { get; set; } // "Admins", "AllTenants", "Accountants"

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        [Required]
        public NotificationType Type { get; set; }

        public NotificationPriority Priority { get; set; } = NotificationPriority.Medium;

        [MaxLength(50)]
        public string? Icon { get; set; } // مثل: "bell", "warning", "check"

        [MaxLength(500)]
        public string? ActionUrl { get; set; } // رابط الانتقال عند الضغط

        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        // بيانات إضافية للربط (اختياري)
        public int? RelatedEntityId { get; set; } // مثل: ComplaintId, PaymentId
        [MaxLength(50)]
        public string? RelatedEntityType { get; set; } // "Complaint", "Payment"

        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }

        // القناة التي أُرسل عبرها
        public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;

        // للجدولة المستقبلية
        public DateTime? ScheduledFor { get; set; }
        public bool IsSent { get; set; } = true;
        public DateTime? SentAt { get; set; }
    }
}
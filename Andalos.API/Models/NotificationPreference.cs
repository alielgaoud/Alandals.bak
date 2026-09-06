using Andalos.API.Common;
using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class NotificationPreference : BaseEntity
    {
        public int? UserId { get; set; }
        public User? User { get; set; }

        public int? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        [Required]
        public NotificationType NotificationType { get; set; }

        // هل يريد استقبال هذا النوع من الإشعارات؟
        public bool InAppEnabled { get; set; } = true;
        public bool PushEnabled { get; set; } = true;
        public bool EmailEnabled { get; set; } = false;
        public bool SmsEnabled { get; set; } = false;

        // الأوقات الهادئة (Quiet Hours) - لا تُرسل إشعارات فيها
        public TimeSpan? QuietHoursStart { get; set; }
        public TimeSpan? QuietHoursEnd { get; set; }
    }
}
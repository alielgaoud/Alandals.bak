using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Notifications
{
    // 1. إنشاء إشعار
    public class CreateNotificationDto
    {
        public int? UserId { get; set; }
        public int? TenantId { get; set; }
        public string? TargetGroup { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        [Required]
        public NotificationType Type { get; set; }

        public NotificationPriority Priority { get; set; } = NotificationPriority.Medium;
        public string? Icon { get; set; }
        public string? ActionUrl { get; set; }
        public string? ImageUrl { get; set; }
        public int? RelatedEntityId { get; set; }
        public string? RelatedEntityType { get; set; }

        // للجدولة
        public DateTime? ScheduledFor { get; set; }
    }

    // 2. عرض الإشعار
    public class NotificationResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string TypeLabel { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? ActionUrl { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string TimeAgo { get; set; } = string.Empty; // مثل "منذ 5 دقائق"
    }

    // 3. تفضيلات الإشعارات
    public class NotificationPreferenceDto
    {
        public NotificationType NotificationType { get; set; }
        public string TypeLabel { get; set; } = string.Empty;
        public bool InAppEnabled { get; set; } = true;
        public bool PushEnabled { get; set; } = true;
        public bool EmailEnabled { get; set; } = false;
        public bool SmsEnabled { get; set; } = false;
    }

    public class UpdatePreferencesDto
    {
        public List<NotificationPreferenceDto> Preferences { get; set; } = new();
        public TimeSpan? QuietHoursStart { get; set; }
        public TimeSpan? QuietHoursEnd { get; set; }
    }

    // 4. Push Subscription DTOs
    public class SubscribeToPushDto
    {
        [Required]
        public string Endpoint { get; set; } = string.Empty;

        [Required]
        public string P256dh { get; set; } = string.Empty;

        [Required]
        public string Auth { get; set; } = string.Empty;

        public string? DeviceInfo { get; set; }
        public string? BrowserType { get; set; }
    }

    // 5. ملخص الإشعارات
    public class NotificationSummaryDto
    {
        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
        public int UrgentCount { get; set; }
        public List<NotificationResponseDto> RecentNotifications { get; set; } = new();
    }
}
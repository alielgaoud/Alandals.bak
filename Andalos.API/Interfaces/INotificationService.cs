using Andalos.API.DTOs.Notifications;
using Andalos.API.Enums;
using Andalos.API.Models;

namespace Andalos.API.Interfaces
{
    public interface INotificationService
    {
        // إنشاء إشعار
        Task<NotificationResponseDto> CreateNotificationAsync(CreateNotificationDto dto);

        // إنشاء إشعار سريع (اختصار)
        Task<NotificationResponseDto> SendToUserAsync(int userId, string title, string message, NotificationType type, string? actionUrl = null, int? relatedEntityId = null);
        Task<NotificationResponseDto> SendToTenantAsync(int tenantId, string title, string message, NotificationType type, string? actionUrl = null, int? relatedEntityId = null);
        Task SendToGroupAsync(string groupName, string title, string message, NotificationType type, string? actionUrl = null);
        Task SendToAllAdminsAsync(string title, string message, NotificationType type, NotificationPriority priority = NotificationPriority.Medium, string? actionUrl = null, int? relatedEntityId = null);

        // استعلام
        Task<NotificationSummaryDto> GetMyNotificationsAsync(int? userId, int? tenantId, int limit = 20);
        Task<List<NotificationResponseDto>> GetAllAsync(int? userId, int? tenantId, bool unreadOnly = false, int limit = 50);
        Task<int> GetUnreadCountAsync(int? userId, int? tenantId);

        // إدارة
        Task<bool> MarkAsReadAsync(int notificationId, int? userId, int? tenantId);
        Task<bool> MarkAllAsReadAsync(int? userId, int? tenantId);
        Task<bool> DeleteAsync(int notificationId, int? userId, int? tenantId);

        // التفضيلات
        Task<List<NotificationPreferenceDto>> GetPreferencesAsync(int? userId, int? tenantId);
        Task<bool> UpdatePreferencesAsync(int? userId, int? tenantId, UpdatePreferencesDto dto);
        Task<bool> IsNotificationEnabledAsync(int? userId, int? tenantId, NotificationType type, NotificationChannel channel);
    }
}
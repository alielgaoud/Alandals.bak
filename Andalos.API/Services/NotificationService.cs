using Andalos.API.Data;
using Andalos.API.DTOs.Notifications;
using Andalos.API.Enums;
using Andalos.API.Hubs;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<NotificationHub> _hub;
        private readonly IPushNotificationService _pushService; // 👈 1. إضافة حقل خدمة الـ Push

        // 👈 2. تحديث الـ Constructor لحقن IPushNotificationService
        public NotificationService(
            AppDbContext db,
            IHubContext<NotificationHub> hub,
            IPushNotificationService pushService)
        {
            _db = db;
            _hub = hub;
            _pushService = pushService;
        }

        // =====================================================
        // 1. إنشاء إشعار كامل (مُحدث بالـ Push Notifications)
        // =====================================================
        public async Task<NotificationResponseDto> CreateNotificationAsync(CreateNotificationDto dto)
        {
            // التحقق من تفضيلات المستخدم
            bool isEnabled = await IsNotificationEnabledAsync(dto.UserId, dto.TenantId, dto.Type, NotificationChannel.InApp);
            if (!isEnabled)
            {
                // لا ننشئ الإشعار إذا كان المستخدم قد أوقف هذا النوع
                return new NotificationResponseDto { Title = "تم تجاهل الإشعار حسب تفضيلات المستخدم" };
            }

            var notification = new Notification
            {
                UserId = dto.UserId,
                TenantId = dto.TenantId,
                TargetGroup = dto.TargetGroup,
                Title = dto.Title,
                Message = dto.Message,
                Type = dto.Type,
                Priority = dto.Priority,
                Icon = dto.Icon ?? GetDefaultIcon(dto.Type),
                ActionUrl = dto.ActionUrl,
                ImageUrl = dto.ImageUrl,
                RelatedEntityId = dto.RelatedEntityId,
                RelatedEntityType = dto.RelatedEntityType,
                Channel = NotificationChannel.InApp,
                ScheduledFor = dto.ScheduledFor,
                IsSent = !dto.ScheduledFor.HasValue,
                SentAt = dto.ScheduledFor.HasValue ? null : DateTime.UtcNow
            };

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            var responseDto = MapToDto(notification);

            // 🔔 إرسال الإشعار اللحظي عبر SignalR + Push Notifications (إذا لم يكن مجدولاً)
            if (!dto.ScheduledFor.HasValue)
            {
                // أ) الإرسال اللحظي داخل التطبيق عبر SignalR
                await SendRealtimeNotificationAsync(notification, responseDto);

                // ب) 👈 جديد: إرسال Push Notification للجوال/المتصفح إذا كان المستخدم مفعل لها
                bool pushEnabled = await IsNotificationEnabledAsync(dto.UserId, dto.TenantId, dto.Type, NotificationChannel.Push);
                if (pushEnabled)
                {
                    // يُنفذ في الخلفية دون تعطيل الـ Request الحالي
                    _ = _pushService.SendPushNotificationAsync(dto.UserId, dto.TenantId, dto.Title, dto.Message, dto.ActionUrl);
                }
            }

            return responseDto;
        }

        // =====================================================
        // 2. اختصارات للإرسال السريع
        // =====================================================
        public async Task<NotificationResponseDto> SendToUserAsync(int userId, string title, string message, NotificationType type, string? actionUrl = null, int? relatedEntityId = null)
        {
            return await CreateNotificationAsync(new CreateNotificationDto
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                ActionUrl = actionUrl,
                RelatedEntityId = relatedEntityId
            });
        }

        public async Task<NotificationResponseDto> SendToTenantAsync(int tenantId, string title, string message, NotificationType type, string? actionUrl = null, int? relatedEntityId = null)
        {
            return await CreateNotificationAsync(new CreateNotificationDto
            {
                TenantId = tenantId,
                Title = title,
                Message = message,
                Type = type,
                ActionUrl = actionUrl,
                RelatedEntityId = relatedEntityId
            });
        }

        public async Task SendToGroupAsync(string groupName, string title, string message, NotificationType type, string? actionUrl = null)
        {
            var notification = new Notification
            {
                TargetGroup = groupName,
                Title = title,
                Message = message,
                Type = type,
                Priority = NotificationPriority.Medium,
                Icon = GetDefaultIcon(type),
                ActionUrl = actionUrl,
                Channel = NotificationChannel.InApp,
                IsSent = true,
                SentAt = DateTime.UtcNow
            };

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            await _hub.Clients.Group(groupName).SendAsync("ReceiveNotification", MapToDto(notification));
        }

        public async Task SendToAllAdminsAsync(string title, string message, NotificationType type, NotificationPriority priority = NotificationPriority.Medium, string? actionUrl = null, int? relatedEntityId = null)
        {
            var adminUsers = await _db.Users
                .Where(u => u.IsActive && u.Role != UserRole.Tenant && u.Role != UserRole.TenantStaff)
                .Select(u => u.Id)
                .ToListAsync();

            foreach (var adminId in adminUsers)
            {
                await CreateNotificationAsync(new CreateNotificationDto
                {
                    UserId = adminId,
                    Title = title,
                    Message = message,
                    Type = type,
                    Priority = priority,
                    ActionUrl = actionUrl,
                    RelatedEntityId = relatedEntityId
                });
            }
        }

        // =====================================================
        // 3. الإرسال اللحظي عبر SignalR
        // =====================================================
        private async Task SendRealtimeNotificationAsync(Notification notification, NotificationResponseDto dto)
        {
            if (notification.UserId.HasValue)
            {
                await _hub.Clients.Group($"user_{notification.UserId}").SendAsync("ReceiveNotification", dto);
            }

            if (notification.TenantId.HasValue)
            {
                await _hub.Clients.Group($"tenant_{notification.TenantId}").SendAsync("ReceiveNotification", dto);
            }

            if (!string.IsNullOrEmpty(notification.TargetGroup))
            {
                await _hub.Clients.Group(notification.TargetGroup).SendAsync("ReceiveNotification", dto);
            }
        }

        // =====================================================
        // 4. جلب إشعاراتي (ملخص)
        // =====================================================
        public async Task<NotificationSummaryDto> GetMyNotificationsAsync(int? userId, int? tenantId, int limit = 20)
        {
            var query = _db.Notifications.Where(n => n.IsActive && n.IsSent);

            if (userId.HasValue)
                query = query.Where(n => n.UserId == userId);
            else if (tenantId.HasValue)
                query = query.Where(n => n.TenantId == tenantId);

            var totalCount = await query.CountAsync();
            var unreadCount = await query.CountAsync(n => !n.IsRead);
            var urgentCount = await query.CountAsync(n => n.Priority == NotificationPriority.Urgent && !n.IsRead);

            var recent = await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(limit)
                .ToListAsync();

            return new NotificationSummaryDto
            {
                TotalCount = totalCount,
                UnreadCount = unreadCount,
                UrgentCount = urgentCount,
                RecentNotifications = recent.Select(MapToDto).ToList()
            };
        }

        // =====================================================
        // 5. جلب كل الإشعارات
        // =====================================================
        public async Task<List<NotificationResponseDto>> GetAllAsync(int? userId, int? tenantId, bool unreadOnly = false, int limit = 50)
        {
            var query = _db.Notifications.Where(n => n.IsActive && n.IsSent);

            if (userId.HasValue)
                query = query.Where(n => n.UserId == userId);
            else if (tenantId.HasValue)
                query = query.Where(n => n.TenantId == tenantId);

            if (unreadOnly)
                query = query.Where(n => !n.IsRead);

            var list = await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(limit)
                .ToListAsync();

            return list.Select(MapToDto).ToList();
        }

        // =====================================================
        // 6. عدد الإشعارات غير المقروءة
        // =====================================================
        public async Task<int> GetUnreadCountAsync(int? userId, int? tenantId)
        {
            var query = _db.Notifications.Where(n => n.IsActive && n.IsSent && !n.IsRead);

            if (userId.HasValue)
                query = query.Where(n => n.UserId == userId);
            else if (tenantId.HasValue)
                query = query.Where(n => n.TenantId == tenantId);

            return await query.CountAsync();
        }

        // =====================================================
        // 7. تعليم كـ مقروء
        // =====================================================
        public async Task<bool> MarkAsReadAsync(int notificationId, int? userId, int? tenantId)
        {
            var notification = await _db.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.IsActive);

            if (notification == null) return false;

            if (userId.HasValue && notification.UserId != userId) return false;
            if (tenantId.HasValue && notification.TenantId != tenantId) return false;

            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            notification.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkAllAsReadAsync(int? userId, int? tenantId)
        {
            var query = _db.Notifications.Where(n => n.IsActive && !n.IsRead);

            if (userId.HasValue)
                query = query.Where(n => n.UserId == userId);
            else if (tenantId.HasValue)
                query = query.Where(n => n.TenantId == tenantId);

            var notifications = await query.ToListAsync();
            foreach (var n in notifications)
            {
                n.IsRead = true;
                n.ReadAt = DateTime.UtcNow;
                n.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int notificationId, int? userId, int? tenantId)
        {
            var notification = await _db.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.IsActive);

            if (notification == null) return false;

            if (userId.HasValue && notification.UserId != userId) return false;
            if (tenantId.HasValue && notification.TenantId != tenantId) return false;

            notification.IsActive = false;
            notification.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        // =====================================================
        // 8. التفضيلات
        // =====================================================
        public async Task<List<NotificationPreferenceDto>> GetPreferencesAsync(int? userId, int? tenantId)
        {
            var query = _db.NotificationPreferences.Where(p => p.IsActive);

            if (userId.HasValue)
                query = query.Where(p => p.UserId == userId);
            else if (tenantId.HasValue)
                query = query.Where(p => p.TenantId == tenantId);

            var existing = await query.ToListAsync();

            var allTypes = Enum.GetValues<NotificationType>();
            var result = new List<NotificationPreferenceDto>();

            foreach (var type in allTypes)
            {
                var pref = existing.FirstOrDefault(p => p.NotificationType == type);
                result.Add(new NotificationPreferenceDto
                {
                    NotificationType = type,
                    TypeLabel = GetTypeLabel(type),
                    InAppEnabled = pref?.InAppEnabled ?? true,
                    PushEnabled = pref?.PushEnabled ?? true,
                    EmailEnabled = pref?.EmailEnabled ?? false,
                    SmsEnabled = pref?.SmsEnabled ?? false
                });
            }

            return result;
        }

        public async Task<bool> UpdatePreferencesAsync(int? userId, int? tenantId, UpdatePreferencesDto dto)
        {
            foreach (var prefDto in dto.Preferences)
            {
                var existing = await _db.NotificationPreferences
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.TenantId == tenantId && p.NotificationType == prefDto.NotificationType);

                if (existing == null)
                {
                    _db.NotificationPreferences.Add(new NotificationPreference
                    {
                        UserId = userId,
                        TenantId = tenantId,
                        NotificationType = prefDto.NotificationType,
                        InAppEnabled = prefDto.InAppEnabled,
                        PushEnabled = prefDto.PushEnabled,
                        EmailEnabled = prefDto.EmailEnabled,
                        SmsEnabled = prefDto.SmsEnabled,
                        QuietHoursStart = dto.QuietHoursStart,
                        QuietHoursEnd = dto.QuietHoursEnd
                    });
                }
                else
                {
                    existing.InAppEnabled = prefDto.InAppEnabled;
                    existing.PushEnabled = prefDto.PushEnabled;
                    existing.EmailEnabled = prefDto.EmailEnabled;
                    existing.SmsEnabled = prefDto.SmsEnabled;
                    existing.QuietHoursStart = dto.QuietHoursStart;
                    existing.QuietHoursEnd = dto.QuietHoursEnd;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IsNotificationEnabledAsync(int? userId, int? tenantId, NotificationType type, NotificationChannel channel)
        {
            var pref = await _db.NotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId && p.TenantId == tenantId && p.NotificationType == type && p.IsActive);

            if (pref == null) return true; // مفعل افتراضياً

            // التحقق من الأوقات الهادئة
            if (pref.QuietHoursStart.HasValue && pref.QuietHoursEnd.HasValue)
            {
                var now = DateTime.Now.TimeOfDay;
                if (now >= pref.QuietHoursStart && now <= pref.QuietHoursEnd)
                    return false;
            }

            return channel switch
            {
                NotificationChannel.InApp => pref.InAppEnabled,
                NotificationChannel.Push => pref.PushEnabled,
                NotificationChannel.Email => pref.EmailEnabled,
                NotificationChannel.SMS => pref.SmsEnabled,
                _ => true
            };
        }

        // =====================================================
        // دوال مساعدة
        // =====================================================
        private NotificationResponseDto MapToDto(Notification n)
        {
            return new NotificationResponseDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type.ToString(),
                TypeLabel = GetTypeLabel(n.Type),
                Priority = n.Priority.ToString(),
                Icon = n.Icon,
                ActionUrl = n.ActionUrl,
                ImageUrl = n.ImageUrl,
                IsRead = n.IsRead,
                ReadAt = n.ReadAt,
                CreatedAt = n.CreatedAt,
                TimeAgo = GetTimeAgo(n.CreatedAt)
            };
        }

        private static string GetTimeAgo(DateTime dateTime)
        {
            var diff = DateTime.UtcNow - dateTime;
            if (diff.TotalMinutes < 1) return "الآن";
            if (diff.TotalMinutes < 60) return $"منذ {(int)diff.TotalMinutes} دقيقة";
            if (diff.TotalHours < 24) return $"منذ {(int)diff.TotalHours} ساعة";
            if (diff.TotalDays < 7) return $"منذ {(int)diff.TotalDays} يوم";
            if (diff.TotalDays < 30) return $"منذ {(int)(diff.TotalDays / 7)} أسبوع";
            return dateTime.ToString("yyyy/MM/dd");
        }

        private static string GetDefaultIcon(NotificationType type) => type switch
        {
            NotificationType.NewComplaint or NotificationType.ComplaintReply or NotificationType.ComplaintStatusChanged => "message-circle",
            NotificationType.NewBankTransfer or NotificationType.BankTransferApproved or NotificationType.BankTransferRejected => "credit-card",
            NotificationType.ContractExpiringSoon or NotificationType.ContractRenewed or NotificationType.ContractTerminated => "file-text",
            NotificationType.PaymentReminder or NotificationType.PaymentOverdue or NotificationType.AutomaticDeduction or NotificationType.PaymentReceived => "dollar-sign",
            NotificationType.NewMaintenanceRequest or NotificationType.MaintenanceStatusChanged => "tool",
            NotificationType.VisitorRejected or NotificationType.VisitorEntered => "user-check",
            NotificationType.System => "settings",
            _ => "bell"
        };

        private static string GetTypeLabel(NotificationType type) => type switch
        {
            NotificationType.NewComplaint => "شكوى جديدة",
            NotificationType.ComplaintReply => "رد على شكوى",
            NotificationType.ComplaintStatusChanged => "تغيير حالة شكوى",
            NotificationType.NewBankTransfer => "حوالة بنكية جديدة",
            NotificationType.BankTransferApproved => "قبول حوالة",
            NotificationType.BankTransferRejected => "رفض حوالة",
            NotificationType.ContractExpiringSoon => "عقد على وشك الانتهاء",
            NotificationType.ContractRenewed => "تجديد عقد",
            NotificationType.ContractTerminated => "إنهاء عقد",
            NotificationType.PaymentReminder => "تذكير باستحقاق",
            NotificationType.PaymentOverdue => "متأخرات مالية",
            NotificationType.AutomaticDeduction => "خصم آلي",
            NotificationType.PaymentReceived => "استلام دفعة",
            NotificationType.NewMaintenanceRequest => "طلب صيانة جديد",
            NotificationType.MaintenanceStatusChanged => "تحديث طلب صيانة",
            NotificationType.VisitorRejected => "رفض دخول زائر",
            NotificationType.VisitorEntered => "دخول زائر",
            NotificationType.System => "إشعار نظام",
            NotificationType.General => "إشعار عام",
            _ => "إشعار"
        };
    }
}
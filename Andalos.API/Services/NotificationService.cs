using Andalos.API.Security;
using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.DTOs.Notifications;
using Andalos.API.Enums;
using Andalos.API.Helpers;
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
        private readonly CurrentUser _current;
        private readonly EffectivePermissions _permissions;
        private readonly IHubContext<NotificationHub> _hub;
        private readonly IPushNotificationService _pushService; // 👈 1. إضافة حقل خدمة الـ Push
        private readonly ISettingService _settings; // 👈 استدعاء الإعدادات


        // 👈 2. تحديث الـ Constructor لحقن IPushNotificationService
        public NotificationService(
            AppDbContext db,
            ISettingService settings,
            IHubContext<NotificationHub> hub,
            IPushNotificationService pushService, CurrentUser current, EffectivePermissions permissions)
        {
            _db = db;
            _current = current;
            _permissions = permissions;
            _settings = settings;
            _hub = hub;
            _pushService = pushService;
        }

        // =====================================================
        // 1. إنشاء إشعار كامل (مُحدث بالـ Push Notifications)
        // =====================================================
        public async Task<NotificationResponseDto> CreateNotificationAsync(CreateNotificationDto dto)
        {
            if (!Enum.IsDefined(dto.Type) || !Enum.IsDefined(dto.Priority)) throw new ArgumentException("Invalid notification type/priority.");
            // 👈 1. هل الإشعارات الداخلية مفعلة من إعدادات النظام ككل؟
            var globalInAppEnabled = await _settings.GetValueAsync(SettingKeys.NotificationInAppEnabled, true);
            if (!globalInAppEnabled) return new NotificationResponseDto { Title = "الإشعارات موقوفة من الإدارة" };
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
                SentAt = dto.ScheduledFor.HasValue ? null : DateTimeHelper.LibyaNow,
            };

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            var responseDto = MapToDto(notification);

            // Durable outbox: the dispatcher observes this row only AFTER the enclosing transaction commits.
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
            if (!Enum.IsDefined(type) || groupName is not ("Admins" or "Accountants" or "AllTenants")) throw new ArgumentException("Invalid notification target/type.");
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
                SentAt = DateTimeHelper.LibyaNow
            };

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            // Delivery is permission-filtered per live recipient by NotificationDispatcher.
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
        public async Task<NotificationSummaryDto> GetMyNotificationsAsync(int? userId, int? tenantId, int limit = 20)
        {
            var query = (await OwnedQueryAsync(userId, tenantId)).Where(n => n.IsActive && n.IsSent);

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

        public async Task<List<NotificationResponseDto>> GetAllAsync(int? userId, int? tenantId, bool unreadOnly = false, int limit = 50)
        {
            var query = (await OwnedQueryAsync(userId, tenantId)).Where(n => n.IsActive && n.IsSent);

            if (unreadOnly)
                query = query.Where(n => !n.IsRead);

            var list = await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(limit)
                .ToListAsync();

            return list.Select(MapToDto).ToList();
        }



        public async Task<int> GetUnreadCountAsync(int? userId, int? tenantId)
        {
            var query = (await OwnedQueryAsync(userId, tenantId)).Where(n => n.IsActive && n.IsSent && !n.IsRead);

            return await query.CountAsync();
        }
        // =====================================================
        // 7. تعليم كـ مقروء
        // =====================================================
        public async Task<bool> MarkAsReadAsync(int notificationId, int? userId, int? tenantId)
        {
            var notification = await (await OwnedQueryAsync(userId, tenantId, write: true))
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.IsActive);

            if (notification == null) return false;


            notification.IsRead = true;
            notification.ReadAt = DateTimeHelper.LibyaNow;
            notification.UpdatedAt = DateTimeHelper.LibyaNow;

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkAllAsReadAsync(int? userId, int? tenantId)
        {
            var query = (await OwnedQueryAsync(userId, tenantId, write: true)).Where(n => n.IsActive && !n.IsRead);

            var notifications = await query.ToListAsync();
            foreach (var n in notifications)
            {
                n.IsRead = true;
                n.ReadAt = DateTimeHelper.LibyaNow;
                n.UpdatedAt = DateTimeHelper.LibyaNow;
            }

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int notificationId, int? userId, int? tenantId)
        {
            var notification = await (await OwnedQueryAsync(userId, tenantId, write: true))
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.IsActive);

            if (notification == null) return false;


            notification.IsActive = false;
            notification.UpdatedAt = DateTimeHelper.LibyaNow;
            await _db.SaveChangesAsync();
            return true;
        }

        // =====================================================
        // 8. التفضيلات
        // =====================================================
        public async Task<List<NotificationPreferenceDto>> GetPreferencesAsync(int? userId, int? tenantId)
        {
            ValidateContext(userId, tenantId);
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
            ValidateContext(userId, tenantId);
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
                    existing.UpdatedAt = DateTimeHelper.LibyaNow;
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

        private void ValidateContext(int? userId, int? tenantId)
        {
            var u = _current.Required;
            if (userId != u.Id || tenantId != (u.IsTenant ? u.TenantId : null)) throw new ForbiddenOperationException();
        }
        private async Task<IQueryable<Notification>> OwnedQueryAsync(int? userId, int? tenantId, bool write = false)
        {
            ValidateContext(userId, tenantId);
            var u = _current.Required;
            var keys = u.IsStaff ? await _permissions.GetAsync(u) : Array.Empty<string>();
            var caps = u.Role == UserRole.TenantStaff ? await _db.TenantStaffPermissions.Where(p => p.UserId == u.Id).Select(p => p.Capability).ToListAsync() : new List<string>();
            var allowed = Enum.GetValues<NotificationType>().Where(t => u.IsStaff
                ? NotificationPrivacy.StaffKey(t) is null || keys.Contains(NotificationPrivacy.StaffKey(t)!, StringComparer.Ordinal)
                : u.Role == UserRole.Tenant || NotificationPrivacy.PortalCapability(t) is null || caps.Contains(NotificationPrivacy.PortalCapability(t)!, StringComparer.Ordinal)).ToArray();
            var q = _db.Notifications.Where(n => allowed.Contains(n.Type));
            if (write)
                return q.Where(n => n.TargetGroup == null &&
                    ((n.UserId == u.Id && (!u.IsTenant || n.TenantId == null || n.TenantId == u.TenantId)) ||
                     (u.Role == UserRole.Tenant && n.UserId == null && n.TenantId == u.TenantId)));
            if (u.IsTenant)
                return q.Where(n => (n.UserId == u.Id && (n.TenantId == null || n.TenantId == u.TenantId)) ||
                    (n.UserId == null && (n.TenantId == u.TenantId && n.TargetGroup == null ||
                     n.TenantId == null && n.TargetGroup == "AllTenants")));
            return q.Where(n => n.UserId == u.Id || (n.UserId == null && n.TenantId == null && (n.TargetGroup == "Admins" || n.TargetGroup == "Accountants")));
        }
        public NotificationResponseDto ToDto(Notification notification) => MapToDto(notification);

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
            var diff = DateTimeHelper.LibyaNow - dateTime;
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
            NotificationType.MaintenanceChargeOffer or NotificationType.MaintenanceChargeApproved or NotificationType.MaintenanceChargeRejected => "clipboard-check",
            NotificationType.NewCircular => "megaphone",
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
            NotificationType.NewCircular => "تعميم جديد",
            NotificationType.ContractRenewed => "تجديد عقد",
            NotificationType.ContractTerminated => "إنهاء عقد",
            NotificationType.PaymentReminder => "تذكير باستحقاق",
            NotificationType.PaymentOverdue => "متأخرات مالية",
            NotificationType.AutomaticDeduction => "خصم آلي",
            NotificationType.PaymentReceived => "استلام دفعة",
            NotificationType.NewMaintenanceRequest => "طلب صيانة جديد",
            NotificationType.MaintenanceStatusChanged => "تحديث طلب صيانة",
            NotificationType.MaintenanceChargeOffer => "عرض صيانة جديد",
            NotificationType.MaintenanceChargeApproved => "قبول عرض صيانة",
            NotificationType.MaintenanceChargeRejected => "رفض عرض صيانة",
            NotificationType.VisitorRejected => "رفض دخول زائر",
            NotificationType.VisitorEntered => "دخول زائر",
            NotificationType.System => "إشعار نظام",
            NotificationType.General => "إشعار عام",
            _ => "إشعار"
        };
    }
}
using Andalos.API.Data;
using Andalos.API.DTOs.Circulars;
using Andalos.API.DTOs.Notifications;
using Andalos.API.Enums;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public class CircularService : ICircularService
    {
        private readonly AppDbContext _db;
        private readonly INotificationService _notification;

        public CircularService(AppDbContext db, INotificationService notification)
        {
            _db = db;
            _notification = notification;
        }

        // =====================================================
        // إنشاء تعميم
        // =====================================================
        public async Task<CircularResponseDto> CreateAsync(CreateCircularDto dto, string createdBy)
        {
            var circular = new Circular
            {
                Title = dto.Title,
                Content = dto.Content,
                Priority = dto.Priority,
                IsPinned = dto.IsPinned,
                PublishAt = dto.PublishAt,
                ExpiresAt = dto.ExpiresAt,
                CreatedBy = createdBy
            };

            // نشر فوري إذا لم يُحدد وقت مستقبلي
            bool publishNow = !dto.PublishAt.HasValue || dto.PublishAt.Value <= DateTimeHelper.LibyaNow;

            if (publishNow)
            {
                circular.IsPublished = true;
                circular.PublishedAt = DateTimeHelper.LibyaNow;
            }

            _db.Circulars.Add(circular);
            await _db.SaveChangesAsync();

            if (publishNow)
                await SendCircularNotificationAsync(circular);

            return MapToResponse(circular);
        }

        // =====================================================
        // تعديل تعميم
        // =====================================================
        public async Task<CircularResponseDto> UpdateAsync(int id, UpdateCircularDto dto, string updatedBy)
        {
            var circular = await _db.Circulars.FirstOrDefaultAsync(c => c.Id == id && c.IsActive);
            if (circular == null)
                throw new KeyNotFoundException("التعميم غير موجود");

            circular.Title = dto.Title;
            circular.Content = dto.Content;
            circular.Priority = dto.Priority;
            circular.IsPinned = dto.IsPinned;
            circular.ExpiresAt = dto.ExpiresAt;
            circular.UpdatedBy = updatedBy;
            circular.UpdatedAt = DateTimeHelper.LibyaNow;

            await _db.SaveChangesAsync();
            return MapToResponse(circular);
        }

        // =====================================================
        // حذف (Soft Delete)
        // =====================================================
        public async Task<bool> DeleteAsync(int id)
        {
            var circular = await _db.Circulars.FirstOrDefaultAsync(c => c.Id == id && c.IsActive);
            if (circular == null) return false;

            circular.IsActive = false;
            circular.UpdatedAt = DateTimeHelper.LibyaNow;
            await _db.SaveChangesAsync();
            return true;
        }

        // =====================================================
        // قائمة الإدارة
        // =====================================================
        public async Task<List<CircularResponseDto>> GetAllAsync()
        {
            return await _db.Circulars
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new CircularResponseDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Content = c.Content,
                    Priority = c.Priority.ToString(),
                    IsPinned = c.IsPinned,
                    PublishAt = c.PublishAt,
                    ExpiresAt = c.ExpiresAt,
                    IsPublished = c.IsPublished,
                    PublishedAt = c.PublishedAt,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<CircularResponseDto?> GetByIdAsync(int id)
        {
            var c = await _db.Circulars.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            return c == null ? null : MapToResponse(c);
        }

        // =====================================================
        // تعاميم المستأجر (البوابة)
        // =====================================================
        public async Task<List<TenantCircularDto>> GetMyCircularsAsync()
        {
            var now = DateTimeHelper.LibyaNow;

            return await _db.Circulars
                .Where(c => c.IsActive && c.IsPublished
                            && (c.ExpiresAt == null || c.ExpiresAt >= now))
                .OrderByDescending(c => c.IsPinned)
                .ThenByDescending(c => c.PublishedAt)
                .Select(c => new TenantCircularDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Content = c.Content,
                    Priority = c.Priority.ToString(),
                    IsPinned = c.IsPinned,
                    PublishedAt = c.PublishedAt ?? c.CreatedAt
                })
                .ToListAsync();
        }

        // =====================================================
        // نشر التعاميم المجدولة المستحقة (للمجدول)
        // =====================================================
        public async Task<int> PublishDueScheduledCircularsAsync()
        {
            var now = DateTimeHelper.LibyaNow;

            var due = await _db.Circulars
                .Where(c => c.IsActive && !c.IsPublished
                            && c.PublishAt != null && c.PublishAt <= now)
                .ToListAsync();

            foreach (var circular in due)
            {
                circular.IsPublished = true;
                circular.PublishedAt = now;
                await _db.SaveChangesAsync();
                await SendCircularNotificationAsync(circular);
            }

            return due.Count;
        }

        // =====================================================
        // إرسال إشعار جماعي لكل المستأجرين + Web Push
        // =====================================================
        private async Task SendCircularNotificationAsync(Circular circular)
        {
            var userIds = await _db.Users
                .AsNoTracking()
                .Where(u => u.IsActive
                            && u.TenantId != null
                            && (u.Role == UserRole.Tenant || u.Role == UserRole.TenantStaff))
                .Select(u => u.Id)
                .ToListAsync();

            var title = $"📢 تعميم جديد: {circular.Title}";
            var message = circular.Content.Length > 150
                ? circular.Content[..150] + "..."
                : circular.Content;

            var priority = circular.Priority switch
            {
                CircularPriority.Urgent => NotificationPriority.Urgent,
                CircularPriority.Important => NotificationPriority.High,
                _ => NotificationPriority.Medium
            };

            foreach (var userId in userIds)
            {
                await _notification.CreateNotificationAsync(new CreateNotificationDto
                {
                    UserId = userId,
                    Title = title,
                    Message = message,
                    Type = NotificationType.NewCircular,
                    Priority = priority,
                    ActionUrl = $"/tenant/circulars/{circular.Id}",
                    RelatedEntityId = circular.Id,
                    RelatedEntityType = "Circular"
                });
            }
        }

        // =====================================================
        private static CircularResponseDto MapToResponse(Circular c) => new()
        {
            Id = c.Id,
            Title = c.Title,
            Content = c.Content,
            Priority = c.Priority.ToString(),
            IsPinned = c.IsPinned,
            PublishAt = c.PublishAt,
            ExpiresAt = c.ExpiresAt,
            IsPublished = c.IsPublished,
            PublishedAt = c.PublishedAt,
            CreatedAt = c.CreatedAt
        };
    }
}
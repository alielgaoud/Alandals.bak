using Andalos.API.Data;
using Andalos.API.DTOs.Maintenance;
using Andalos.API.Enums;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public class MaintenanceService : IMaintenanceService
    {
        private readonly AppDbContext _db;
        private readonly INotificationService _notification;
        private readonly INumberGeneratorService _numberGen;

        public MaintenanceService(AppDbContext db, INotificationService notification, INumberGeneratorService numberGen)
        {
            _db = db;
            _notification = notification;
            _numberGen = numberGen;
        }

        public async Task<List<MaintenanceResponseDto>> GetAllAsync()
        {
            return await _db.MaintenanceRequests
                .Include(m => m.Unit)
                .Include(m => m.Tenant)
                .Where(m => m.IsActive)
                .OrderByDescending(m => m.RequestDate)
                .Select(m => MapToDto(m))
                .ToListAsync();
        }

        public async Task<List<MaintenanceResponseDto>> GetByUnitAsync(int unitId)
        {
            return await _db.MaintenanceRequests
                .Include(m => m.Unit)
                .Include(m => m.Tenant)
                .Where(m => m.UnitId == unitId && m.IsActive)
                .OrderByDescending(m => m.RequestDate)
                .Select(m => MapToDto(m))
                .ToListAsync();
        }

        public async Task<MaintenanceResponseDto?> GetByIdAsync(int id)
        {
            var request = await _db.MaintenanceRequests
                .Include(m => m.Unit)
                .Include(m => m.Tenant)
                .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);

            return request == null ? null : MapToDto(request);
        }

        public async Task<MaintenanceResponseDto> CreateAsync(CreateMaintenanceRequestDto dto)
        {
            var unit = await _db.Units.FirstOrDefaultAsync(u => u.Id == dto.UnitId && u.IsActive);
            if (unit == null)
                throw new KeyNotFoundException("المحل المحدد غير موجود");

            // الآن يستخدم الإعدادات المترابطة {PREFIX}-{YYYY}-{SEQ:4}
            string requestNumber = await _numberGen.GenerateMaintenanceNumberAsync();

            var request = new MaintenanceRequest
            {
                RequestNumber = requestNumber,
                UnitId = dto.UnitId,
                TenantId = dto.TenantId,
                Type = dto.Type,
                Priority = dto.Priority,
                Status = MaintenanceStatus.New,
                Description = dto.Description,
                Cost = dto.Cost,
                Notes = dto.Notes
            };

            _db.MaintenanceRequests.Add(request);
            await _db.SaveChangesAsync();

            var saved = await _db.MaintenanceRequests
                .Include(m => m.Unit)
                .Include(m => m.Tenant)
                .FirstAsync(m => m.Id == request.Id);

            // 🔔 إشعار فوري للإدارة
            _ = _notification.SendToAllAdminsAsync(
                "طلب صيانة جديد 🛠️",
                $"طلب صيانة جديد رقم {requestNumber} للمحل {unit.UnitNumber} - النوع: {dto.Type} - الأولوية: {dto.Priority}",
                NotificationType.NewMaintenanceRequest,
                dto.Priority == MaintenancePriority.Urgent || dto.Priority == MaintenancePriority.High ? NotificationPriority.High : NotificationPriority.Medium,
                $"/admin/maintenance/{saved.Id}",
                saved.Id
            );

            // 🔔 تأكيد للمستأجر إذا الطلب منه
            if (dto.TenantId.HasValue)
            {
                _ = _notification.SendToTenantAsync(
                    dto.TenantId.Value,
                    "تم استلام طلب الصيانة بنجاح ✅",
                    $"تم استلام طلب الصيانة رقم {requestNumber} للمحل {unit.UnitNumber} وسنقوم بمعالجته قريباً.",
                    NotificationType.NewMaintenanceRequest,
                    $"/tenant/maintenance/{saved.Id}",
                    saved.Id
                );
            }

            return MapToDto(saved);
        }

        public async Task<bool> UpdateStatusAsync(int id, UpdateMaintenanceStatusDto dto)
        {
            var request = await _db.MaintenanceRequests
                .Include(m => m.Unit)
                .Include(m => m.Tenant)
                .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);
            if (request == null) return false;

            var oldStatus = request.Status;
            request.Status = dto.Status;
            request.Cost = dto.Cost > 0 ? dto.Cost : request.Cost;
            if (dto.Notes != null) request.Notes = dto.Notes;
            request.UpdatedAt = DateTimeHelper.LibyaNow;

            if (dto.Status == MaintenanceStatus.Completed)
                request.CompletionDate = DateTimeHelper.LibyaNow;

            await _db.SaveChangesAsync();

            // 🔔 إشعار فوري للمستأجر بتغيير الحالة
            if (request.TenantId.HasValue)
            {
                string statusLabel = dto.Status switch
                {
                    MaintenanceStatus.InProgress => "قيد المعالجة",
                    MaintenanceStatus.Completed => "مكتمل",
                    MaintenanceStatus.Cancelled => "ملغى",
                    _ => dto.Status.ToString()
                };

                _ = _notification.SendToTenantAsync(
                    request.TenantId.Value,
                    $"تحديث حالة طلب الصيانة: {statusLabel} 🔧",
                    $"تم تحديث حالة طلب الصيانة رقم {request.RequestNumber} للمحل {request.Unit?.UnitNumber} إلى: {statusLabel}. {(dto.Status == MaintenanceStatus.Completed ? $"التكلفة: {request.Cost:N2} د.ل" : "")}",
                    NotificationType.MaintenanceStatusChanged,
                    $"/tenant/maintenance/{request.Id}",
                    request.Id
                );
            }

            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var request = await _db.MaintenanceRequests.FirstOrDefaultAsync(m => m.Id == id && m.IsActive);
            if (request == null) return false;

            request.IsActive = false;
            request.UpdatedAt = DateTimeHelper.LibyaNow;
            await _db.SaveChangesAsync();
            return true;
        }

        private static MaintenanceResponseDto MapToDto(MaintenanceRequest m)
        {
            return new MaintenanceResponseDto
            {
                Id = m.Id,
                RequestNumber = m.RequestNumber,
                UnitId = m.UnitId,
                UnitNumber = m.Unit?.UnitNumber ?? "",
                UnitName = m.Unit?.UnitNumber ?? "",
                TenantId = m.TenantId,
                TenantName = m.Tenant?.FullName,
                Type = m.Type.ToString(),
                Priority = m.Priority.ToString(),
                Status = m.Status.ToString(),
                Description = m.Description,
                Cost = m.Cost,
                RequestDate = m.RequestDate,
                CompletionDate = m.CompletionDate,
                Notes = m.Notes
            };
        }
    }
}
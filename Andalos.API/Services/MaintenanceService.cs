using Andalos.API.Data;
using Andalos.API.DTOs.Maintenance;
using Andalos.API.Enums;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public class MaintenanceService : IMaintenanceService
    {
        private readonly AppDbContext _db;

        public MaintenanceService(AppDbContext db)
        {
            _db = db;
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

            string requestNumber = await GenerateRequestNumberAsync();

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

            return MapToDto(saved);
        }

        public async Task<bool> UpdateStatusAsync(int id, UpdateMaintenanceStatusDto dto)
        {
            var request = await _db.MaintenanceRequests.FirstOrDefaultAsync(m => m.Id == id && m.IsActive);
            if (request == null) return false;

            request.Status = dto.Status;
            request.Cost = dto.Cost > 0 ? dto.Cost : request.Cost;
            if (dto.Notes != null) request.Notes = dto.Notes;
            request.UpdatedAt = DateTime.UtcNow;

            if (dto.Status == MaintenanceStatus.Completed)
                request.CompletionDate = DateTime.Now;

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var request = await _db.MaintenanceRequests.FirstOrDefaultAsync(m => m.Id == id && m.IsActive);
            if (request == null) return false;

            request.IsActive = false;
            request.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        private async Task<string> GenerateRequestNumberAsync()
        {
            int year = DateTime.Now.Year;
            int count = await _db.MaintenanceRequests.CountAsync(m => m.RequestDate.Year == year);
            return $"MNT-{year}-{(count + 1):D4}";
        }

        private static MaintenanceResponseDto MapToDto(MaintenanceRequest m)
        {
            return new MaintenanceResponseDto
            {
                Id = m.Id,
                RequestNumber = m.RequestNumber,
                UnitId = m.UnitId,
                UnitNumber = m.Unit?.UnitNumber ?? "",
                UnitName = m.Unit?.UnitName ?? "",
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
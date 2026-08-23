using Andalos.API.Data;
using Andalos.API.DTOs.Tenants;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public class TenantService : ITenantService
    {
        private readonly AppDbContext _db;

        public TenantService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<TenantResponseDto>> GetAllAsync()
        {
            return await _db.Tenants
                .Where(t => t.IsActive)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => MapToDto(t))
                .ToListAsync();
        }

        public async Task<TenantResponseDto?> GetByIdAsync(int id)
        {
            var tenant = await _db.Tenants
                .FirstOrDefaultAsync(t => t.Id == id && t.IsActive);

            return tenant == null ? null : MapToDto(tenant);
        }

        public async Task<TenantResponseDto> CreateAsync(CreateTenantDto dto)
        {
            // التحقق من تكرار رقم الهوية
            var exists = await _db.Tenants
                .AnyAsync(t => t.NationalId == dto.NationalId && t.IsActive);

            if (exists)
                throw new InvalidOperationException($"المستأجر ذو الهوية رقم ({dto.NationalId}) مسجل مسبقاً");

            var tenant = new Tenant
            {
                FullName = dto.FullName,
                NationalId = dto.NationalId,
                Phone = dto.Phone,
                ContactPerson = dto.ContactPerson,
                Notes = dto.Notes
            };

            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync();

            return MapToDto(tenant);
        }

        public async Task<TenantResponseDto?> UpdateAsync(int id, UpdateTenantDto dto)
        {
            var tenant = await _db.Tenants
                .FirstOrDefaultAsync(t => t.Id == id && t.IsActive);

            if (tenant == null) return null;

            // التحقق من أن رقم الهوية الجديد لا يخص مستأجر آخر
            var exists = await _db.Tenants
                .AnyAsync(t => t.NationalId == dto.NationalId && t.Id != id && t.IsActive);

            if (exists)
                throw new InvalidOperationException($"رقم الهوية ({dto.NationalId}) مستخدم من قبل مستأجر آخر");

            tenant.FullName = dto.FullName;
            tenant.NationalId = dto.NationalId;
            tenant.Phone = dto.Phone;
            tenant.ContactPerson = dto.ContactPerson;
            tenant.Notes = dto.Notes;
            tenant.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return MapToDto(tenant);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var tenant = await _db.Tenants
                .FirstOrDefaultAsync(t => t.Id == id && t.IsActive);

            if (tenant == null) return false;

            // Soft Delete
            tenant.IsActive = false;
            tenant.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return true;
        }

        // ===== دالة التحويل من Model إلى DTO =====
        private static TenantResponseDto MapToDto(Tenant t)
        {
            return new TenantResponseDto
            {
                Id = t.Id,
                FullName = t.FullName,
                NationalId = t.NationalId,
                Phone = t.Phone,
                ContactPerson = t.ContactPerson,
                Notes = t.Notes,
                CreatedAt = t.CreatedAt
            };
        }
    }
}
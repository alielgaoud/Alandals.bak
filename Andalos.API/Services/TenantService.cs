using Andalos.API.Data;
using Andalos.API.DTOs.Contracts;
using Andalos.API.DTOs.Maintenance;
using Andalos.API.DTOs.Payments;
using Andalos.API.DTOs.Tenants;
using Andalos.API.DTOs.Units;
using Andalos.API.DTOs.Visitors;
using Andalos.API.Enums;
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

        // ==================== CRUD الأساسي ====================

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
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id && t.IsActive);
            return tenant == null ? null : MapToDto(tenant);
        }

        public async Task<TenantResponseDto> CreateAsync(CreateTenantDto dto)
        {
            var exists = await _db.Tenants.AnyAsync(t => t.NationalId == dto.NationalId && t.IsActive);
            if (exists)
                throw new InvalidOperationException($"المستأجر ذو الهوية ({dto.NationalId}) مسجل مسبقاً");

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
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id && t.IsActive);
            if (tenant == null) return null;

            var exists = await _db.Tenants.AnyAsync(t => t.NationalId == dto.NationalId && t.Id != id && t.IsActive);
            if (exists)
                throw new InvalidOperationException($"رقم الهوية ({dto.NationalId}) مستخدم لمستأجر آخر");

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
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id && t.IsActive);
            if (tenant == null) return false;

            tenant.IsActive = false;
            tenant.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        // ==================== 1. البيانات الشخصية ====================
        public async Task<TenantResponseDto?> GetPersonalInfoAsync(int tenantId)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);
            return tenant == null ? null : MapToDto(tenant);
        }

        // ==================== 2. المحلات المستأجرة ====================
        public async Task<List<UnitResponseDto>> GetRentedUnitsAsync(int tenantId)
        {
            var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == tenantId && t.IsActive);
            if (!tenantExists) return new List<UnitResponseDto>();

            var units = await _db.Contracts
                .Include(c => c.Unit)
                .Where(c => c.TenantId == tenantId && c.IsActive && c.Status == ContractStatus.Active && c.Unit != null)
                .Select(c => c.Unit!)
                .Distinct()
                .ToListAsync();

            return units.Select(u => new UnitResponseDto
            {
                Id = u.Id,
                UnitNumber = u.UnitNumber,
                UnitName = u.UnitName,
                UnitType = u.UnitType.ToString(),
                Status = u.Status.ToString(),
                Area = u.Area,
                Floor = u.Floor,
                Building = u.Building,
                Description = u.Description,
                Notes = u.Notes,
                ElectricityMeterStart = u.ElectricityMeterStart,
                WaterMeterStart = u.WaterMeterStart,
                CreatedAt = u.CreatedAt
            }).ToList();
        }

        // ==================== 3. العقود ====================
        public async Task<List<ContractResponseDto>> GetContractsAsync(int tenantId)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);
            if (tenant == null) return new List<ContractResponseDto>();

            var contracts = await _db.Contracts
                .Include(c => c.Unit)
                .Include(c => c.ContractItems)
                .Where(c => c.TenantId == tenantId && c.IsActive)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            return contracts.Select(c => new ContractResponseDto
            {
                Id = c.Id,
                ContractNumber = c.ContractNumber,
                TenantId = c.TenantId,
                TenantName = tenant.FullName,
                TenantPhone = tenant.Phone,
                UnitId = c.UnitId,
                UnitNumber = c.Unit?.UnitNumber ?? "",
                UnitName = c.Unit?.UnitName ?? "",
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                RentAmount = c.RentAmount,
                RentCycle = c.RentCycle.ToString(),
                DepositAmount = c.DepositAmount,
                Status = c.Status.ToString(),
                AutoRenew = c.AutoRenew,
                Notes = c.Notes,
                CreatedAt = c.CreatedAt,
                ExtraItems = c.ContractItems.Select(i => new ContractItemDto
                {
                    Id = i.Id,
                    ItemName = i.ItemName,
                    Amount = i.Amount,
                    Notes = i.Notes
                }).ToList()
            }).ToList();
        }

        // ==================== 4. الدفعات ====================
        public async Task<List<PaymentResponseDto>> GetPaymentsAsync(int tenantId)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);
            if (tenant == null) return new List<PaymentResponseDto>();

            var payments = await _db.Payments
                .Include(p => p.Contract)
                    .ThenInclude(c => c!.Unit)
                .Where(p => p.TenantId == tenantId && p.IsActive)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            return payments.Select(p => new PaymentResponseDto
            {
                Id = p.Id,
                ReceiptNumber = p.ReceiptNumber,
                ContractId = p.ContractId,
                ContractNumber = p.Contract?.ContractNumber ?? "",
                TenantId = p.TenantId,
                TenantName = tenant.FullName,
                UnitId = p.UnitId,
                UnitNumber = p.Contract?.Unit?.UnitNumber ?? "",
                PaymentType = p.PaymentType.ToString(),
                Amount = p.Amount,
                PaymentMethod = p.PaymentMethod.ToString(),
                ReferenceNumber = p.ReferenceNumber,
                PaymentDate = p.PaymentDate,
                Notes = p.Notes
            }).ToList();
        }

        // ==================== 5. الصيانة ====================
        public async Task<List<MaintenanceResponseDto>> GetMaintenanceRequestsAsync(int tenantId)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);
            if (tenant == null) return new List<MaintenanceResponseDto>();

            var requests = await _db.MaintenanceRequests
                .Include(m => m.Unit)
                .Where(m => m.TenantId == tenantId && m.IsActive)
                .OrderByDescending(m => m.RequestDate)
                .ToListAsync();

            return requests.Select(m => new MaintenanceResponseDto
            {
                Id = m.Id,
                RequestNumber = m.RequestNumber,
                UnitId = m.UnitId,
                UnitNumber = m.Unit?.UnitNumber ?? "",
                UnitName = m.Unit?.UnitName ?? "",
                TenantId = m.TenantId,
                TenantName = tenant.FullName,
                Type = m.Type.ToString(),
                Priority = m.Priority.ToString(),
                Status = m.Status.ToString(),
                Description = m.Description,
                Cost = m.Cost,
                RequestDate = m.RequestDate,
                CompletionDate = m.CompletionDate,
                Notes = m.Notes
            }).ToList();
        }

        // ==================== 6. تصاريح الزوار ====================
        public async Task<List<VisitorPassResponseDto>> GetVisitorPassesAsync(int tenantId)
        {
            var unitIds = await _db.Contracts
                .Where(c => c.TenantId == tenantId && c.IsActive)
                .Select(c => c.UnitId)
                .ToListAsync();

            if (!unitIds.Any()) return new List<VisitorPassResponseDto>();

            var passes = await _db.VisitorPasses
                .Include(p => p.Unit)
                .Where(p => p.IsActive && p.UnitId.HasValue && unitIds.Contains(p.UnitId.Value))
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return passes.Select(p => new VisitorPassResponseDto
            {
                Id = p.Id,
                PassCode = p.PassCode,
                VisitorName = p.VisitorName,
                VisitorPhone = p.VisitorPhone,
                NationalId = p.NationalId,
                VisitorType = p.VisitorType.ToString(),
                UnitId = p.UnitId,
                UnitNumber = p.Unit?.UnitNumber,
                UnitName = p.Unit?.UnitName,
                ValidDate = p.ValidDate,
                MaxEntries = p.MaxEntries,
                UsedCount = p.UsedCount,
                Status = p.Status.ToString(),
                Purpose = p.Purpose,
                Notes = p.Notes,
                CreatedAt = p.CreatedAt
            }).ToList();
        }

        // ==================== 7. الملخص المالي ====================
        public async Task<TenantFinancialSummaryDto?> GetFinancialSummaryAsync(int tenantId)
        {
            var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == tenantId && t.IsActive);
            if (!tenantExists) return null;

            var today = DateTime.Today;

            var contracts = await _db.Contracts
                .Where(c => c.TenantId == tenantId && c.IsActive)
                .ToListAsync();

            decimal totalDue = 0;
            foreach (var c in contracts.Where(c => c.Status == ContractStatus.Active))
            {
                int months = (int)((today - c.StartDate).TotalDays / 30);
                if (months < 1) months = 1;
                totalDue += (months * c.RentAmount);
            }

            decimal totalPaid = await _db.Payments
                .Where(p => p.TenantId == tenantId && p.IsActive)
                .SumAsync(p => p.Amount);

            return new TenantFinancialSummaryDto
            {
                TotalRequiredRent = totalDue,
                TotalPaidAmount = totalPaid,
                RemainingBalance = Math.Max(0, totalDue - totalPaid),
                TotalContractsCount = contracts.Count,
                ActiveContractsCount = contracts.Count(c => c.Status == ContractStatus.Active)
            };
        }

        // ==================== دالة التحويل ====================
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
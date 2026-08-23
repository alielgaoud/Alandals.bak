using Andalos.API.Data;
using Andalos.API.DTOs.Contracts;
using Andalos.API.DTOs.Maintenance;
using Andalos.API.DTOs.Payments;
using Andalos.API.DTOs.Portal;
using Andalos.API.DTOs.Visitors;
using Andalos.API.Enums;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Andalos.API.Services
{
    public class TenantPortalService : ITenantPortalService
    {
        private readonly AppDbContext _db;
        private readonly IMaintenanceService _maintenanceService;
        private readonly IVisitorPassService _passService;

        public TenantPortalService(
            AppDbContext db,
            IMaintenanceService maintenanceService,
            IVisitorPassService passService)
        {
            _db = db;
            _maintenanceService = maintenanceService;
            _passService = passService;
        }

        public async Task<TenantAccountStatementDto> GetMyStatementAsync(int tenantId)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);
            if (tenant == null)
                throw new KeyNotFoundException("بيانات المستأجر غير موجودة");

            var today = DateTime.Today;

            // 1. جلب عقود المستأجر
            var contracts = await _db.Contracts
                .Include(c => c.Unit)
                .Include(c => c.ContractItems)
                .Include(c => c.ContractDocuments)
                .Where(c => c.TenantId == tenantId && c.IsActive)
                .ToListAsync();

            // 2. جلب مدفوعات المستأجر
            var payments = await _db.Payments
                .Include(p => p.Contract)
                .ThenInclude(c => c!.Unit)
                .Where(p => p.TenantId == tenantId && p.IsActive)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            // 3. الحسابات المالية
            decimal totalDue = 0;
            foreach (var c in contracts.Where(c => c.Status == ContractStatus.Active))
            {
                int monthsElapsed = (int)((today - c.StartDate).TotalDays / 30);
                if (monthsElapsed < 1) monthsElapsed = 1;
                totalDue += (monthsElapsed * c.RentAmount);
            }

            decimal totalPaid = payments.Sum(p => p.Amount);

            return new TenantAccountStatementDto
            {
                TenantId = tenant.Id,
                TenantName = tenant.FullName,
                Phone = tenant.Phone,
                TotalUnitsRented = contracts.Select(c => c.UnitId).Distinct().Count(),
                TotalRequiredRent = totalDue,
                TotalPaidAmount = totalPaid,
                RemainingBalance = Math.Max(0, totalDue - totalPaid),
                ActiveContracts = contracts.Select(c => new ContractResponseDto
                {
                    Id = c.Id,
                    ContractNumber = c.ContractNumber,
                    TenantId = c.TenantId,
                    TenantName = tenant.FullName,
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
                    Notes = c.Notes
                }).ToList(),
                PaymentHistory = payments.Select(p => new PaymentResponseDto
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
                }).ToList()
            };
        }

        public async Task<List<ContractResponseDto>> GetMyContractsAsync(int tenantId)
        {
            var statement = await GetMyStatementAsync(tenantId);
            return statement.ActiveContracts;
        }

        public async Task<List<PaymentResponseDto>> GetMyPaymentsAsync(int tenantId)
        {
            var statement = await GetMyStatementAsync(tenantId);
            return statement.PaymentHistory;
        }

        public async Task<MaintenanceResponseDto> RequestMaintenanceAsync(int tenantId, TenantCreateMaintenanceDto dto)
        {
            // التحقق من أن هذا المحل مؤجر فعلياً لهذا المستأجر
            var isMyUnit = await _db.Contracts.AnyAsync(c =>
                c.TenantId == tenantId &&
                c.UnitId == dto.UnitId &&
                c.Status == ContractStatus.Active &&
                c.IsActive);

            if (!isMyUnit)
                throw new UnauthorizedAccessException("لا يمكنك رفع طلب صيانة لمحل غير مسجل باسمك");

            return await _maintenanceService.CreateAsync(new CreateMaintenanceRequestDto
            {
                UnitId = dto.UnitId,
                TenantId = tenantId,
                Type = dto.Type,
                Priority = dto.Priority,
                Description = dto.Description
            });
        }

        public async Task<VisitorPassResponseDto> CreateVisitorPassAsync(int tenantId, TenantCreatePassDto dto, string createdBy)
        {
            // التحقق من أن هذا المحل يخص هذا المستأجر
            var isMyUnit = await _db.Contracts.AnyAsync(c =>
                c.TenantId == tenantId &&
                c.UnitId == dto.UnitId &&
                c.Status == ContractStatus.Active &&
                c.IsActive);

            if (!isMyUnit)
                throw new UnauthorizedAccessException("لا يمكنك إنشاء تصريح زائر لمحل ليس تحت إيجارك");

            return await _passService.CreatePassAsync(new CreateVisitorPassDto
            {
                UnitId = dto.UnitId,
                VisitorName = dto.VisitorName,
                VisitorPhone = dto.VisitorPhone,
                VisitorType = dto.VisitorType,
                ValidDate = dto.ValidDate,
                MaxEntries = dto.MaxEntries,
                Purpose = dto.Purpose
            }, createdBy);
        }

        public async Task<List<VisitorPassResponseDto>> GetMyVisitorPassesAsync(int tenantId)
        {
            // جلب أرقام المحلات التابعة للمستأجر
            var unitIds = await _db.Contracts
                .Where(c => c.TenantId == tenantId && c.IsActive)
                .Select(c => c.UnitId)
                .ToListAsync();

            return await _db.VisitorPasses
                .Include(p => p.Unit)
                .Where(p => p.IsActive && p.UnitId.HasValue && unitIds.Contains(p.UnitId.Value))
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new VisitorPassResponseDto
                {
                    Id = p.Id,
                    PassCode = p.PassCode,
                    VisitorName = p.VisitorName,
                    VisitorPhone = p.VisitorPhone,
                    NationalId = p.NationalId,
                    VisitorType = p.VisitorType.ToString(),
                    UnitId = p.UnitId,
                    UnitNumber = p.Unit != null ? p.Unit.UnitNumber : "",
                    UnitName = p.Unit != null ? p.Unit.UnitName : "",
                    ValidDate = p.ValidDate,
                    MaxEntries = p.MaxEntries,
                    UsedCount = p.UsedCount,
                    Status = p.Status.ToString(),
                    Purpose = p.Purpose,
                    Notes = p.Notes,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<bool> CreateTenantUserAccountAsync(CreateTenantUserAccountDto dto)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == dto.TenantId && t.IsActive);
            if (tenant == null)
                throw new KeyNotFoundException("المستأجر غير موجود");

            var emailExists = await _db.Users.AnyAsync(u => u.Email == dto.Email && u.IsActive);
            if (emailExists)
                throw new InvalidOperationException("البريد الإلكتروني مستخدم لحساب آخر");

            var user = new User
            {
                FullName = tenant.FullName,
                Email = dto.Email,
                Phone = tenant.Phone,
                PasswordHash = HashPassword(dto.Password),
                Role = UserRole.Tenant,
                TenantId = tenant.Id,
                IsActive = true
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return true;
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }
}
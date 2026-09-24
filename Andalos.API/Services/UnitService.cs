using Andalos.API.Data;
using Andalos.API.DTOs.Units;
using Andalos.API.Enums;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public class UnitService : IUnitService
    {
        private readonly AppDbContext _db;
        private readonly INotificationService _notification;
        private readonly ISettingService _settings;

        public UnitService(AppDbContext db, INotificationService notification, ISettingService settings)
        {
            _db = db;
            _notification = notification;
            _settings = settings;
        }

        public async Task<List<UnitResponseDto>> GetAllAsync()
        {
            return await _db.Units
                .Where(u => u.IsActive)
                .OrderBy(u => u.UnitNumber)
                .Select(u => MapToDto(u))
                .ToListAsync();
        }

        public async Task<UnitResponseDto?> GetByIdAsync(int id)
        {
            var unit = await _db.Units
                .FirstOrDefaultAsync(u => u.Id == id && u.IsActive);

            return unit == null ? null : MapToDto(unit);
        }

        public async Task<UnitResponseDto> CreateAsync(CreateUnitDto dto)
        {
            var exists = await _db.Units
                .AnyAsync(u => u.UnitNumber == dto.UnitNumber && u.IsActive);

            if (exists)
                throw new InvalidOperationException($"المحل رقم {dto.UnitNumber} موجود مسبقاً");

            // قراءة وحدة المساحة من الإعدادات - مترابطة
            var areaUnit = await _settings.GetValueAsync(Constants.SettingKeys.UnitAreaUnit, "SQM");
            var areaUnitLabel = areaUnit == "SQM" ? "م²" : areaUnit;

            var unit = new Unit
            {
                UnitNumber = dto.UnitNumber,
                Status = UnitStatus.Vacant,
                Area = dto.Area,
                Floor = dto.Floor,
                Building = dto.Building,
                Description = dto.Description,
                Notes = dto.Notes,
                ElectricityMeterStart = dto.ElectricityMeterStart
            };

            _db.Units.Add(unit);
            await _db.SaveChangesAsync();

            // 🔔 إشعار للإدارة بإضافة محل جديد
            _ = _notification.SendToAllAdminsAsync(
                $"تمت إضافة محل جديد: {dto.UnitNumber} 🏬",
                $"تمت إضافة المحل رقم {dto.UnitNumber} - المساحة {dto.Area} {areaUnitLabel} - {dto.Building} {dto.Floor}",
                NotificationType.System,
                NotificationPriority.Low,
                $"/admin/units/{unit.Id}",
                unit.Id
            );

            return MapToDto(unit);
        }

        public async Task<UnitResponseDto?> UpdateAsync(int id, UpdateUnitDto dto)
        {
            var unit = await _db.Units
                .FirstOrDefaultAsync(u => u.Id == id && u.IsActive);

            if (unit == null) return null;

            var oldStatus = unit.Status;
            unit.Status = dto.Status;
            unit.Area = dto.Area;
            unit.Floor = dto.Floor;
            unit.Building = dto.Building;
            unit.Description = dto.Description;
            unit.Notes = dto.Notes;
            unit.ElectricityMeterStart = dto.ElectricityMeterStart;
            unit.UpdatedAt = DateTimeHelper.LibyaNow;

            await _db.SaveChangesAsync();

            // 🔔 إذا تغيرت حالة المحل والمحل مؤجر، نبه المستأجر
            if (oldStatus != dto.Status)
            {
                var activeContract = await _db.Contracts
                    .Include(c => c.Tenant)
                    .FirstOrDefaultAsync(c => c.UnitId == id && c.Status == ContractStatus.Active && c.IsActive);

                if (activeContract != null)
                {
                    string statusLabel = dto.Status switch
                    {
                        UnitStatus.Vacant => "شاغر",
                        UnitStatus.Rented => "مؤجر",
                        UnitStatus.Maintenance => "صيانة",
                        UnitStatus.Reserved => "محجوز",
                        _ => dto.Status.ToString()
                    };

                    _ = _notification.SendToTenantAsync(
                        activeContract.TenantId,
                        $"تحديث حالة محلك {unit.UnitNumber} إلى {statusLabel} 🏬",
                        $"تم تغيير حالة المحل رقم {unit.UnitNumber} من {oldStatus} إلى {statusLabel}. {(dto.Status == UnitStatus.Maintenance ? "سيتم تنفيذ أعمال صيانة، قد يؤثر على النشاط." : "")}",
                        dto.Status == UnitStatus.Maintenance ? NotificationType.MaintenanceStatusChanged : NotificationType.System,
                        $"/tenant/units",
                        unit.Id
                    );
                }

                // إشعار للإدارة أيضاً
                _ = _notification.SendToAllAdminsAsync(
                    $"تغيير حالة المحل {unit.UnitNumber} 🔄",
                    $"تم تغيير حالة المحل {unit.UnitNumber} من {oldStatus} إلى {dto.Status}",
                    NotificationType.System,
                    NotificationPriority.Medium,
                    $"/admin/units/{unit.Id}",
                    unit.Id
                );
            }

            return MapToDto(unit);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var unit = await _db.Units
                .FirstOrDefaultAsync(u => u.Id == id && u.IsActive);

            if (unit == null) return false;

            // تحقق هل لديه عقود نشطة
            var hasActiveContract = await _db.Contracts.AnyAsync(c => c.UnitId == id && c.IsActive && c.Status == ContractStatus.Active);
            if (hasActiveContract)
                throw new InvalidOperationException("لا يمكن حذف محل لديه عقد نشط");

            unit.IsActive = false;
            unit.UpdatedAt = DateTimeHelper.LibyaNow;
            await _db.SaveChangesAsync();

            _ = _notification.SendToAllAdminsAsync(
                $"تم حذف المحل {unit.UnitNumber} 🗑️",
                $"تم حذف المحل رقم {unit.UnitNumber} من النظام",
                NotificationType.System,
                NotificationPriority.Medium,
                $"/admin/units",
                id
            );

            return true;
        }

        public async Task<int> GetCountByStatusAsync(string status)
        {
            if (Enum.TryParse<UnitStatus>(status, true, out var parsedStatus))
            {
                return await _db.Units
                    .CountAsync(u => u.Status == parsedStatus && u.IsActive);
            }
            return 0;
        }

        private static UnitResponseDto MapToDto(Unit u)
        {
            return new UnitResponseDto
            {
                Id = u.Id,
                UnitNumber = u.UnitNumber,
                Status = u.Status.ToString(),
                Area = u.Area,
                Floor = u.Floor,
                Building = u.Building,
                Description = u.Description,
                Notes = u.Notes,
                ElectricityMeterStart = u.ElectricityMeterStart,
                CreatedAt = u.CreatedAt
            };
        }

        // =====================================================
        // جلب سجل المحل التاريخي والمالي الكامل (Unit Passport)
        // =====================================================
        public async Task<UnitHistoryDto?> GetUnitHistoryAsync(int unitId)
        {
            var unit = await _db.Units.FirstOrDefaultAsync(u => u.Id == unitId && u.IsActive);
            if (unit == null) return null;

            // 1. جلب كل عقود المحل مع المستأجرين
            var contracts = await _db.Contracts
                .Include(c => c.Tenant)
                .Where(c => c.UnitId == unitId && c.IsActive)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            // 2. العقد والنشاط الحالي
            var activeContract = contracts.FirstOrDefault(c => c.Status == ContractStatus.Active);
            UnitCurrentTenantDto? currentOccupant = null;

            if (activeContract != null)
            {
                currentOccupant = new UnitCurrentTenantDto
                {
                    ContractId = activeContract.Id,
                    ContractNumber = activeContract.ContractNumber,
                    TenantId = activeContract.TenantId,
                    TenantName = activeContract.Tenant?.FullName ?? "",
                    TenantPhone = activeContract.Tenant?.Phone ?? "",
                    TradeName = activeContract.TradeName,
                    ActivityType = activeContract.ActivityType.ToString(),
                    MonthlyRent = activeContract.RentAmount,
                    StartDate = activeContract.StartDate,
                    EndDate = activeContract.EndDate
                };
            }

            // 3. سجل العقود المترابطة
            var contractHistory = contracts.Select(c => new UnitContractHistoryDto
            {
                ContractId = c.Id,
                ContractNumber = c.ContractNumber,
                TenantName = c.Tenant?.FullName ?? "",
                TenantPhone = c.Tenant?.Phone ?? "",
                TradeName = c.TradeName,
                ActivityType = c.ActivityType.ToString(),
                MonthlyRent = c.RentAmount,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                Status = c.Status.ToString(),
                ParentContractId = c.ParentContractId
            }).ToList();

            // 4. سجل الصيانة
            var maintenanceList = await _db.MaintenanceRequests
                .Include(m => m.Tenant)
                .Where(m => m.UnitId == unitId && m.IsActive)
                .OrderByDescending(m => m.RequestDate)
                .Select(m => new UnitMaintenanceHistoryDto
                {
                    Id = m.Id,
                    RequestNumber = m.RequestNumber,
                    TenantName = m.Tenant != null ? m.Tenant.FullName : "الإدارة",
                    Type = m.Type.ToString(),
                    Cost = m.Cost,
                    Status = m.Status.ToString(),
                    RequestDate = m.RequestDate,
                    Description = m.Description
                })
                .ToListAsync();

            // 5. سجل المصروفات المباشرة
            var expenseList = await _db.Expenses
                .Include(e => e.Tenant)
                .Where(e => e.UnitId == unitId && e.IsActive)
                .OrderByDescending(e => e.ExpenseDate)
                .Select(e => new UnitExpenseHistoryDto
                {
                    Id = e.Id,
                    ExpenseNumber = e.ExpenseNumber,
                    TenantName = e.Tenant != null ? e.Tenant.FullName : "عام",
                    IsChargedToTenant = e.IsChargedToTenant,
                    ExpenseType = e.ExpenseType.ToString(),
                    Amount = e.Amount,
                    ExpenseDate = e.ExpenseDate,
                    Description = e.Description
                })
                .ToListAsync();

            // 6. الحسابات المالية التجميعية للمحل
            var contractIds = contracts.Select(c => c.Id).ToList();
            decimal totalRevenue = await _db.Payments
                .Where(p => contractIds.Contains(p.ContractId) && p.IsActive)
                .SumAsync(p => p.Amount);

            decimal totalMaintenance = maintenanceList.Sum(m => m.Cost);
            decimal totalExpenses = expenseList.Where(e => !e.IsChargedToTenant).Sum(e => e.Amount); // المصروفات التي تتحملها الإدارة

            var financialSummary = new UnitFinancialStatsDto
            {
                TotalRevenueGenerated = totalRevenue,
                TotalMaintenanceCost = totalMaintenance,
                TotalDirectExpenses = totalExpenses,
                NetProfitGenerated = totalRevenue - (totalMaintenance + totalExpenses),
                TotalContractsCount = contracts.Count
            };

            return new UnitHistoryDto
            {
                UnitInfo = MapToDto(unit),
                CurrentOccupant = currentOccupant,
                FinancialSummary = financialSummary,
                ContractHistory = contractHistory,
                MaintenanceHistory = maintenanceList,
                ExpenseHistory = expenseList
            };
        }
    }
}
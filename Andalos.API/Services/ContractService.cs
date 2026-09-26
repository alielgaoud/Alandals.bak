using Andalos.API.Data;
using Andalos.API.DTOs.Contracts;
using Andalos.API.Enums;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public class ContractService : IContractService
    {
        private readonly AppDbContext _db;
        private readonly INumberGeneratorService _numberGen;
        private readonly INotificationService _notification;
        private readonly ISettingService _settings;

        public ContractService(AppDbContext db, INumberGeneratorService numberGen, INotificationService notification, ISettingService settings)
        {
            _db = db;
            _numberGen = numberGen;
            _notification = notification;
            _settings = settings;
        }

        public async Task<List<ContractResponseDto>> GetAllAsync()
        {
            var contracts = await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .Include(c => c.ContractItems)
                .Include(c => c.ContractDocuments)
                .Include(c => c.ContractFees)
                .Include(c => c.ParentContract)
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return contracts.Select(c => MapToDto(c)).ToList();
        }

        public async Task<ContractResponseDto?> GetByIdAsync(int id)
        {
            var contract = await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .Include(c => c.ContractItems)
                .Include(c => c.ContractDocuments)
                .Include(c => c.ContractFees)
                .Include(c => c.ParentContract)
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

            return contract == null ? null : MapToDto(contract);
        }

        public async Task<ContractResponseDto> CreateAsync(CreateContractDto dto)
        {
            var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == dto.TenantId && t.IsActive);
            if (!tenantExists) throw new KeyNotFoundException("المستأجر المحدد غير موجود");

            var unit = await _db.Units.FirstOrDefaultAsync(u => u.Id == dto.UnitId && u.IsActive);
            if (unit == null) throw new KeyNotFoundException("المحل المحدد غير موجود");

            if (unit.Status != UnitStatus.Vacant)
                throw new InvalidOperationException($"المحل المحدد غير شاغر حالياً (حالة المحل الحالية: {unit.Status})");

            string contractNumber = await _numberGen.GenerateContractNumberAsync();
            var defaultDuration = await _settings.GetValueAsync<int>(Constants.SettingKeys.ContractDefaultDuration, 12);
            var defaultCycleStr = await _settings.GetValueAsync(Constants.SettingKeys.RentDefaultCycle, "Monthly");
            var defaultAutoRenew = await _settings.GetValueAsync<bool>(Constants.SettingKeys.ContractAutoRenew, true);

            DateTime endDate = dto.EndDate;
            if (endDate <= dto.StartDate)
            {
                endDate = dto.StartDate.AddMonths(defaultDuration);
            }

            var rentCycle = dto.RentCycle;
            if (!Enum.TryParse<RentCycle>(defaultCycleStr, true, out var parsedCycle))
                parsedCycle = RentCycle.Monthly;

            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _db.Database.BeginTransactionAsync();
                try
                {
                    var contract = new Contract
                    {
                        ContractNumber = contractNumber,
                        TenantId = dto.TenantId,
                        UnitId = dto.UnitId,
                        StartDate = dto.StartDate,
                        EndDate = endDate,
                        RentAmount = dto.RentAmount,
                        RentCycle = rentCycle,
                        DepositAmount = dto.DepositAmount,
                        Status = ContractStatus.Active,
                        ActivityType = dto.ActivityType,
                        TradeName = dto.TradeName,
                        AutoRenew = dto.AutoRenew || defaultAutoRenew,
                        AnnualIncreasePercentage = dto.AnnualIncreasePercentage,
                        Notes = dto.Notes
                    };

                    if (dto.ExtraItems != null && dto.ExtraItems.Any())
                    {
                        foreach (var item in dto.ExtraItems)
                        {
                            contract.ContractItems.Add(new ContractItem
                            {
                                ItemName = item.ItemName,
                                Amount = item.Amount,
                                Notes = item.Notes
                            });
                        }
                    }

                    if (dto.ContractFees != null && dto.ContractFees.Any())
                    {
                        foreach (var fee in dto.ContractFees)
                        {
                            contract.ContractFees.Add(new ContractFee
                            {
                                FeeName = fee.FeeName,
                                ValueType = fee.ValueType,
                                Frequency = fee.Frequency,
                                Value = fee.Value,
                                Notes = fee.Notes
                            });
                        }
                    }

                    _db.Contracts.Add(contract);

                    unit.Status = UnitStatus.Rented;
                    unit.UpdatedAt = DateTime.UtcNow;

                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var savedContract = await _db.Contracts
                        .Include(c => c.Tenant)
                        .Include(c => c.Unit)
                        .Include(c => c.ContractItems)
                        .Include(c => c.ContractDocuments)
                        .Include(c => c.ContractFees)
                        .Include(c => c.ParentContract)
                        .FirstAsync(c => c.Id == contract.Id);

                    _ = _notification.SendToTenantAsync(
                        savedContract.TenantId,
                        "تفعيل عقد إيجار جديد 📜",
                        $"مرحباً بك، تم إصدار وتفعيل عقدك رقم {savedContract.ContractNumber} للمحل رقم ({unit.UnitNumber}) بنجاح.",
                        NotificationType.ContractRenewed,
                        $"/tenant/contracts/{savedContract.Id}",
                        savedContract.Id
                    );

                    return MapToDto(savedContract);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task<ContractResponseDto> UpdateAsync(int id, UpdateContractDto dto)
        {
            if (dto.EndDate <= dto.StartDate)
                throw new InvalidOperationException("تاريخ انتهاء العقد يجب أن يكون بعد تاريخ البداية");

            var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == dto.TenantId && t.IsActive);
            if (!tenantExists) throw new KeyNotFoundException("المستأجر المحدد غير موجود");

            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _db.Database.BeginTransactionAsync();
                try
                {
                    var contract = await _db.Contracts
                        .Include(c => c.ContractFees)
                        .Include(c => c.ContractItems)
                        .Include(c => c.Unit)
                        .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

                    if (contract == null)
                        throw new KeyNotFoundException("العقد غير موجود");

                    if (contract.Status == ContractStatus.Terminated || contract.Status == ContractStatus.Renewed)
                        throw new InvalidOperationException($"لا يمكن تعديل عقد بحالة {contract.Status}");

                    Unit? newUnit = null;
                    Unit? oldUnit = contract.Unit;

                    if (dto.UnitId != contract.UnitId)
                    {
                        newUnit = await _db.Units.FirstOrDefaultAsync(u => u.Id == dto.UnitId && u.IsActive);
                        if (newUnit == null) throw new KeyNotFoundException("المحل الجديد غير موجود");

                        if (newUnit.Status != UnitStatus.Vacant)
                            throw new InvalidOperationException($"المحل الجديد غير شاغر (حالة: {newUnit.Status})");

                        var hasActiveContractOnNewUnit = await _db.Contracts
                            .AnyAsync(c => c.UnitId == dto.UnitId && c.Id != id && c.IsActive && c.Status == ContractStatus.Active);
                        if (hasActiveContractOnNewUnit)
                            throw new InvalidOperationException("المحل الجديد لديه عقد نشط آخر");
                    }
                    else
                    {
                        var overlapping = await _db.Contracts
                            .AnyAsync(c => c.UnitId == dto.UnitId && c.Id != id && c.IsActive && c.Status == ContractStatus.Active
                                && c.StartDate < dto.EndDate && dto.StartDate < c.EndDate);
                        if (overlapping)
                            throw new InvalidOperationException("يوجد تداخل تواريخ مع عقد نشط آخر لنفس المحل");
                    }

                    var oldRent = contract.RentAmount;
                    var oldUnitId = contract.UnitId;

                    contract.TenantId = dto.TenantId;
                    contract.UnitId = dto.UnitId;
                    contract.StartDate = dto.StartDate;
                    contract.EndDate = dto.EndDate;
                    contract.RentAmount = dto.RentAmount;
                    contract.RentCycle = dto.RentCycle;
                    contract.DepositAmount = dto.DepositAmount;
                    contract.AnnualIncreasePercentage = dto.AnnualIncreasePercentage;
                    contract.ActivityType = dto.ActivityType;
                    contract.TradeName = dto.TradeName;
                    contract.AutoRenew = dto.AutoRenew;
                    contract.Notes = dto.Notes;
                    contract.UpdatedAt = DateTimeHelper.LibyaNow;

                    if (dto.UnitId != oldUnitId)
                    {
                        if (oldUnit != null)
                        {
                            oldUnit.Status = UnitStatus.Vacant;
                            oldUnit.UpdatedAt = DateTimeHelper.LibyaNow;
                        }
                        if (newUnit != null)
                        {
                            newUnit.Status = UnitStatus.Rented;
                            newUnit.UpdatedAt = DateTimeHelper.LibyaNow;
                        }
                    }

                    if (dto.ContractFees != null)
                    {
                        var existingFees = contract.ContractFees.Where(f => f.IsActive).ToList();
                        var dtoFeesById = dto.ContractFees.Where(f => f.Id.HasValue && f.Id.Value > 0)
                                                          .ToDictionary(f => f.Id!.Value, f => f);

                        foreach (var existing in existingFees)
                        {
                            if (!dtoFeesById.ContainsKey(existing.Id))
                            {
                                existing.IsActive = false;
                                existing.UpdatedAt = DateTimeHelper.LibyaNow;
                            }
                        }

                        foreach (var feeDto in dto.ContractFees)
                        {
                            if (feeDto.Id.HasValue && feeDto.Id.Value > 0)
                            {
                                var existing = existingFees.FirstOrDefault(f => f.Id == feeDto.Id.Value);
                                if (existing != null)
                                {
                                    existing.FeeName = feeDto.FeeName;
                                    existing.ValueType = feeDto.ValueType;
                                    existing.Frequency = feeDto.Frequency;
                                    existing.Value = feeDto.Value;
                                    existing.Notes = feeDto.Notes;
                                    existing.UpdatedAt = DateTimeHelper.LibyaNow;
                                }
                            }
                            else
                            {
                                contract.ContractFees.Add(new ContractFee
                                {
                                    FeeName = feeDto.FeeName,
                                    ValueType = feeDto.ValueType,
                                    Frequency = feeDto.Frequency,
                                    Value = feeDto.Value,
                                    Notes = feeDto.Notes
                                });
                            }
                        }
                    }

                    if (dto.ExtraItems != null)
                    {
                        var existingItems = contract.ContractItems.Where(i => i.IsActive).ToList();
                        var dtoItemsById = dto.ExtraItems.Where(i => i.Id.HasValue && i.Id.Value > 0)
                                                         .ToDictionary(i => i.Id!.Value, i => i);

                        foreach (var existing in existingItems)
                        {
                            if (!dtoItemsById.ContainsKey(existing.Id))
                            {
                                existing.IsActive = false;
                                existing.UpdatedAt = DateTimeHelper.LibyaNow;
                            }
                        }

                        foreach (var itemDto in dto.ExtraItems)
                        {
                            if (itemDto.Id.HasValue && itemDto.Id.Value > 0)
                            {
                                var existing = existingItems.FirstOrDefault(i => i.Id == itemDto.Id.Value);
                                if (existing != null)
                                {
                                    existing.ItemName = itemDto.ItemName;
                                    existing.Amount = itemDto.Amount;
                                    existing.Notes = itemDto.Notes;
                                    existing.UpdatedAt = DateTimeHelper.LibyaNow;
                                }
                            }
                            else
                            {
                                contract.ContractItems.Add(new ContractItem
                                {
                                    ItemName = itemDto.ItemName,
                                    Amount = itemDto.Amount,
                                    Notes = itemDto.Notes
                                });
                            }
                        }
                    }

                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var saved = await _db.Contracts
                        .Include(c => c.Tenant)
                        .Include(c => c.Unit)
                        .Include(c => c.ContractItems.Where(i => i.IsActive))
                        .Include(c => c.ContractDocuments.Where(d => d.IsActive))
                        .Include(c => c.ContractFees.Where(f => f.IsActive))
                        .Include(c => c.ParentContract)
                        .FirstAsync(c => c.Id == id);

                    if (oldRent != dto.RentAmount || oldUnitId != dto.UnitId)
                    {
                        _ = _notification.SendToTenantAsync(
                            saved.TenantId,
                            "تم تحديث بيانات عقد الإيجار ✏️",
                            $"تم تعديل عقدك رقم {saved.ContractNumber}. الإيجار الجديد: {saved.RentAmount:N2} د.ل للمحل {saved.Unit?.UnitNumber}.",
                            NotificationType.ContractRenewed,
                            $"/tenant/contracts/{saved.Id}",
                            saved.Id
                        );
                    }

                    return MapToDto(saved);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task<bool> UpdateStatusAsync(int id, ContractStatus newStatus)
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _db.Database.BeginTransactionAsync();
                try
                {
                    var contract = await _db.Contracts
                        .Include(c => c.Unit)
                        .Include(c => c.Tenant)
                        .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

                    if (contract == null) return false;

                    contract.Status = newStatus;
                    contract.UpdatedAt = DateTimeHelper.LibyaNow;

                    if (contract.Unit != null && (newStatus == ContractStatus.Expired || newStatus == ContractStatus.Terminated))
                    {
                        contract.Unit.Status = UnitStatus.Vacant;
                        contract.UpdatedAt = DateTimeHelper.LibyaNow;
                    }
                    else if (contract.Unit != null && newStatus == ContractStatus.Active)
                    {
                        contract.Unit.Status = UnitStatus.Rented;
                        contract.Unit.UpdatedAt = DateTimeHelper.LibyaNow;
                    }

                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    string title = newStatus switch
                    {
                        ContractStatus.Terminated => "تم إنهاء عقد الإيجار ⚠️",
                        ContractStatus.Expired => "انتهى عقد الإيجار ⏰",
                        ContractStatus.Active => "تم تفعيل عقد الإيجار ✅",
                        ContractStatus.Pending => "عقدك في حالة انتظار ⏳",
                        _ => $"تحديث حالة العقد: {newStatus}"
                    };

                    string message = newStatus switch
                    {
                        ContractStatus.Terminated => $"نعلمكم بأنه تم إنهاء العقد رقم {contract.ContractNumber} للمحل {contract.Unit?.UnitNumber} رسمياً.",
                        ContractStatus.Expired => $"انتهى عقدك رقم {contract.ContractNumber} للمحل {contract.Unit?.UnitNumber}. يرجى مراجعة الإدارة للتجديد.",
                        ContractStatus.Active => $"تم تفعيل عقدك رقم {contract.ContractNumber} للمحل {contract.Unit?.UnitNumber} بنجاح.",
                        ContractStatus.Pending => $"عقدك رقم {contract.ContractNumber} أصبح في حالة انتظار.",
                        _ => $"تم تحديث حالة عقدك {contract.ContractNumber} إلى {newStatus}"
                    };

                    NotificationType notifType = newStatus == ContractStatus.Terminated ? NotificationType.ContractTerminated :
                                                newStatus == ContractStatus.Expired ? NotificationType.ContractExpiringSoon :
                                                NotificationType.ContractRenewed;

                    _ = _notification.SendToTenantAsync(
                        contract.TenantId,
                        title,
                        message,
                        notifType,
                        $"/tenant/contracts/{contract.Id}",
                        contract.Id
                    );

                    return true;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    return false;
                }
            });
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _db.Database.BeginTransactionAsync();
                try
                {
                    var contract = await _db.Contracts
                        .Include(c => c.Unit)
                        .Include(c => c.Tenant)
                        .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

                    if (contract == null) return false;

                    contract.IsActive = false;
                    contract.UpdatedAt = DateTimeHelper.LibyaNow;

                    if (contract.Unit != null && contract.Status == ContractStatus.Active)
                    {
                        contract.Unit.Status = UnitStatus.Vacant;
                        contract.Unit.UpdatedAt = DateTimeHelper.LibyaNow;
                    }

                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    if (contract.Tenant != null)
                    {
                        _ = _notification.SendToTenantAsync(
                            contract.TenantId,
                            "تم حذف عقد الإيجار 🗑️",
                            $"تم حذف العقد رقم {contract.ContractNumber} للمحل {contract.Unit?.UnitNumber} من النظام.",
                            NotificationType.ContractTerminated,
                            $"/tenant/contracts",
                            contract.Id
                        );
                    }

                    return true;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    return false;
                }
            });
        }

        public async Task<ContractResponseDto> RenewAsync(int contractId, RenewContractDto dto)
        {
            var oldContract = await _db.Contracts
                .Include(c => c.ContractFees)
                .Include(c => c.ContractItems)
                .FirstOrDefaultAsync(c => c.Id == contractId && c.IsActive);

            if (oldContract == null) throw new KeyNotFoundException("العقد المراد تجديده غير موجود");

            if (oldContract.Status == ContractStatus.Terminated || oldContract.Status == ContractStatus.Renewed)
                throw new InvalidOperationException("لا يمكن تجديد عقد مفسوخ أو تم تجديده مسبقاً");

            decimal oldRent = oldContract.RentAmount;
            decimal newRent = oldRent;

            if (dto.IncreaseType == IncreaseType.Percentage && dto.IncreaseValue > 0)
                newRent = oldRent + Math.Round(oldRent * (dto.IncreaseValue / 100m), 2);
            else if (dto.IncreaseType == IncreaseType.FixedAmount && dto.IncreaseValue > 0)
                newRent = oldRent + dto.IncreaseValue;

            string newContractNumber = await _numberGen.GenerateContractNumberAsync();

            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _db.Database.BeginTransactionAsync();
                try
                {
                    var newContract = new Contract
                    {
                        ContractNumber = newContractNumber,
                        TenantId = oldContract.TenantId,
                        UnitId = oldContract.UnitId,
                        StartDate = dto.NewStartDate,
                        EndDate = dto.NewEndDate,
                        RentAmount = newRent,
                        RentCycle = oldContract.RentCycle,
                        DepositAmount = oldContract.DepositAmount,
                        Status = ContractStatus.Active,
                        ActivityType = oldContract.ActivityType,
                        TradeName = oldContract.TradeName,
                        AutoRenew = dto.AutoRenew,
                        AnnualIncreasePercentage = dto.AnnualIncreasePercentage ?? (dto.IncreaseType == IncreaseType.Percentage ? dto.IncreaseValue : oldContract.AnnualIncreasePercentage),
                        ParentContractId = oldContract.Id,
                        Notes = dto.Notes ?? $"تجديد للعقد السابق رقم {oldContract.ContractNumber} بزيادة ({dto.IncreaseValue})"
                    };

                    if (dto.CopyFeesFromPreviousContract && oldContract.ContractFees.Any())
                    {
                        foreach (var fee in oldContract.ContractFees.Where(f => f.IsActive))
                        {
                            newContract.ContractFees.Add(new ContractFee
                            {
                                FeeName = fee.FeeName,
                                ValueType = fee.ValueType,
                                Frequency = fee.Frequency,
                                Value = fee.Value,
                                Notes = fee.Notes
                            });
                        }
                    }

                    if (dto.NewFees != null && dto.NewFees.Any())
                    {
                        foreach (var feeDto in dto.NewFees)
                        {
                            newContract.ContractFees.Add(new ContractFee
                            {
                                FeeName = feeDto.FeeName,
                                ValueType = feeDto.ValueType,
                                Frequency = feeDto.Frequency,
                                Value = feeDto.Value,
                                Notes = feeDto.Notes
                            });
                        }
                    }

                    _db.Contracts.Add(newContract);

                    oldContract.Status = ContractStatus.Renewed;
                    oldContract.UpdatedAt = DateTime.UtcNow;

                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var savedContract = await _db.Contracts
                        .Include(c => c.Tenant)
                        .Include(c => c.Unit)
                        .Include(c => c.ContractItems)
                        .Include(c => c.ContractDocuments)
                        .Include(c => c.ContractFees)
                        .Include(c => c.ParentContract)
                        .FirstAsync(c => c.Id == newContract.Id);

                    _ = _notification.SendToTenantAsync(
                        savedContract.TenantId,
                        "تم تجديد عقد الإيجار بنجاح 🔄",
                        $"تم تجديد عقدكم بنجاح برقم جديد {savedContract.ContractNumber} وقيمة إيجار معدلة: {savedContract.RentAmount:N2} د.ل.",
                        NotificationType.ContractRenewed,
                        $"/tenant/contracts/{savedContract.Id}",
                        savedContract.Id
                    );

                    return MapToDto(savedContract);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task<List<ContractResponseDto>> GetByTenantAsync(int tenantId)
        {
            var contracts = await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .Include(c => c.ContractItems.Where(i => i.IsActive))
                .Include(c => c.ContractDocuments.Where(d => d.IsActive))
                .Include(c => c.ContractFees.Where(f => f.IsActive))
                .Include(c => c.ParentContract)
                .Where(c => c.TenantId == tenantId && c.IsActive)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();
            return contracts.Select(MapToDto).ToList();
        }

        public async Task<List<ContractResponseDto>> GetByUnitAsync(int unitId)
        {
            var contracts = await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .Include(c => c.ContractItems.Where(i => i.IsActive))
                .Include(c => c.ContractDocuments.Where(d => d.IsActive))
                .Include(c => c.ContractFees.Where(f => f.IsActive))
                .Include(c => c.ParentContract)
                .Where(c => c.UnitId == unitId && c.IsActive)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();
            return contracts.Select(MapToDto).ToList();
        }

        public async Task<List<ContractResponseDto>> GetExpiringAsync(int days)
        {
            var targetDate = DateTime.Today.AddDays(days);
            var contracts = await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .Include(c => c.ContractItems.Where(i => i.IsActive))
                .Include(c => c.ContractDocuments.Where(d => d.IsActive))
                .Include(c => c.ContractFees.Where(f => f.IsActive))
                .Include(c => c.ParentContract)
                .Where(c => c.IsActive && c.Status == ContractStatus.Active
                    && c.EndDate.Date >= DateTime.Today && c.EndDate.Date <= targetDate.Date)
                .OrderBy(c => c.EndDate)
                .ToListAsync();
            return contracts.Select(MapToDto).ToList();
        }

        public async Task<ContractRenewalChainDto?> GetRenewalChainAsync(int contractId)
        {
            var contract = await _db.Contracts.FirstOrDefaultAsync(c => c.Id == contractId && c.IsActive);
            if (contract == null) return null;

            var root = contract;
            while (root.ParentContractId != null)
            {
                var parent = await _db.Contracts.FirstOrDefaultAsync(c => c.Id == root.ParentContractId && c.IsActive);
                if (parent == null) break;
                root = parent;
            }

            var chain = new List<Contract>();
            var current = root;
            chain.Add(current);

            var queue = new Queue<Contract>();
            queue.Enqueue(current);
            var visited = new HashSet<int> { current.Id };

            while (queue.Count > 0)
            {
                var parent = queue.Dequeue();
                var children = await _db.Contracts
                    .Include(c => c.Tenant)
                    .Include(c => c.Unit)
                    .Include(c => c.ContractItems.Where(i => i.IsActive))
                    .Include(c => c.ContractDocuments.Where(d => d.IsActive))
                    .Include(c => c.ContractFees.Where(f => f.IsActive))
                    .Include(c => c.ParentContract)
                    .Where(c => c.ParentContractId == parent.Id && c.IsActive)
                    .ToListAsync();

                foreach (var child in children)
                {
                    if (!visited.Contains(child.Id))
                    {
                        visited.Add(child.Id);
                        chain.Add(child);
                        queue.Enqueue(child);
                    }
                }
            }

            var chainIds = chain.Select(c => c.Id).ToList();
            var fullChain = await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .Include(c => c.ContractItems.Where(i => i.IsActive))
                .Include(c => c.ContractDocuments.Where(d => d.IsActive))
                .Include(c => c.ContractFees.Where(f => f.IsActive))
                .Include(c => c.ParentContract)
                .Where(c => chainIds.Contains(c.Id))
                .OrderBy(c => c.StartDate)
                .ToListAsync();

            return new ContractRenewalChainDto
            {
                RootContractId = root.Id,
                Chain = fullChain.Select(MapToDto).ToList()
            };
        }

        public async Task<ContractFinancialSummaryDto?> GetFinancialSummaryAsync(int contractId)
        {
            var contract = await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .Include(c => c.ContractFees.Where(f => f.IsActive))
                .FirstOrDefaultAsync(c => c.Id == contractId && c.IsActive);

            if (contract == null) return null;

            var payments = await _db.Payments
                .Where(p => p.ContractId == contractId && p.IsActive)
                .ToListAsync();

            int durationMonths = Math.Max(1, (int)((contract.EndDate - contract.StartDate).TotalDays / 30));
            decimal totalContractValue = contract.RentAmount * durationMonths;

            decimal oneTimeFees = contract.ContractFees
                .Where(f => f.Frequency == FeeFrequency.OneTime)
                .Sum(f => f.CalculateActualAmount(contract.RentAmount, totalContractValue));

            decimal monthlyFees = contract.ContractFees
                .Where(f => f.Frequency == FeeFrequency.Monthly)
                .Sum(f => f.CalculateActualAmount(contract.RentAmount, totalContractValue));

            decimal totalDue = (contract.RentAmount * durationMonths) + contract.DepositAmount + oneTimeFees + (monthlyFees * durationMonths);
            decimal totalPaid = payments.Sum(p => p.Amount);

            return new ContractFinancialSummaryDto
            {
                ContractId = contract.Id,
                ContractNumber = contract.ContractNumber,
                TenantName = contract.Tenant?.FullName ?? "",
                UnitNumber = contract.Unit?.UnitNumber ?? "",
                RentAmount = contract.RentAmount,
                TotalDue = totalDue,
                TotalPaid = totalPaid,
                Remaining = totalDue - totalPaid,
                TotalFeesOneTime = oneTimeFees,
                TotalFeesMonthly = monthlyFees * durationMonths,
                DepositAmount = contract.DepositAmount,
                PaymentsCount = payments.Count,
                LastPaymentDate = payments.OrderByDescending(p => p.PaymentDate).FirstOrDefault()?.PaymentDate,
                Fees = contract.ContractFees.Select(f => new ContractFeeResponseDto
                {
                    Id = f.Id,
                    FeeName = f.FeeName,
                    ValueType = f.ValueType,
                    ValueTypeLabel = f.ValueType == FeeValueType.Fixed ? "مبلغ ثابت" : "نسبة مئوية",
                    Frequency = f.Frequency,
                    FrequencyLabel = f.Frequency == FeeFrequency.OneTime ? "مرة واحدة" : "شهرياً",
                    InputValue = f.Value,
                    CalculatedAmount = f.CalculateActualAmount(contract.RentAmount, totalContractValue),
                    Notes = f.Notes
                }).ToList()
            };
        }

        public async Task<List<DTOs.Payments.PaymentResponseDto>> GetPaymentsAsync(int contractId)
        {
            var contractExists = await _db.Contracts.AnyAsync(c => c.Id == contractId && c.IsActive);
            if (!contractExists) throw new KeyNotFoundException("العقد غير موجود");

            return await _db.Payments
                .Include(p => p.Contract).ThenInclude(c => c!.Tenant)
                .Include(p => p.Contract).ThenInclude(c => c!.Unit)
                .Where(p => p.ContractId == contractId && p.IsActive)
                .OrderByDescending(p => p.PaymentDate)
                .Select(p => new DTOs.Payments.PaymentResponseDto
                {
                    Id = p.Id,
                    ReceiptNumber = p.ReceiptNumber,
                    ContractId = p.ContractId,
                    ContractNumber = p.Contract != null ? p.Contract.ContractNumber : "",
                    TenantId = p.TenantId,
                    TenantName = p.Contract != null && p.Contract.Tenant != null ? p.Contract.Tenant.FullName : "",
                    UnitId = p.UnitId,
                    UnitNumber = p.Contract != null && p.Contract.Unit != null ? p.Contract.Unit.UnitNumber : "",
                    PaymentType = p.PaymentType.ToString(),
                    Amount = p.Amount,
                    PaymentMethod = p.PaymentMethod.ToString(),
                    ReferenceNumber = p.ReferenceNumber,
                    PaymentDate = p.PaymentDate,
                    Notes = p.Notes
                }).ToListAsync();
        }

        public async Task<ContractStatementDto?> GetStatementAsync(int contractId, DateTime? fromDate, DateTime? toDate)
        {
            var contract = await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .Include(c => c.ContractFees.Where(f => f.IsActive))
                .FirstOrDefaultAsync(c => c.Id == contractId && c.IsActive);

            if (contract == null) return null;

            fromDate ??= contract.StartDate;
            toDate ??= DateTime.Today;

            var payments = await _db.Payments
                .Include(p => p.Contract).ThenInclude(c => c!.Tenant)
                .Include(p => p.Contract).ThenInclude(c => c!.Unit)
                .Where(p => p.ContractId == contractId && p.IsActive
                    && p.PaymentDate >= fromDate && p.PaymentDate <= toDate.Value.Date.AddDays(1).AddTicks(-1))
                .OrderBy(p => p.PaymentDate)
                .ToListAsync();

            int durationMonths = Math.Max(1, (int)((contract.EndDate - contract.StartDate).TotalDays / 30));
            decimal totalContractValue = contract.RentAmount * durationMonths;

            var fees = contract.ContractFees.Select(f => new ContractFeeResponseDto
            {
                Id = f.Id,
                FeeName = f.FeeName,
                ValueType = f.ValueType,
                ValueTypeLabel = f.ValueType == FeeValueType.Fixed ? "مبلغ ثابت" : "نسبة مئوية",
                Frequency = f.Frequency,
                FrequencyLabel = f.Frequency == FeeFrequency.OneTime ? "مرة واحدة" : "شهرياً",
                InputValue = f.Value,
                CalculatedAmount = f.CalculateActualAmount(contract.RentAmount, totalContractValue),
                Notes = f.Notes
            }).ToList();

            decimal oneTimeFees = fees.Where(f => f.Frequency == FeeFrequency.OneTime).Sum(f => f.CalculatedAmount);
            decimal monthlyFeePerMonth = fees.Where(f => f.Frequency == FeeFrequency.Monthly).Sum(f => f.CalculatedAmount);

            var endForCalc = contract.EndDate < toDate ? contract.EndDate : toDate.Value;
            var startForCalc = contract.StartDate > fromDate ? contract.StartDate : fromDate.Value;
            int monthsToCalc = Math.Max(0, (int)((endForCalc - startForCalc).TotalDays / 30)) + 1;
            if (monthsToCalc < 1) monthsToCalc = 1;

            decimal totalDue = (contract.RentAmount * monthsToCalc) + (monthlyFeePerMonth * monthsToCalc) + oneTimeFees;
            if (contract.StartDate >= fromDate && contract.StartDate <= toDate) totalDue += contract.DepositAmount;

            decimal totalPaid = payments.Sum(p => p.Amount);

            var monthlyBreakdown = new List<ContractMonthlyDueDto>();
            var current = new DateTime(startForCalc.Year, startForCalc.Month, 1);
            var endMonth = new DateTime(endForCalc.Year, endForCalc.Month, 1);
            while (current <= endMonth)
            {
                var monthPayments = payments.Where(p => p.PaymentDate.Year == current.Year && p.PaymentDate.Month == current.Month).Sum(p => p.Amount);
                monthlyBreakdown.Add(new ContractMonthlyDueDto
                {
                    Year = current.Year,
                    Month = current.Month,
                    MonthName = System.Globalization.CultureInfo.GetCultureInfo("ar-LY").DateTimeFormat.GetMonthName(current.Month),
                    RentDue = contract.RentAmount,
                    FeesDue = monthlyFeePerMonth,
                    TotalDue = contract.RentAmount + monthlyFeePerMonth,
                    Paid = monthPayments,
                    Balance = (contract.RentAmount + monthlyFeePerMonth) - monthPayments,
                    DueDate = current
                });
                current = current.AddMonths(1);
            }

            return new ContractStatementDto
            {
                ContractId = contract.Id,
                ContractNumber = contract.ContractNumber,
                TenantName = contract.Tenant?.FullName ?? "",
                UnitNumber = contract.Unit?.UnitNumber ?? "",
                StartDate = contract.StartDate,
                EndDate = contract.EndDate,
                RentAmount = contract.RentAmount,
                DepositAmount = contract.DepositAmount,
                Fees = fees,
                Payments = payments.Select(p => new DTOs.Payments.PaymentResponseDto
                {
                    Id = p.Id,
                    ReceiptNumber = p.ReceiptNumber,
                    ContractId = p.ContractId,
                    ContractNumber = p.Contract?.ContractNumber ?? "",
                    TenantId = p.TenantId,
                    TenantName = p.Contract?.Tenant?.FullName ?? "",
                    UnitId = p.UnitId,
                    UnitNumber = p.Contract?.Unit?.UnitNumber ?? "",
                    PaymentType = p.PaymentType.ToString(),
                    Amount = p.Amount,
                    PaymentMethod = p.PaymentMethod.ToString(),
                    ReferenceNumber = p.ReferenceNumber,
                    PaymentDate = p.PaymentDate,
                    Notes = p.Notes
                }).ToList(),
                TotalDue = totalDue,
                TotalPaid = totalPaid,
                Remaining = totalDue - totalPaid,
                TotalFeesCalculated = oneTimeFees + (monthlyFeePerMonth * monthsToCalc),
                MonthlyBreakdown = monthlyBreakdown
            };
        }

        public async Task<ContractFeeResponseDto> AddFeeAsync(int contractId, CreateContractFeeDto dto)
        {
            var contract = await _db.Contracts
                .Include(c => c.ContractFees)
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .FirstOrDefaultAsync(c => c.Id == contractId && c.IsActive);

            if (contract == null) throw new KeyNotFoundException("العقد غير موجود");
            if (contract.Status == ContractStatus.Terminated || contract.Status == ContractStatus.Renewed)
                throw new InvalidOperationException("لا يمكن إضافة رسوم لعقد مفسوخ أو مجدد");

            var fee = new ContractFee
            {
                ContractId = contractId,
                FeeName = dto.FeeName,
                ValueType = dto.ValueType,
                Frequency = dto.Frequency,
                Value = dto.Value,
                Notes = dto.Notes
            };

            _db.ContractFees.Add(fee);
            await _db.SaveChangesAsync();

            int durationMonths = Math.Max(1, (int)((contract.EndDate - contract.StartDate).TotalDays / 30));
            decimal totalContractValue = contract.RentAmount * durationMonths;
            decimal calcAmount = fee.CalculateActualAmount(contract.RentAmount, totalContractValue);

            _ = _notification.SendToTenantAsync(
                contract.TenantId,
                $"تمت إضافة رسم جديد لعقدك: {dto.FeeName} 💰",
                $"تمت إضافة رسم '{dto.FeeName}' بقيمة {calcAmount:N2} د.ل ({(dto.Frequency == FeeFrequency.OneTime ? "مرة واحدة" : "شهري")}) لعقدك رقم {contract.ContractNumber} للمحل {contract.Unit?.UnitNumber}",
                NotificationType.PaymentReminder,
                $"/tenant/contracts/{contract.Id}",
                fee.Id
            );

            return new ContractFeeResponseDto
            {
                Id = fee.Id,
                FeeName = fee.FeeName,
                ValueType = fee.ValueType,
                ValueTypeLabel = fee.ValueType == FeeValueType.Fixed ? "مبلغ ثابت" : "نسبة مئوية",
                Frequency = fee.Frequency,
                FrequencyLabel = fee.Frequency == FeeFrequency.OneTime ? "مرة واحدة" : "شهرياً",
                InputValue = fee.Value,
                CalculatedAmount = calcAmount,
                Notes = fee.Notes
            };
        }

        public async Task<ContractFeeResponseDto> UpdateFeeAsync(int contractId, int feeId, UpdateContractFeeDto dto)
        {
            var contract = await _db.Contracts
                .Include(c => c.ContractFees)
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .FirstOrDefaultAsync(c => c.Id == contractId && c.IsActive);

            if (contract == null) throw new KeyNotFoundException("العقد غير موجود");

            var fee = contract.ContractFees.FirstOrDefault(f => f.Id == feeId && f.IsActive);
            if (fee == null) throw new KeyNotFoundException("الرسم غير موجود في هذا العقد");

            fee.FeeName = dto.FeeName;
            fee.ValueType = dto.ValueType;
            fee.Frequency = dto.Frequency;
            fee.Value = dto.Value;
            fee.Notes = dto.Notes;
            fee.UpdatedAt = DateTimeHelper.LibyaNow;

            await _db.SaveChangesAsync();

            int durationMonths = Math.Max(1, (int)((contract.EndDate - contract.StartDate).TotalDays / 30));
            decimal totalContractValue = contract.RentAmount * durationMonths;
            decimal calcAmount = fee.CalculateActualAmount(contract.RentAmount, totalContractValue);

            _ = _notification.SendToTenantAsync(
                contract.TenantId,
                $"تم تعديل رسم في عقدك: {dto.FeeName} ✏️",
                $"تم تعديل الرسم '{dto.FeeName}' إلى {calcAmount:N2} د.ل في عقدك {contract.ContractNumber}",
                NotificationType.PaymentReminder,
                $"/tenant/contracts/{contract.Id}",
                fee.Id
            );

            return new ContractFeeResponseDto
            {
                Id = fee.Id,
                FeeName = fee.FeeName,
                ValueType = fee.ValueType,
                ValueTypeLabel = fee.ValueType == FeeValueType.Fixed ? "مبلغ ثابت" : "نسبة مئوية",
                Frequency = fee.Frequency,
                FrequencyLabel = fee.Frequency == FeeFrequency.OneTime ? "مرة واحدة" : "شهرياً",
                InputValue = fee.Value,
                CalculatedAmount = calcAmount,
                Notes = fee.Notes
            };
        }

        public async Task<bool> DeleteFeeAsync(int contractId, int feeId)
        {
            var contract = await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .FirstOrDefaultAsync(c => c.Id == contractId && c.IsActive);

            var fee = await _db.ContractFees
                .FirstOrDefaultAsync(f => f.Id == feeId && f.ContractId == contractId && f.IsActive);

            if (fee == null || contract == null) return false;

            var feeName = fee.FeeName;
            fee.IsActive = false;
            fee.UpdatedAt = DateTimeHelper.LibyaNow;
            await _db.SaveChangesAsync();

            _ = _notification.SendToTenantAsync(
                contract.TenantId,
                $"تم حذف رسم من عقدك: {feeName} 🗑️",
                $"تم حذف الرسم '{feeName}' من عقدك رقم {contract.ContractNumber}",
                NotificationType.System,
                $"/tenant/contracts/{contract.Id}",
                contract.Id
            );

            return true;
        }

        public async Task<DTOs.Payments.PaymentResponseDto?> ProcessDueAsync(int contractId)
        {
            var contract = await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.ContractFees.Where(f => f.IsActive))
                .FirstOrDefaultAsync(c => c.Id == contractId && c.IsActive && c.Status == ContractStatus.Active);

            if (contract == null) throw new KeyNotFoundException("العقد غير موجود أو غير نشط");

            var tenant = contract.Tenant;
            if (tenant == null || tenant.CreditBalance <= 0)
                throw new InvalidOperationException("رصيد المستأجر غير كافٍ أو غير موجود");

            var today = DateTimeHelper.LibyaToday;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            bool alreadyProcessed = await _db.Payments.AnyAsync(p =>
                p.ContractId == contractId && p.PaymentType == PaymentType.Rent &&
                p.PaymentDate >= startOfMonth && p.PaymentDate <= endOfMonth && p.IsActive);

            if (alreadyProcessed)
                throw new InvalidOperationException("تمت معالجة إيجار هذا الشهر مسبقاً لهذا العقد");

            int durationMonths = Math.Max(1, (int)((contract.EndDate - contract.StartDate).TotalDays / 30));
            decimal totalContractValue = contract.RentAmount * durationMonths;
            decimal monthlyFeesTotal = contract.ContractFees
                .Where(f => f.Frequency == FeeFrequency.Monthly)
                .Sum(f => f.CalculateActualAmount(contract.RentAmount, totalContractValue));

            decimal totalMonthlyDue = contract.RentAmount + monthlyFeesTotal;

            if (tenant.CreditBalance < totalMonthlyDue)
                throw new InvalidOperationException($"رصيد المستأجر ({tenant.CreditBalance:N2}) أقل من المستحق ({totalMonthlyDue:N2})");

            tenant.CreditBalance -= totalMonthlyDue;
            var receiptNo = await _numberGen.GenerateReceiptNumberAsync();

            var payment = new Payment
            {
                TenantId = tenant.Id,
                ContractId = contract.Id,
                UnitId = contract.UnitId,
                Amount = totalMonthlyDue,
                PaymentDate = today,
                PaymentType = PaymentType.Rent,
                PaymentMethod = PaymentMethod.FromBalance,
                ReceiptNumber = receiptNo,
                Notes = $"خصم آلي فردي لإيجار {today:MM/yyyy} شامل رسوم شهرية {monthlyFeesTotal} د.ل - عقد {contract.ContractNumber}",
                IsActive = true
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();

            _ = _notification.SendToTenantAsync(
                tenant.Id,
                $"تم خصم إيجار {today:MM/yyyy} تلقائياً ✅",
                $"تم خصم {totalMonthlyDue:N2} د.ل من رصيدك لعقد {contract.ContractNumber} - المحل {contract.Unit?.UnitNumber}. رصيدك المتبقي: {tenant.CreditBalance:N2} د.ل",
                NotificationType.AutomaticDeduction,
                $"/tenant/payments/{payment.Id}",
                payment.Id
            );

            return new DTOs.Payments.PaymentResponseDto
            {
                Id = payment.Id,
                ReceiptNumber = payment.ReceiptNumber,
                ContractId = payment.ContractId,
                ContractNumber = contract.ContractNumber,
                TenantId = payment.TenantId,
                TenantName = tenant.FullName,
                UnitId = payment.UnitId,
                UnitNumber = contract.Unit?.UnitNumber ?? "",
                PaymentType = payment.PaymentType.ToString(),
                Amount = payment.Amount,
                PaymentMethod = payment.PaymentMethod.ToString(),
                ReferenceNumber = payment.ReferenceNumber,
                PaymentDate = payment.PaymentDate,
                Notes = payment.Notes
            };
        }

        private static ContractResponseDto MapToDto(Contract contract)
        {
            int durationMonths = Math.Max(1, (int)((contract.EndDate - contract.StartDate).TotalDays / 30));
            decimal totalContractValue = contract.RentAmount * durationMonths;

            return new ContractResponseDto
            {
                Id = contract.Id,
                ContractNumber = contract.ContractNumber,
                TenantId = contract.TenantId,
                TenantName = contract.Tenant?.FullName ?? "",
                TenantPhone = contract.Tenant?.Phone ?? "",
                UnitId = contract.UnitId,
                UnitNumber = contract.Unit?.UnitNumber ?? "",
                UnitName = contract.TradeName ?? contract.Unit?.UnitNumber ?? "",
                StartDate = contract.StartDate,
                EndDate = contract.EndDate,
                RentAmount = contract.RentAmount,
                RentCycle = contract.RentCycle.ToString(),
                DepositAmount = contract.DepositAmount,
                Status = contract.Status.ToString(),
                AutoRenew = contract.AutoRenew,
                Notes = contract.Notes,
                AnnualIncreasePercentage = contract.AnnualIncreasePercentage,
                ParentContractId = contract.ParentContractId,
                ParentContractNumber = contract.ParentContract?.ContractNumber,
                ExtraItems = contract.ContractItems?.Select(i => new ContractItemDto
                {
                    Id = i.Id,
                    ItemName = i.ItemName,
                    Amount = i.Amount,
                    Notes = i.Notes
                }).ToList() ?? new List<ContractItemDto>(),
                Documents = contract.ContractDocuments?.Select(d => new ContractDocumentDto
                {
                    Id = d.Id,
                    FileName = d.FileName,
                    FilePath = d.FilePath,
                    FileType = d.FileType
                }).ToList() ?? new List<ContractDocumentDto>(),
                ContractFees = contract.ContractFees?.Select(f => new ContractFeeResponseDto
                {
                    Id = f.Id,
                    FeeName = f.FeeName,
                    ValueType = f.ValueType,
                    ValueTypeLabel = f.ValueType == FeeValueType.Fixed ? "مبلغ ثابت" : "نسبة مئوية",
                    Frequency = f.Frequency,
                    FrequencyLabel = f.Frequency == FeeFrequency.OneTime ? "مرة واحدة" : "شهرياً",
                    InputValue = f.Value,
                    CalculatedAmount = f.CalculateActualAmount(contract.RentAmount, totalContractValue),
                    Notes = f.Notes
                }).ToList() ?? new List<ContractFeeResponseDto>()
            };
        }
    }
}
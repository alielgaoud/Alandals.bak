using Andalos.API.Data;
using Andalos.API.DTOs.Contracts;
using Andalos.API.Enums;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public class ContractService : IContractService
    {
        private readonly AppDbContext _db;
        private readonly INumberGeneratorService _numberGen;

        public ContractService(AppDbContext db, INumberGeneratorService numberGen)
        {
            _db = db;
            _numberGen = numberGen;
        }

        public async Task<List<ContractResponseDto>> GetAllAsync()
        {
            return await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .Include(c => c.ContractItems)
                .Include(c => c.ContractDocuments)
                .Include(c => c.ContractFees)   // 👈 إصلاح: تم إضافة جلب الرسوم
                .Include(c => c.ParentContract) // 👈 إصلاح: تم إضافة جلب العقد الأب
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => MapToDto(c))
                .ToListAsync();
        }

        public async Task<ContractResponseDto?> GetByIdAsync(int id)
        {
            var contract = await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .Include(c => c.ContractItems)
                .Include(c => c.ContractDocuments)
                .Include(c => c.ContractFees)   // 👈 إصلاح: تم إضافة جلب الرسوم
                .Include(c => c.ParentContract) // 👈 إصلاح: تم إضافة جلب العقد الأب
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

            string contractNumber = await _numberGen.GenerateAsync("Contract");

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var contract = new Contract
                {
                    ContractNumber = contractNumber,
                    TenantId = dto.TenantId,
                    UnitId = dto.UnitId,
                    StartDate = dto.StartDate,
                    EndDate = dto.EndDate,
                    RentAmount = dto.RentAmount,
                    RentCycle = dto.RentCycle,
                    DepositAmount = dto.DepositAmount,
                    Status = ContractStatus.Active,
                    AutoRenew = dto.AutoRenew,
                    AnnualIncreasePercentage = dto.AnnualIncreasePercentage, // 👈 إصلاح: تم إضافتها للإنشاء
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

                return MapToDto(savedContract);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> UpdateStatusAsync(int id, ContractStatus newStatus)
        {
            var contract = await _db.Contracts
                .Include(c => c.Unit)
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

            if (contract == null) return false;

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                contract.Status = newStatus;
                contract.UpdatedAt = DateTime.UtcNow;

                if (contract.Unit != null && (newStatus == ContractStatus.Expired || newStatus == ContractStatus.Terminated))
                {
                    contract.Unit.Status = UnitStatus.Vacant;
                    contract.Unit.UpdatedAt = DateTime.UtcNow;
                }
                else if (contract.Unit != null && newStatus == ContractStatus.Active)
                {
                    contract.Unit.Status = UnitStatus.Rented;
                    contract.Unit.UpdatedAt = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var contract = await _db.Contracts
                .Include(c => c.Unit)
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

            if (contract == null) return false;

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                contract.IsActive = false;
                contract.UpdatedAt = DateTime.UtcNow;

                // 👈 إصلاح منطقي مهم: لا تفرغ المحل إلا إذا كان العقد المحذوف نشطاً!
                if (contract.Unit != null && contract.Status == ContractStatus.Active)
                {
                    contract.Unit.Status = UnitStatus.Vacant;
                    contract.Unit.UpdatedAt = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                return false;
            }
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

            string newContractNumber = await _numberGen.GenerateAsync("Contract");

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

                return MapToDto(savedContract);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ===== دالة التحويل من Model لـ DTO =====
        private ContractResponseDto MapToDto(Contract contract)
        {
            int durationMonths = Math.Max(1, (int)((contract.EndDate - contract.StartDate).TotalDays / 30));
            decimal totalContractValue = contract.RentAmount * durationMonths;

            return new ContractResponseDto
            {
                Id = contract.Id,
                ContractNumber = contract.ContractNumber,
                TenantId = contract.TenantId,
                TenantName = contract.Tenant?.FullName ?? "",
                UnitId = contract.UnitId,
                UnitNumber = contract.Unit?.UnitNumber ?? "",
                UnitName = contract.Unit?.UnitName ?? "",
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

                // 👈 إصلاح: تم إضافة تحويل المستندات والبنود الإضافية
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
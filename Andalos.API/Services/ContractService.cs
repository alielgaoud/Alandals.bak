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
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

            return contract == null ? null : MapToDto(contract);
        }

        public async Task<ContractResponseDto> CreateAsync(CreateContractDto dto)
        {
            // 1. التحقق من وجود المستأجر
            var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == dto.TenantId && t.IsActive);
            if (!tenantExists)
                throw new KeyNotFoundException("المستأجر المحدد غير موجود");

            // 2. التحقق من وجود المحل وحالته (يجب أن يكون شاغراً للتعاقد)
            var unit = await _db.Units.FirstOrDefaultAsync(u => u.Id == dto.UnitId && u.IsActive);
            if (unit == null)
                throw new KeyNotFoundException("المحل المحدد غير موجود");

            if (unit.Status != UnitStatus.Vacant)
                throw new InvalidOperationException($"المحل المحدد غير شاغر حالياً (حالة المحل الحالية: {unit.Status})");

            // 3. توليد رقم عقد تسلسلي تلقائي فريد
            string contractNumber = await _numberGen.GenerateAsync("Contract");

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // 4. إنشاء كيان العقد
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
                    Status = ContractStatus.Active, // تفعيله تلقائياً عند الإنشاء
                    AutoRenew = dto.AutoRenew,
                    Notes = dto.Notes
                };

                // أ) إضافة البنود الإضافية إن وُجدت
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

                // ب) 👈 الجديد: إضافة العمولات والرسوم الإضافية إن وُجدت
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

                // 5. تعديل حالة المحل تلقائياً إلى (مؤجر)
                unit.Status = UnitStatus.Rented;
                unit.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                // 6. استرجاع الكيان كاملاً مع كافة العلاقات بما فيها الرسوم والعمولات الجديدة
                var savedContract = await _db.Contracts
                    .Include(c => c.Tenant)
                    .Include(c => c.Unit)
                    .Include(c => c.ContractItems)
                    .Include(c => c.ContractDocuments)
                    .Include(c => c.ContractFees) // 👈 جديد: تضمين جدول الرسوم
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

                // إذا انتهى العقد أو فُسخ، نقوم بإرجاع حالة المحل إلى (شاغر)
                if (contract.Unit != null && (newStatus == ContractStatus.Expired || newStatus == ContractStatus.Terminated))
                {
                    contract.Unit.Status = UnitStatus.Vacant;
                    contract.Unit.UpdatedAt = DateTime.UtcNow;
                }
                // إذا أُعيد تفعيل العقد، نرجع حالة المحل إلى (مؤجر)
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
                // Soft Delete للعقد
                contract.IsActive = false;
                contract.UpdatedAt = DateTime.UtcNow;

                // تحرير المحل ليكون شاغراً مجدداً
                if (contract.Unit != null)
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

                // 👈 جديد: تحويل قائمة العمولات مع حساب قيمتها بالدينار تلقائياً
                ContractFees = contract.ContractFees.Select(f => new ContractFeeResponseDto
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
    }
}
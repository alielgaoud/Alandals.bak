using Andalos.API.Data;
using Andalos.API.DTOs.Payments;
using Andalos.API.Enums;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _db;

        public PaymentService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<PaymentResponseDto>> GetAllAsync()
        {
            return await _db.Payments
                .Include(p => p.Contract)
                    .ThenInclude(c => c!.Tenant)
                .Include(p => p.Contract)
                    .ThenInclude(c => c!.Unit)
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.PaymentDate)
                .Select(p => MapToDto(p))
                .ToListAsync();
        }

        public async Task<List<PaymentResponseDto>> GetByContractAsync(int contractId)
        {
            return await _db.Payments
                .Include(p => p.Contract)
                    .ThenInclude(c => c!.Tenant)
                .Include(p => p.Contract)
                    .ThenInclude(c => c!.Unit)
                .Where(p => p.ContractId == contractId && p.IsActive)
                .OrderByDescending(p => p.PaymentDate)
                .Select(p => MapToDto(p))
                .ToListAsync();
        }

        public async Task<List<PaymentResponseDto>> GetByTenantAsync(int tenantId)
        {
            return await _db.Payments
                .Include(p => p.Contract)
                    .ThenInclude(c => c!.Tenant)
                .Include(p => p.Contract)
                    .ThenInclude(c => c!.Unit)
                .Where(p => p.TenantId == tenantId && p.IsActive)
                .OrderByDescending(p => p.PaymentDate)
                .Select(p => MapToDto(p))
                .ToListAsync();
        }

        public async Task<PaymentResponseDto> CreateAsync(CreatePaymentDto dto)
        {
            // 1. التحقق من وجود العقد
            var contract = await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .FirstOrDefaultAsync(c => c.Id == dto.ContractId && c.IsActive);

            if (contract == null)
                throw new KeyNotFoundException("العقد غير موجود");

            // 2. توليد رقم إيصال تلقائي
            string receiptNumber = await GenerateReceiptNumberAsync();

            // 3. إنشاء الدفعة
            var payment = new Payment
            {
                ReceiptNumber = receiptNumber,
                ContractId = dto.ContractId,
                TenantId = contract.TenantId,   // نسخ مباشرة للتقارير
                UnitId = contract.UnitId,        // نسخ مباشرة للتقارير
                PaymentType = dto.PaymentType,
                Amount = dto.Amount,
                PaymentMethod = dto.PaymentMethod,
                ReferenceNumber = dto.ReferenceNumber,
                PaymentDate = dto.PaymentDate,
                Notes = dto.Notes
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();

            return MapToDto(payment);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            if (payment == null) return false;

            payment.IsActive = false;
            payment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return true;
        }

        // ===== تقرير ملخص عقد واحد =====
        public async Task<PaymentSummaryDto> GetContractSummaryAsync(int contractId)
        {
            var contract = await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .FirstOrDefaultAsync(c => c.Id == contractId && c.IsActive);

            if (contract == null)
                throw new KeyNotFoundException("العقد غير موجود");

            // حساب المستحق: عدد الأشهر × الإيجار
            int months = (int)((contract.EndDate - contract.StartDate).TotalDays / 30);
            if (months < 1) months = 1;
            decimal totalDue = months * contract.RentAmount;

            // حساب المدفوع
            decimal totalPaid = await _db.Payments
                .Where(p => p.ContractId == contractId && p.IsActive)
                .SumAsync(p => p.Amount);

            return new PaymentSummaryDto
            {
                ContractId = contract.Id,
                ContractNumber = contract.ContractNumber,
                TenantName = contract.Tenant?.FullName ?? "",
                UnitNumber = contract.Unit?.UnitNumber ?? "",
                TotalDue = totalDue,
                TotalPaid = totalPaid,
                Remaining = totalDue - totalPaid
            };
        }

        // ===== تقرير ملخص كل العقود =====
        public async Task<List<PaymentSummaryDto>> GetAllSummariesAsync()
        {
            var contracts = await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .Where(c => c.IsActive && c.Status == ContractStatus.Active)
                .ToListAsync();

            var summaries = new List<PaymentSummaryDto>();

            foreach (var contract in contracts)
            {
                int months = (int)((contract.EndDate - contract.StartDate).TotalDays / 30);
                if (months < 1) months = 1;
                decimal totalDue = months * contract.RentAmount;

                decimal totalPaid = await _db.Payments
                    .Where(p => p.ContractId == contract.Id && p.IsActive)
                    .SumAsync(p => p.Amount);

                summaries.Add(new PaymentSummaryDto
                {
                    ContractId = contract.Id,
                    ContractNumber = contract.ContractNumber,
                    TenantName = contract.Tenant?.FullName ?? "",
                    UnitNumber = contract.Unit?.UnitNumber ?? "",
                    TotalDue = totalDue,
                    TotalPaid = totalPaid,
                    Remaining = totalDue - totalPaid
                });
            }

            return summaries;
        }

        // ===== توليد رقم إيصال تلقائي =====
        private async Task<string> GenerateReceiptNumberAsync()
        {
            int year = DateTime.Now.Year;
            int count = await _db.Payments.CountAsync(p => p.PaymentDate.Year == year);
            return $"REC-{year}-{(count + 1):D5}"; // مثال: REC-2026-00001
        }

        // ===== دالة التحويل =====
        private static PaymentResponseDto MapToDto(Payment p)
        {
            return new PaymentResponseDto
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
            };
        }
    }
}
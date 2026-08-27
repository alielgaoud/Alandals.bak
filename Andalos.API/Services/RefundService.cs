using Andalos.API.Data;
using Andalos.API.DTOs.Refunds;
using Andalos.API.Enums;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public class RefundService : IRefundService
    {
        private readonly AppDbContext _db;
        private readonly INumberGeneratorService _numberGen;

        public RefundService(AppDbContext db, INumberGeneratorService numberGen)
        {
            _db = db;
            _numberGen = numberGen;
        }

        public async Task<List<RefundResponseDto>> GetAllAsync()
        {
            return await _db.Refunds
                .Include(r => r.Contract).ThenInclude(c => c!.Tenant)
                .Include(r => r.Contract).ThenInclude(c => c!.Unit)
                .Include(r => r.OriginalPayment)
                .Where(r => r.IsActive)
                .OrderByDescending(r => r.RefundDate)
                .Select(r => MapToDto(r))
                .ToListAsync();
        }

        public async Task<List<RefundResponseDto>> GetByContractAsync(int contractId)
        {
            return await _db.Refunds
                .Include(r => r.Contract).ThenInclude(c => c!.Tenant)
                .Include(r => r.Contract).ThenInclude(c => c!.Unit)
                .Include(r => r.OriginalPayment)
                .Where(r => r.ContractId == contractId && r.IsActive)
                .OrderByDescending(r => r.RefundDate)
                .Select(r => MapToDto(r))
                .ToListAsync();
        }

        public async Task<RefundResponseDto> CreateAsync(CreateRefundDto dto)
        {
            var contract = await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .FirstOrDefaultAsync(c => c.Id == dto.ContractId && c.IsActive);

            if (contract == null)
                throw new KeyNotFoundException("العقد المحدد غير موجود");

            // في حال تم ربطها بدفعة سابقة، نتحقق من أن قيمة المرتجع لا تتجاوز القيمة الأصلية
            if (dto.OriginalPaymentId.HasValue)
            {
                var originalPayment = await _db.Payments
                    .FirstOrDefaultAsync(p => p.Id == dto.OriginalPaymentId.Value && p.IsActive);

                if (originalPayment == null)
                    throw new KeyNotFoundException("سند القبض الأصلي غير موجود");

                if (dto.Amount > originalPayment.Amount)
                    throw new InvalidOperationException("لا يمكن إرجاع مبلغ أكبر من قيمة السند الأصلي");
            }

            // توليد رقم المرتجع التلقائي (مثال: RFD-2026-00001)
            string refundNumber = await _numberGen.GenerateAsync("Refund");

            var refund = new Refund
            {
                RefundNumber = refundNumber,
                ContractId = dto.ContractId,
                TenantId = contract.TenantId,
                UnitId = contract.UnitId,
                OriginalPaymentId = dto.OriginalPaymentId,
                RefundType = dto.RefundType,
                Amount = dto.Amount,
                RefundMethod = dto.RefundMethod,
                RefundDate = dto.RefundDate,
                Reason = dto.Reason,
                Notes = dto.Notes
            };

            _db.Refunds.Add(refund);
            await _db.SaveChangesAsync();

            var saved = await _db.Refunds
                .Include(r => r.Contract).ThenInclude(c => c!.Tenant)
                .Include(r => r.Contract).ThenInclude(c => c!.Unit)
                .Include(r => r.OriginalPayment)
                .FirstAsync(r => r.Id == refund.Id);

            return MapToDto(saved);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var refund = await _db.Refunds.FirstOrDefaultAsync(r => r.Id == id && r.IsActive);
            if (refund == null) return false;

            refund.IsActive = false; // إلغاء المرتجع منطقياً
            refund.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        private static RefundResponseDto MapToDto(Refund r)
        {
            return new RefundResponseDto
            {
                Id = r.Id,
                RefundNumber = r.RefundNumber,
                ContractId = r.ContractId,
                ContractNumber = r.Contract?.ContractNumber ?? "",
                TenantId = r.TenantId,
                TenantName = r.Contract?.Tenant?.FullName ?? "",
                UnitId = r.UnitId,
                UnitNumber = r.Contract?.Unit?.UnitNumber ?? "",
                OriginalReceiptNumber = r.OriginalPayment?.ReceiptNumber,
                RefundType = r.RefundType.ToString(),
                Amount = r.Amount,
                RefundMethod = r.RefundMethod.ToString(),
                RefundDate = r.RefundDate,
                Reason = r.Reason,
                Notes = r.Notes
            };
        }
    }
}
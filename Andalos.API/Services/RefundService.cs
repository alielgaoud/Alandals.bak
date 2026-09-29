using Andalos.API.Security;
using Andalos.API.Data;
using Andalos.API.DTOs.Refunds;
using Andalos.API.Enums;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public class RefundService : IRefundService
    {
        private readonly AppDbContext _db;
        private readonly INumberGeneratorService _numberGen;
        private readonly INotificationService _notification; // 👈 تم الحقن

        public RefundService(
            AppDbContext db,
            INumberGeneratorService numberGen,
            INotificationService notification) // 👈 تم الحقن
        {
            _db = db;
            _numberGen = numberGen;
            _notification = notification;
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

        public Task<RefundResponseDto> CreateAsync(CreateRefundDto dto) => _db.AtomicAsync(() => CreateAsyncCore(dto));

        private async Task<RefundResponseDto> CreateAsyncCore(CreateRefundDto dto)
        {
            FinancialOperationGuard.ValidateAmount(dto.Amount);
            if (!Enum.IsDefined(dto.RefundType) || !Enum.IsDefined(dto.RefundMethod) || dto.RefundMethod == PaymentMethod.FromBalance)
                throw new ArgumentException("Invalid refund type/method.");

            var contract = await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .FirstOrDefaultAsync(c => c.Id == dto.ContractId && c.IsActive);

            if (contract == null)
                throw new KeyNotFoundException("العقد المحدد غير موجود");

            if (dto.OriginalPaymentId.HasValue)
            {
                var originalPayment = await _db.Payments
                    .FirstOrDefaultAsync(p => p.Id == dto.OriginalPaymentId.Value && p.IsActive);

                if (originalPayment == null)
                    throw new KeyNotFoundException("سند القبض الأصلي غير موجود");

                if (originalPayment.ContractId != dto.ContractId || originalPayment.TenantId != contract.TenantId) throw new ForbiddenOperationException();
                if (originalPayment.PaymentMethod == PaymentMethod.FromBalance || originalPayment.PaymentType == PaymentType.AdvancePayment ||
                    originalPayment.WalletCreditAmount > 0 || originalPayment.AllocatedChargeAmount > 0 ||
                    !originalPayment.AllocationRecorded)
                    throw new ConcurrencyConflictException("Allocated payments require a reviewed accounting reversal, not a standalone refund.");
                var previous = await _db.Refunds.Where(r => r.IsActive && r.OriginalPaymentId == originalPayment.Id).SumAsync(r => r.Amount);
                if (dto.Amount + previous > originalPayment.Amount) throw new ArgumentException("Cumulative refunds exceed the original payment.");
            }


            var eligible = _db.Payments.Where(p => p.ContractId == dto.ContractId && p.IsActive && p.PaymentMethod != PaymentMethod.FromBalance &&
                p.PaymentType != PaymentType.AdvancePayment && p.WalletCreditAmount == 0 && p.AllocatedChargeAmount == 0 && p.AllocationRecorded);
            var paid = await eligible.SumAsync(p => p.Amount);
            var previousRefunds = await _db.Refunds.Where(r => r.ContractId == dto.ContractId && r.IsActive).SumAsync(r => r.Amount);
            if (previousRefunds + dto.Amount > paid) throw new ArgumentException("Refund exceeds eligible collected funds.");
            if (dto.RefundType == RefundType.DepositReturn)
            {
                var depositPaid = await eligible.Where(p => p.PaymentType == PaymentType.Deposit).SumAsync(p => p.Amount);
                var returned = await _db.Refunds.Where(r => r.IsActive && r.ContractId == dto.ContractId && r.RefundType == RefundType.DepositReturn).SumAsync(r => r.Amount);
                if (returned + dto.Amount > depositPaid) throw new ArgumentException("Deposit refund exceeds collected deposits.");
            }
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

            // 🔔 إشعار المستأجر بإصدار مرتجع مالي لصالحه
            await _notification.SendToTenantAsync(
                contract.TenantId,
                "تم إصدار مرتجع مالي لصالحك 💰",
                $"تم إصدار سند مرتجع رقم {refundNumber} بقيمة {dto.Amount:N2} د.ل. السبب: {dto.Reason}.",
                NotificationType.PaymentReceived,
                "/portal/payments",
                refund.Id
            );

            var saved = await _db.Refunds
                .Include(r => r.Contract).ThenInclude(c => c!.Tenant)
                .Include(r => r.Contract).ThenInclude(c => c!.Unit)
                .Include(r => r.OriginalPayment)
                .FirstAsync(r => r.Id == refund.Id);

            return MapToDto(saved);
        }

        public Task<bool> DeleteAsync(int id) => _db.AtomicAsync(() => DeleteAsyncCore(id));

        private async Task<bool> DeleteAsyncCore(int id)
        {
            var refund = await _db.Refunds.FirstOrDefaultAsync(r => r.Id == id && r.IsActive);
            if (refund == null) return false;

            refund.IsActive = false;
            refund.UpdatedAt = DateTimeHelper.LibyaNow;
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
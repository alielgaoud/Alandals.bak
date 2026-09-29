using Andalos.API.Security;
using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.DTOs.Payments;
using Andalos.API.Enums;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _db;
        private readonly FinancialOperationGuard _guard;
        private readonly INotificationService _notification; // 👈 حقن الإشعارات
        private readonly INumberGeneratorService _numberGen; // 👈 جديد: مولّد الأرقام الموحد (يمنع تكرار الإيصالات)

        public PaymentService(
            AppDbContext db,
            INotificationService notification,
            INumberGeneratorService numberGen, FinancialOperationGuard guard)
        {
            _db = db;
            _guard = guard;
            _notification = notification;
            _numberGen = numberGen;
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

        public Task<PaymentResponseDto> CreateAsync(CreatePaymentDto dto) => _db.AtomicAsync(() => CreateAsyncCore(dto));

        private async Task<PaymentResponseDto> CreateAsyncCore(CreatePaymentDto dto)
        {
            FinancialOperationGuard.ValidateAmount(dto.Amount);
            if (!Enum.IsDefined(dto.PaymentType) || !Enum.IsDefined(dto.PaymentMethod) || !Enum.IsDefined(dto.AllocationMode) || dto.PaymentMethod == PaymentMethod.FromBalance)
                throw new ArgumentException("Invalid payment type/method/allocation; FromBalance is server-only.");
            if (dto.AllocationMode == PaymentAllocationMode.OnAccount) await _guard.RequireAsync(Permissions.Financials.DepositAdvance);
            if (dto.AllocationMode == PaymentAllocationMode.SettleCharge) await _guard.RequireAsync(Permissions.Financials.SettleCharge);
            if (dto.PaymentType == PaymentType.AdvancePayment && dto.AllocationMode != PaymentAllocationMode.OnAccount)
                throw new ArgumentException("Advance payments must use OnAccount allocation.");

            var contract = await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .FirstOrDefaultAsync(c => c.Id == dto.ContractId && c.IsActive);

            if (contract == null)
                throw new KeyNotFoundException("العقد غير موجود");

            string receiptNumber = await _numberGen.GenerateAsync("receipt"); // 👈 المولّد الموحد (يتحقق من التكرار)

            var tenant = contract.Tenant;
            string? allocationNote = null;
            decimal overflowToBalance = 0;
            decimal allocatedAmount = 0;

            // ============================================================
            // 👈 جديد: توجيه الدفعة حسب النمط المختار
            // ============================================================
            if (dto.AllocationMode == PaymentAllocationMode.SettleCharge)
            {
                // ===== تسوية الدفعة بتحميل معين (جزئياً أو كلياً) =====
                if (dto.ChargeId == null)
                    throw new InvalidOperationException("تحديد رقم التحميل (chargeId) مطلوب عند وضع التسوية");

                var targetCharge = await _db.TenantCharges
                    .FirstOrDefaultAsync(ch => ch.Id == dto.ChargeId.Value && ch.IsActive);

                if (targetCharge == null)
                    throw new KeyNotFoundException("التحميل المحدد غير موجود");

                if (targetCharge.TenantId != contract.TenantId)
                    throw new UnauthorizedAccessException("هذا التحميل لا يخص مستأجر هذا العقد");

                if (targetCharge.ChargeStatus != ChargeStatus.Approved)
                    throw new InvalidOperationException("يمكن التسوية فقط للتحميلات المؤكدة (المعلقة تنتظر موافقة المستأجر، والمرفوضة والمسددة لا تُقبل)");

                decimal chargeRemaining = targetCharge.Amount - targetCharge.SettledAmount;
                if (chargeRemaining <= 0)
                    throw new InvalidOperationException("هذا التحميل مسدد بالكامل");

                // 👈 التسوية الجزئية: نسدد ما تكفيه الدفعة والفائض يبقى رصيداً بحسابه
                decimal settledPart = Math.Min(dto.Amount, chargeRemaining);
                overflowToBalance = dto.Amount - settledPart;
                allocatedAmount = settledPart;
                if (overflowToBalance > 0)
                {
                    await _guard.RequireAsync(Permissions.Financials.DepositAdvance);
                    if (tenant is null) throw new InvalidOperationException("Tenant missing.");
                    tenant.CreditBalance += overflowToBalance;
                }

                targetCharge.SettledAmount += settledPart;
                if (targetCharge.SettledAmount >= targetCharge.Amount)
                {
                    targetCharge.IsSettled = true;
                    targetCharge.ChargeStatus = ChargeStatus.Paid;
                    targetCharge.SettlementReceiptNumber = receiptNumber;
                }
                targetCharge.UpdatedAt = DateTimeHelper.LibyaNow;

                allocationNote = $"تسوية تحميل رقم ({targetCharge.ChargeNumber}) بمبلغ {settledPart:N2} د.ل";
                if (overflowToBalance > 0)
                    allocationNote += $" — والفائض {overflowToBalance:N2} د.ل أُودع رصيداً بحسابه";
            }
            else if (dto.AllocationMode == PaymentAllocationMode.OnAccount)
            {
                // ===== دفعة على الحساب: تُودع رصيداً وتُخصم لاحقاً تلقائياً =====
                if (tenant == null)
                    throw new InvalidOperationException("لا يوجد مستأجر مرتبط بهذا العقد");

                tenant.CreditBalance += dto.Amount;
                allocationNote = $"إيداع دفعة على الحساب — الرصيد بعد الإيداع: {tenant.CreditBalance:N2} د.ل";
            }

            var payment = new Payment
            {
                ReceiptNumber = receiptNumber,
                AllocationRecorded = true,
                WalletCreditAmount = dto.AllocationMode == PaymentAllocationMode.OnAccount ? dto.Amount : overflowToBalance,
                AllocatedChargeId = dto.AllocationMode == PaymentAllocationMode.SettleCharge ? dto.ChargeId : null,
                AllocatedChargeAmount = allocatedAmount,
                ContractId = dto.ContractId,
                TenantId = contract.TenantId,
                UnitId = contract.UnitId,
                // 👈 على الحساب = توحيداً مع آلية الرصيد الحالية (تُعامل كدفعة مقدمة)
                PaymentType = dto.AllocationMode == PaymentAllocationMode.OnAccount
                    ? PaymentType.AdvancePayment
                    : dto.PaymentType,
                Amount = dto.Amount,
                PaymentMethod = dto.PaymentMethod,
                ReferenceNumber = dto.ReferenceNumber,
                PaymentDate = dto.PaymentDate,
                Notes = CombineNotes(allocationNote, dto.Notes)
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();

            // 🔔 إشعار المستأجر فور تسجيل السداد في النظام
            await _notification.SendToTenantAsync(
                contract.TenantId,
                "استلام دفعة مالية ناجح 🧾",
                $"تم استلام مبلغ {dto.Amount:N2} د.ل بنجاح بموجب الإيصال رقم {receiptNumber}.",
                NotificationType.PaymentReceived,
                $"/tenant/payments/{payment.Id}",
                payment.Id
            );

            // 🔔 إشعار المحاسبين وإدارة المجمع بوجود دفعة جديدة داخل اللوحة
            await _notification.SendToGroupAsync(
                "Accountants",
                "دفعة مالية جديدة 💰",
                $"قام المستأجر {contract.Tenant?.FullName} بسداد مبلغ {dto.Amount:N2} د.ل للمحل {contract.Unit?.UnitNumber}.",
                NotificationType.PaymentReceived,
                $"/admin/payments/{payment.Id}"
            );

            return MapToDto(payment);
        }

        public Task<bool> DeleteAsync(int id) => _db.AtomicAsync(() => DeleteAsyncCore(id));

        private async Task<bool> DeleteAsyncCore(int id)
        {
            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            if (payment == null) return false;


            if (await _db.Refunds.AnyAsync(r => r.IsActive && r.OriginalPaymentId == id)) throw new ConcurrencyConflictException("Payment has active refunds.");
            if (!payment.AllocationRecorded)
                throw new ConcurrencyConflictException("Legacy allocated payment requires a reviewed accounting reversal.");
            var tenant = await _db.Tenants.SingleAsync(t => t.Id == payment.TenantId);
            if (tenant.CreditBalance < payment.WalletCreditAmount) throw new ConcurrencyConflictException("Credited funds have already been consumed.");
            if (payment.AllocatedChargeId.HasValue)
            {
                var charge = await _db.TenantCharges.SingleAsync(c => c.Id == payment.AllocatedChargeId);
                if (charge.SettledAmount < payment.AllocatedChargeAmount) throw new ConcurrencyConflictException();
                charge.SettledAmount -= payment.AllocatedChargeAmount; charge.IsSettled = false; charge.ChargeStatus = ChargeStatus.Approved;
                if (charge.SettlementReceiptNumber == payment.ReceiptNumber) charge.SettlementReceiptNumber = null;
            }
            tenant.CreditBalance = tenant.CreditBalance - payment.WalletCreditAmount + payment.WalletDebitAmount;
            var eligiblePaid = await _db.Payments.Where(p => p.ContractId == payment.ContractId && p.Id != id && p.IsActive &&
                p.PaymentMethod != PaymentMethod.FromBalance && p.WalletCreditAmount == 0 && p.AllocatedChargeAmount == 0 && p.AllocationRecorded && p.PaymentType != PaymentType.AdvancePayment).SumAsync(p => p.Amount);
            var refunded = await _db.Refunds.Where(r => r.ContractId == payment.ContractId && r.IsActive).SumAsync(r => r.Amount);
            if (eligiblePaid < refunded) throw new ConcurrencyConflictException("Payment cancellation would leave an unfunded refund.");
            payment.IsActive = false;
            payment.UpdatedAt = DateTimeHelper.LibyaNow;
            await _db.SaveChangesAsync();

            return true;
        }

        public async Task<PaymentSummaryDto> GetContractSummaryAsync(int contractId)
        {
            var contract = await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .FirstOrDefaultAsync(c => c.Id == contractId && c.IsActive);

            if (contract == null)
                throw new KeyNotFoundException("العقد غير موجود");

            int months = (int)((contract.EndDate - contract.StartDate).TotalDays / 30);
            if (months < 1) months = 1;
            decimal totalDue = months * contract.RentAmount;

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

        // 👈 دمج ملاحظة التوجيه مع ملاحظات المحاسب
        private static string? CombineNotes(string? allocationNote, string? userNotes)
        {
            if (allocationNote == null) return userNotes;
            if (string.IsNullOrWhiteSpace(userNotes)) return allocationNote;
            return $"{allocationNote} — {userNotes}";
        }

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
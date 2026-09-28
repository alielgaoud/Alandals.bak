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
        private readonly INotificationService _notification; // 👈 حقن الإشعارات
        private readonly INumberGeneratorService _numberGen; // 👈 جديد: مولّد الأرقام الموحد (يمنع تكرار الإيصالات)

        public PaymentService(
            AppDbContext db,
            INotificationService notification,
            INumberGeneratorService numberGen)
        {
            _db = db;
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

        public async Task<PaymentResponseDto> CreateAsync(CreatePaymentDto dto)
        {
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

        public async Task<bool> DeleteAsync(int id)
        {
            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            if (payment == null) return false;

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
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

        public PaymentService(AppDbContext db, INotificationService notification)
        {
            _db = db;
            _notification = notification;
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

            string receiptNumber = await GenerateReceiptNumberAsync();

            var payment = new Payment
            {
                ReceiptNumber = receiptNumber,
                ContractId = dto.ContractId,
                TenantId = contract.TenantId,
                UnitId = contract.UnitId,
                PaymentType = dto.PaymentType,
                Amount = dto.Amount,
                PaymentMethod = dto.PaymentMethod,
                ReferenceNumber = dto.ReferenceNumber,
                PaymentDate = dto.PaymentDate,
                Notes = dto.Notes
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();

            // 🔔 إشعار المستأجر فور تسجيل السداد في النظام
            _ = _notification.SendToTenantAsync(
                contract.TenantId,
                "استلام دفعة مالية ناجح 🧾",
                $"تم استلام مبلغ {dto.Amount:N2} د.ل بنجاح بموجب الإيصال رقم {receiptNumber}.",
                NotificationType.PaymentReceived,
                $"/tenant/payments/{payment.Id}",
                payment.Id
            );

            // 🔔 إشعار المحاسبين وإدارة المجمع بوجود دفعة جديدة داخل اللوحة
            _ = _notification.SendToGroupAsync(
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

        private async Task<string> GenerateReceiptNumberAsync()
        {
            int year = DateTime.Now.Year;
            int count = await _db.Payments.CountAsync(p => p.PaymentDate.Year == year);
            return $"REC-{year}-{(count + 1):D5}";
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
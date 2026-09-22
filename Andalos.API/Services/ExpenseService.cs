using Andalos.API.Data;
using Andalos.API.DTOs.Expenses;
using Andalos.API.Enums;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public class ExpenseService : IExpenseService
    {
        private readonly AppDbContext _db;
        private readonly INumberGeneratorService _numberGen;
        private readonly IWebHostEnvironment _env;
        private readonly INotificationService _notification; // 👈 تم الحقن

        public ExpenseService(
            AppDbContext db,
            INumberGeneratorService numberGen,
            IWebHostEnvironment env,
            INotificationService notification) // 👈 تم الحقن
        {
            _db = db;
            _numberGen = numberGen;
            _env = env;
            _notification = notification;
        }

        public async Task<List<ExpenseResponseDto>> GetAllAsync()
        {
            return await _db.Expenses
                .Include(e => e.Unit)
                .Include(e => e.Tenant)
                .Where(e => e.IsActive)
                .OrderByDescending(e => e.ExpenseDate)
                .Select(e => MapToDto(e))
                .ToListAsync();
        }

        public async Task<List<ExpenseResponseDto>> GetByUnitAsync(int unitId)
        {
            return await _db.Expenses
                .Include(e => e.Unit)
                .Include(e => e.Tenant)
                .Where(e => e.UnitId == unitId && e.IsActive)
                .OrderByDescending(e => e.ExpenseDate)
                .Select(e => MapToDto(e))
                .ToListAsync();
        }

        public async Task<List<ExpenseResponseDto>> GetByTenantAsync(int tenantId)
        {
            return await _db.Expenses
                .Include(e => e.Unit)
                .Include(e => e.Tenant)
                .Where(e => e.TenantId == tenantId && e.IsChargedToTenant && e.IsActive)
                .OrderByDescending(e => e.ExpenseDate)
                .Select(e => MapToDto(e))
                .ToListAsync();
        }

        public async Task<ExpenseResponseDto> CreateAsync(CreateExpenseDto dto)
        {
            if (dto.UnitId.HasValue)
            {
                var unitExists = await _db.Units.AnyAsync(u => u.Id == dto.UnitId.Value && u.IsActive);
                if (!unitExists)
                    throw new KeyNotFoundException("المحل المحدد غير موجود");
            }

            Tenant? tenant = null;
            if (dto.TenantId.HasValue)
            {
                tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == dto.TenantId.Value && t.IsActive);
                if (tenant == null)
                    throw new KeyNotFoundException("المستأجر المحدد غير موجود");
            }

            string expenseNumber = await _numberGen.GenerateAsync("Expense");

            string? attachmentPath = null;
            if (dto.Attachment != null && dto.Attachment.Length > 0)
            {
                attachmentPath = await SaveFileAsync(dto.Attachment);
            }

            var expense = new Expense
            {
                ExpenseNumber = expenseNumber,
                UnitId = dto.UnitId,
                TenantId = dto.TenantId,
                IsChargedToTenant = dto.IsChargedToTenant,
                ExpenseType = dto.ExpenseType,
                Amount = dto.Amount,
                ExpenseDate = dto.ExpenseDate,
                PaidTo = dto.PaidTo,
                Description = dto.Description,
                InvoiceNumber = dto.InvoiceNumber,
                AttachmentUrl = attachmentPath
            };

            _db.Expenses.Add(expense);

            if (dto.IsChargedToTenant && tenant != null && tenant.CreditBalance > 0)
            {
                decimal amountToDeduct = Math.Min(tenant.CreditBalance, dto.Amount);
                tenant.CreditBalance -= amountToDeduct;

                string receiptNo = await _numberGen.GenerateAsync("Receipt");

                var activeContract = await _db.Contracts
                    .FirstOrDefaultAsync(c => c.TenantId == tenant.Id && c.Status == ContractStatus.Active && c.IsActive);

                if (activeContract != null)
                {
                    var settlementPayment = new Payment
                    {
                        TenantId = tenant.Id,
                        ContractId = activeContract.Id,
                        Amount = amountToDeduct,
                        PaymentDate = dto.ExpenseDate,
                        PaymentType = PaymentType.Maintenance,
                        PaymentMethod = PaymentMethod.FromBalance,
                        ReceiptNumber = receiptNo,
                        Notes = $"خصم تلقائي لمصروف محمّل برقم ({expenseNumber}): {dto.Description}",
                        IsActive = true
                    };

                    _db.Payments.Add(settlementPayment);
                }
            }

            await _db.SaveChangesAsync();

            // 🔔 إشعار المستأجر إذا تم تحميل مصروف على حسابه
            if (dto.IsChargedToTenant && dto.TenantId.HasValue)
            {
                _ = _notification.SendToTenantAsync(
                    dto.TenantId.Value,
                    "مصروف جديد محمّل على حسابك 📋",
                    $"تم تحميل مصروف بقيمة {dto.Amount:N2} د.ل على حسابكم. الوصف: {dto.Description}.",
                    NotificationType.AutomaticDeduction,
                    "/portal/payments",
                    expense.Id
                );
            }

            var saved = await _db.Expenses
                .Include(e => e.Unit)
                .Include(e => e.Tenant)
                .FirstAsync(e => e.Id == expense.Id);

            return MapToDto(saved);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.IsActive);
            if (expense == null) return false;

            expense.IsActive = false;
            expense.UpdatedAt = DateTimeHelper.LibyaNow;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<decimal> GetTotalExpensesAsync(DateTime? fromDate, DateTime? toDate)
        {
            var query = _db.Expenses.Where(e => e.IsActive);

            if (fromDate.HasValue)
                query = query.Where(e => e.ExpenseDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(e => e.ExpenseDate <= toDate.Value);

            return await query.SumAsync(e => e.Amount);
        }

        private async Task<string> SaveFileAsync(IFormFile file)
        {
            string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string uploadsFolder = Path.Combine(webRoot, "uploads", "expenses");

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            string fileExtension = Path.GetExtension(file.FileName);
            string uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return $"/uploads/expenses/{uniqueFileName}";
        }

        private static ExpenseResponseDto MapToDto(Expense e)
        {
            return new ExpenseResponseDto
            {
                Id = e.Id,
                ExpenseNumber = e.ExpenseNumber,
                UnitId = e.UnitId,
                UnitNumber = e.Unit?.UnitNumber,
                UnitName = e.Unit?.UnitNumber,
                TenantId = e.TenantId,
                TenantName = e.Tenant?.FullName,
                IsChargedToTenant = e.IsChargedToTenant,
                ExpenseType = e.ExpenseType.ToString(),
                Amount = e.Amount,
                ExpenseDate = e.ExpenseDate,
                PaidTo = e.PaidTo,
                Description = e.Description,
                InvoiceNumber = e.InvoiceNumber,
                AttachmentUrl = e.AttachmentUrl
            };
        }
    }
}
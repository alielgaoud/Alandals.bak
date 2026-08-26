using Andalos.API.Data;
using Andalos.API.DTOs.Expenses;
using Andalos.API.Enums;
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

        public ExpenseService(AppDbContext db, INumberGeneratorService numberGen, IWebHostEnvironment env)
        {
            _db = db;
            _numberGen = numberGen;
            _env = env;
        }

        public async Task<List<ExpenseResponseDto>> GetAllAsync()
        {
            return await _db.Expenses
                .Include(e => e.Unit)
                .Include(e => e.Tenant) // 👈 تضمين المستأجر
                .Where(e => e.IsActive)
                .OrderByDescending(e => e.ExpenseDate)
                .Select(e => MapToDto(e))
                .ToListAsync();
        }

        public async Task<List<ExpenseResponseDto>> GetByUnitAsync(int unitId)
        {
            return await _db.Expenses
                .Include(e => e.Unit)
                .Include(e => e.Tenant) // 👈 تضمين المستأجر
                .Where(e => e.UnitId == unitId && e.IsActive)
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

            // التحقق من وجود المستأجر في حال تم تحديده
            if (dto.TenantId.HasValue)
            {
                var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == dto.TenantId.Value && t.IsActive);
                if (!tenantExists)
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
                TenantId = dto.TenantId, // 👈 حفظ المستأجر
                IsChargedToTenant = dto.IsChargedToTenant, // 👈 حفظ حالة التحميل المالي
                ExpenseType = dto.ExpenseType,
                Amount = dto.Amount,
                ExpenseDate = dto.ExpenseDate,
                PaidTo = dto.PaidTo,
                Description = dto.Description,
                InvoiceNumber = dto.InvoiceNumber,
                AttachmentUrl = attachmentPath
            };

            _db.Expenses.Add(expense);
            await _db.SaveChangesAsync();

            var saved = await _db.Expenses
                .Include(e => e.Unit)
                .Include(e => e.Tenant) // 👈 تضمين المستأجر
                .FirstAsync(e => e.Id == expense.Id);

            return MapToDto(saved);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.IsActive);
            if (expense == null) return false;

            expense.IsActive = false;
            expense.UpdatedAt = DateTime.UtcNow;
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
                UnitName = e.Unit?.UnitName,
                TenantId = e.TenantId, // 👈 إرجاع البيانات للـ Frontend
                TenantName = e.Tenant?.FullName, // 👈 اسم المستأجر
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
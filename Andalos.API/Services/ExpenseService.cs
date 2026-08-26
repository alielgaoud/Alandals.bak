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

        public ExpenseService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<ExpenseResponseDto>> GetAllAsync()
        {
            return await _db.Expenses
                .Include(e => e.Unit)
                .Where(e => e.IsActive)
                .OrderByDescending(e => e.ExpenseDate)
                .Select(e => MapToDto(e))
                .ToListAsync();
        }

        public async Task<List<ExpenseResponseDto>> GetByUnitAsync(int unitId)
        {
            return await _db.Expenses
                .Include(e => e.Unit)
                .Where(e => e.UnitId == unitId && e.IsActive)
                .OrderByDescending(e => e.ExpenseDate)
                .Select(e => MapToDto(e))
                .ToListAsync();
        }

        public async Task<ExpenseResponseDto> CreateAsync(CreateExpenseDto dto)
        {
            int? detectedTenantId = null;

            if (dto.UnitId.HasValue)
            {
                var unitExists = await _db.Units.AnyAsync(u => u.Id == dto.UnitId.Value && u.IsActive);
                if (!unitExists)
                    throw new KeyNotFoundException("المحل المحدد غير موجود");

                // 👈 الجديد: إذا كان المصروف محملاً على المستأجر، نجلب العقد النشط حالياً للمحل
                if (dto.IsChargedToTenant)
                {
                    var activeContract = await _db.Contracts
                        .FirstOrDefaultAsync(c => c.UnitId == dto.UnitId.Value
                                             && c.Status == ContractStatus.Active
                                             && c.IsActive);

                    if (activeContract == null)
                        throw new InvalidOperationException("لا يمكن تحميل المصروف على المستأجر لعدم وجود عقد إيجار نشط حالياً لهذا المحل.");

                    detectedTenantId = activeContract.TenantId;
                }
            }
            else if (dto.IsChargedToTenant)
            {
                throw new InvalidOperationException("لا يمكن تحميل المصروف على مستأجر دون تحديد المحل المرتبط به.");
            }

            string expenseNumber = await GenerateExpenseNumberAsync();

            var expense = new Expense
            {
                ExpenseNumber = expenseNumber,
                UnitId = dto.UnitId,
                ExpenseType = dto.ExpenseType,
                Amount = dto.Amount,
                ExpenseDate = dto.ExpenseDate,
                PaidTo = dto.PaidTo,
                Description = dto.Description,
                InvoiceNumber = dto.InvoiceNumber,
                IsChargedToTenant = dto.IsChargedToTenant, // 👈 الجديد
                TenantId = detectedTenantId               // 👈 الجديد
            };

            _db.Expenses.Add(expense);
            await _db.SaveChangesAsync();

            var saved = await _db.Expenses
                .Include(e => e.Unit)
                .Include(e => e.Tenant) // 👈 الجديد
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

        private async Task<string> GenerateExpenseNumberAsync()
        {
            int year = DateTime.Now.Year;
            int count = await _db.Expenses.CountAsync(e => e.ExpenseDate.Year == year);
            return $"EXP-{year}-{(count + 1):D5}";
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
                ExpenseType = e.ExpenseType.ToString(),
                Amount = e.Amount,
                ExpenseDate = e.ExpenseDate,
                PaidTo = e.PaidTo,
                Description = e.Description,
                InvoiceNumber = e.InvoiceNumber,
                IsChargedToTenant = e.IsChargedToTenant, // 👈 الجديد
                TenantId = e.TenantId,                   // 👈 الجديد
                TenantName = e.Tenant?.FullName          // 👈 الجديد
            };
        }

    }
}
using Andalos.API.DTOs.Expenses;

namespace Andalos.API.Interfaces
{
    public interface IExpenseService
    {
        Task<List<ExpenseResponseDto>> GetAllAsync();
        Task<List<ExpenseResponseDto>> GetByUnitAsync(int unitId);
        Task<ExpenseResponseDto> CreateAsync(CreateExpenseDto dto);
        Task<bool> DeleteAsync(int id);
        Task<decimal> GetTotalExpensesAsync(DateTime? fromDate, DateTime? toDate);
    }
}
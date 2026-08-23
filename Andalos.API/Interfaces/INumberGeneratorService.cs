namespace Andalos.API.Interfaces
{
    public interface INumberGeneratorService
    {
        Task<string> GenerateAsync(string sequenceKey);
        // sequenceKey: "Contract", "Receipt", "Maintenance", "Expense", "PassCode"
    }
}
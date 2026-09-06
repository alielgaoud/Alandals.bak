namespace Andalos.API.Interfaces
{
    public interface INumberGeneratorService
    {
        // 👈 الدالة التي تعتمد عليها الخدمات القديمة عندك
        Task<string> GenerateAsync(string sequenceKey);

        // الدوال الخاصة لكل نوع
        Task<string> GenerateContractNumberAsync();
        Task<string> GenerateReceiptNumberAsync();
        Task<string> GenerateMaintenanceNumberAsync();
        Task<string> GenerateExpenseNumberAsync();
        Task<string> GeneratePassCodeAsync();
        Task<string> GenerateRefundNumberAsync();

        // الدالة المرنة للترقيم المخصص
        Task<string> GenerateNumberAsync(string sequenceKey, string formatSettingKey, string prefixSettingKey);
    }
}
namespace Andalos.API.Interfaces
{
    public interface INumberGeneratorService
    {
        Task<string> GenerateAsync(string sequenceKey);

        Task<string> GenerateContractNumberAsync();
        Task<string> GenerateReceiptNumberAsync();
        Task<string> GenerateMaintenanceNumberAsync();
        Task<string> GenerateExpenseNumberAsync();
        Task<string> GeneratePassCodeAsync();
        Task<string> GenerateRefundNumberAsync();

        Task<string> GenerateNumberAsync(string sequenceKey, string formatSettingKey, string prefixSettingKey);

        // معاينة الرقم القادم بدون زيادة العداد
        Task<string> PreviewNextNumberAsync(string sequenceKey, string formatSettingKey, string prefixSettingKey);
    }
}
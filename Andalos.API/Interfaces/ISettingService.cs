using Andalos.API.DTOs.Settings;

namespace Andalos.API.Interfaces
{
    public interface ISettingService
    {
        Task<string?> GetValueAsync(string key);
        Task<T> GetValueAsync<T>(string key, T defaultValue = default!);
        Task SetValueAsync(string key, string value, string updatedBy);
        Task<List<SettingGroupDto>> GetGroupedSettingsAsync();
        Task<Dictionary<string, string?>> GetGroupAsync(string group);
        Task ResetToDefaultAsync(string key);

        // جديد - إنشاء وتكامل كامل
        Task<SettingResponseDto> CreateSettingAsync(CreateSettingDto dto, string createdBy);
        Task<bool> DeleteSettingAsync(string key);
        Task<CompanyInfoDto> GetCompanyInfoAsync();
        Task<Dictionary<string, string?>> GetAllSettingsDictionaryAsync();
        Task<List<SettingResponseDto>> GetAllSettingsAsync();
        Task<bool> SettingExistsAsync(string key);
    }

    public class CreateSettingDto
    {
        public string SettingKey { get; set; } = string.Empty;
        public string? SettingValue { get; set; }
        public string SettingGroup { get; set; } = "Custom";
        public string? SettingSubGroup { get; set; }
        public string DataType { get; set; } = "String";
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? DefaultValue { get; set; }
        public bool IsRequired { get; set; } = false;
        public int SortOrder { get; set; } = 999;
    }

    public class CompanyInfoDto
    {
        public string Name { get; set; } = "الأندلس للاستثمار السياحي";
        public string ShortName { get; set; } = "الأندلس";
        public string Phone { get; set; } = "0925288883";
        public string Email { get; set; } = "info@andalos.ly";
        public string Address { get; set; } = "ليبيا";
        public string TaxNumber { get; set; } = "";
        public string LogoPath { get; set; } = "/uploads/logos/logo.png";
        public string LogoUrl { get; set; } = "";
        public string FaviconUrl { get; set; } = "";
        public string StampUrl { get; set; } = "";
        public string Currency { get; set; } = "LYD";
        public string CurrencySymbol { get; set; } = "د.ل";
    }
}
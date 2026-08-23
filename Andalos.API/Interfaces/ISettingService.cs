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
    }
}
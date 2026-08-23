using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.DTOs.Settings;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Andalos.API.Services
{
    public class SettingService : ISettingService
    {
        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;
        private const string CacheKey = "AllSettings";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

        public SettingService(AppDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<string?> GetValueAsync(string key)
        {
            var settings = await GetCachedSettingsAsync();
            return settings.TryGetValue(key, out var value) ? value : null;
        }

        public async Task<T> GetValueAsync<T>(string key, T defaultValue = default!)
        {
            var value = await GetValueAsync(key);
            if (value == null) return defaultValue;

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        public async Task SetValueAsync(string key, string value, string updatedBy)
        {
            var setting = await _db.Settings.FirstOrDefaultAsync(s => s.SettingKey == key);
            if (setting == null) return;

            setting.SettingValue = value;
            setting.UpdatedAt = DateTime.UtcNow;
            setting.UpdatedBy = updatedBy;

            await _db.SaveChangesAsync();
            _cache.Remove(CacheKey); // مسح الكاش لتحميل القيم الجديدة
        }

        public async Task<List<SettingGroupDto>> GetGroupedSettingsAsync()
        {
            var settings = await _db.Settings
                .Where(s => s.IsActive)
                .OrderBy(s => s.SettingGroup)
                .ThenBy(s => s.SortOrder)
                .ToListAsync();

            var groupDisplayNames = new Dictionary<string, string>
            {
                { "Company", "بيانات الشركة" },
                { "Financial", "الإعدادات المالية" },
                { "Rent", "إعدادات الإيجارات" },
                { "Numbering", "الترقيم التسلسلي" },
                { "Contract", "إعدادات العقود" },
                { "Unit", "إعدادات المحلات" },
                { "Visitor", "إعدادات الزوار" },
                { "System", "إعدادات النظام" }
            };

            return settings
                .GroupBy(s => s.SettingGroup)
                .Select(g => new SettingGroupDto
                {
                    GroupName = g.Key,
                    GroupDisplayName = groupDisplayNames.ContainsKey(g.Key) ? groupDisplayNames[g.Key] : g.Key,
                    Settings = g.Select(s => new SettingResponseDto
                    {
                        SettingKey = s.SettingKey,
                        SettingValue = s.SettingValue,
                        SettingGroup = s.SettingGroup,
                        DataType = s.DataType,
                        DisplayName = s.DisplayName,
                        Description = s.Description,
                        DefaultValue = s.DefaultValue,
                        IsRequired = s.IsRequired,
                        SortOrder = s.SortOrder
                    }).ToList()
                })
                .ToList();
        }

        public async Task<Dictionary<string, string?>> GetGroupAsync(string group)
        {
            return await _db.Settings
                .Where(s => s.SettingGroup == group && s.IsActive)
                .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);
        }

        public async Task ResetToDefaultAsync(string key)
        {
            var setting = await _db.Settings.FirstOrDefaultAsync(s => s.SettingKey == key);
            if (setting == null) return;

            setting.SettingValue = setting.DefaultValue;
            setting.UpdatedAt = DateTime.UtcNow;
            setting.UpdatedBy = "System";

            await _db.SaveChangesAsync();
            _cache.Remove(CacheKey);
        }

        private async Task<Dictionary<string, string?>> GetCachedSettingsAsync()
        {
            return await _cache.GetOrCreateAsync(CacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                return await _db.Settings
                    .Where(s => s.IsActive)
                    .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);
            }) ?? new();
        }
    }
}
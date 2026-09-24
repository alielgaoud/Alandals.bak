using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.DTOs.Settings;
using Andalos.API.Helpers;
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
            if (string.IsNullOrWhiteSpace(value)) return defaultValue;

            try
            {
                // معالجة خاصة للـ Boolean
                if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
                {
                    if (bool.TryParse(value, out var boolVal))
                        return (T)(object)boolVal;
                    // دعم 1/0 و yes/no
                    if (value == "1" || value.Equals("yes", StringComparison.OrdinalIgnoreCase) || value.Equals("true", StringComparison.OrdinalIgnoreCase))
                        return (T)(object)true;
                    if (value == "0" || value.Equals("no", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase))
                        return (T)(object)false;
                    return defaultValue;
                }

                // معالجة int
                if (typeof(T) == typeof(int) || typeof(T) == typeof(int?))
                {
                    if (int.TryParse(value, out var intVal))
                        return (T)(object)intVal;
                    return defaultValue;
                }

                // معالجة decimal
                if (typeof(T) == typeof(decimal) || typeof(T) == typeof(decimal?))
                {
                    if (decimal.TryParse(value, out var decVal))
                        return (T)(object)decVal;
                    return defaultValue;
                }

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
            if (setting == null)
            {
                // إنشاء تلقائي إذا غير موجود (Upsert)
                setting = new Setting
                {
                    SettingKey = key,
                    SettingValue = value,
                    SettingGroup = key.Contains('.') ? key.Split('.')[0] : "Custom",
                    DataType = "String",
                    DisplayName = key,
                    Description = "تم إنشاؤه تلقائياً",
                    DefaultValue = value,
                    IsRequired = false,
                    SortOrder = 999,
                    IsActive = true,
                    UpdatedBy = updatedBy,
                    UpdatedAt = DateTimeHelper.LibyaNow,
                    CreatedAt = DateTimeHelper.LibyaNow
                };
                _db.Settings.Add(setting);
            }
            else
            {
                setting.SettingValue = value;
                setting.UpdatedAt = DateTimeHelper.LibyaNow;
                setting.UpdatedBy = updatedBy;
            }

            await _db.SaveChangesAsync();
            _cache.Remove(CacheKey);
        }

        public async Task<SettingResponseDto> CreateSettingAsync(CreateSettingDto dto, string createdBy)
        {
            var exists = await _db.Settings.AnyAsync(s => s.SettingKey == dto.SettingKey);
            if (exists)
                throw new InvalidOperationException($"الإعداد {dto.SettingKey} موجود مسبقاً، استخدم التعديل");

            var setting = new Setting
            {
                SettingKey = dto.SettingKey,
                SettingValue = dto.SettingValue ?? dto.DefaultValue,
                SettingGroup = dto.SettingGroup,
                SettingSubGroup = dto.SettingSubGroup,
                DataType = dto.DataType,
                DisplayName = dto.DisplayName,
                Description = dto.Description,
                DefaultValue = dto.DefaultValue,
                IsRequired = dto.IsRequired,
                SortOrder = dto.SortOrder,
                IsActive = true,
                CreatedAt = DateTimeHelper.LibyaNow,
                UpdatedAt = DateTimeHelper.LibyaNow,
                UpdatedBy = createdBy
            };

            _db.Settings.Add(setting);
            await _db.SaveChangesAsync();
            _cache.Remove(CacheKey);

            return new SettingResponseDto
            {
                SettingKey = setting.SettingKey,
                SettingValue = setting.SettingValue,
                SettingGroup = setting.SettingGroup,
                DataType = setting.DataType,
                DisplayName = setting.DisplayName,
                Description = setting.Description,
                DefaultValue = setting.DefaultValue,
                IsRequired = setting.IsRequired,
                SortOrder = setting.SortOrder
            };
        }

        public async Task<bool> DeleteSettingAsync(string key)
        {
            var setting = await _db.Settings.FirstOrDefaultAsync(s => s.SettingKey == key);
            if (setting == null) return false;

            // لا نسمح بحذف الإعدادات المطلوبة
            if (setting.IsRequired)
                throw new InvalidOperationException("لا يمكن حذف إعداد مطلوب");

            setting.IsActive = false;
            setting.UpdatedAt = DateTimeHelper.LibyaNow;
            await _db.SaveChangesAsync();
            _cache.Remove(CacheKey);
            return true;
        }

        public async Task<bool> SettingExistsAsync(string key)
        {
            return await _db.Settings.AnyAsync(s => s.SettingKey == key && s.IsActive);
        }

        public async Task<List<SettingResponseDto>> GetAllSettingsAsync()
        {
            var settings = await _db.Settings.Where(s => s.IsActive).OrderBy(s => s.SettingGroup).ThenBy(s => s.SortOrder).ToListAsync();
            return settings.Select(s => new SettingResponseDto
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
            }).ToList();
        }

        public async Task<Dictionary<string, string?>> GetAllSettingsDictionaryAsync()
        {
            return await GetCachedSettingsAsync();
        }

        public async Task<CompanyInfoDto> GetCompanyInfoAsync()
        {
            var dict = await GetCachedSettingsAsync();

            string Get(string key, string fallback) => dict.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v! : fallback;

            return new CompanyInfoDto
            {
                Name = Get(SettingKeys.CompanyName, "الأندلس للاستثمار السياحي"),
                ShortName = Get(SettingKeys.CompanyShortName, "الأندلس"),
                Phone = Get(SettingKeys.CompanyPhone, "0925288883"),
                Email = Get(SettingKeys.CompanyEmail, "info@andalos.ly"),
                Address = Get(SettingKeys.CompanyAddress, "ليبيا"),
                TaxNumber = Get(SettingKeys.CompanyTaxNumber, ""),
                LogoPath = Get(SettingKeys.CompanyLogoPath, "/uploads/logos/logo.png"),
                LogoUrl = Get(SettingKeys.CompanyLogoUrl, ""),
                FaviconUrl = Get(SettingKeys.CompanyFaviconUrl, ""),
                StampUrl = Get(SettingKeys.CompanyStampUrl, ""),
                Currency = Get(SettingKeys.Currency, "LYD"),
                CurrencySymbol = Get(SettingKeys.CurrencySymbol, "د.ل")
            };
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
                { "Company", "بيانات الشركة والشعار" },
                { "Financial", "الإعدادات المالية" },
                { "Rent", "إعدادات الإيجارات" },
                { "Numbering", "الترقيم التسلسلي" },
                { "Contract", "إعدادات العقود" },
                { "ContractTemplate", "قالب ومحتوى العقد" },
                { "Unit", "إعدادات المحلات" },
                { "Visitor", "إعدادات الزوار" },
                { "System", "إعدادات النظام والروابط" },
                { "Notifications", "إعدادات الإشعارات" },
                { "Pdf", "إعدادات ملفات PDF" },
                { "Custom", "إعدادات مخصصة" }
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
            setting.UpdatedAt = DateTimeHelper.LibyaNow;
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
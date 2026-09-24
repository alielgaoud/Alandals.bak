using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Andalos.API.Services
{
    public class NumberGeneratorService : INumberGeneratorService
    {
        private readonly AppDbContext _db;
        private readonly ISettingService _settingService;
        private readonly ILogger<NumberGeneratorService> _logger;

        // خريطة البادئات القديمة الافتراضية لكل نوع - للتوافق مع القواعد القديمة
        private static readonly Dictionary<string, string> OldDefaultPrefixes = new()
        {
            { "Contract", "CTR" },
            { "Receipt", "REC" },
            { "Maintenance", "MNT" },
            { "Expense", "EXP" },
            { "PassCode", "PASS" },
            { "Refund", "RFD" },
            { "Payment", "REC" },
            { "Pass", "PASS" }
        };

        public NumberGeneratorService(AppDbContext db, ISettingService settingService, ILogger<NumberGeneratorService> logger)
        {
            _db = db;
            _settingService = settingService;
            _logger = logger;
        }

        public Task<string> GenerateAsync(string sequenceKey)
        {
            return sequenceKey.ToLower() switch
            {
                "contract" => GenerateContractNumberAsync(),
                "receipt" or "payment" => GenerateReceiptNumberAsync(),
                "maintenance" => GenerateMaintenanceNumberAsync(),
                "expense" => GenerateExpenseNumberAsync(),
                "passcode" or "pass" => GeneratePassCodeAsync(),
                "refund" => GenerateRefundNumberAsync(),
                _ => GenerateNumberAsync(sequenceKey, $"Numbering.{sequenceKey}Format", $"Numbering.{sequenceKey}Prefix")
            };
        }

        public Task<string> GenerateContractNumberAsync()
            => GenerateNumberAsync("Contract", SettingKeys.ContractNumberFormat, SettingKeys.ContractNumberPrefix);

        public Task<string> GenerateReceiptNumberAsync()
            => GenerateNumberAsync("Receipt", SettingKeys.ReceiptNumberFormat, SettingKeys.ReceiptNumberPrefix);

        public Task<string> GenerateMaintenanceNumberAsync()
            => GenerateNumberAsync("Maintenance", SettingKeys.MaintenanceNumberFormat, SettingKeys.MaintenanceNumberPrefix);

        public Task<string> GenerateExpenseNumberAsync()
            => GenerateNumberAsync("Expense", SettingKeys.ExpenseNumberFormat, SettingKeys.ExpenseNumberPrefix);

        public Task<string> GeneratePassCodeAsync()
            => GenerateNumberAsync("PassCode", SettingKeys.PassCodeFormat, SettingKeys.PassCodePrefix);

        public Task<string> GenerateRefundNumberAsync()
            => GenerateNumberAsync("Refund", SettingKeys.RefundNumberFormat, SettingKeys.RefundNumberPrefix);

        /// <summary>
        /// معاينة الرقم القادم بدون زيادة العداد - مفيد لاختبار الإعدادات
        /// </summary>
        public async Task<string> PreviewNextNumberAsync(string sequenceKey, string formatSettingKey, string prefixSettingKey)
        {
            var format = await _settingService.GetValueAsync(formatSettingKey);
            if (string.IsNullOrWhiteSpace(format))
            {
                var setting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.SettingKey == formatSettingKey);
                format = setting?.SettingValue ?? setting?.DefaultValue ?? $"{sequenceKey.ToUpper()}-{{YYYY}}-{{SEQ:5}}";
            }

            var prefix = await _settingService.GetValueAsync(prefixSettingKey);
            if (string.IsNullOrWhiteSpace(prefix))
            {
                var setting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.SettingKey == prefixSettingKey);
                prefix = setting?.SettingValue ?? setting?.DefaultValue ?? sequenceKey.ToUpper();
            }

            int currentYear = DateTime.Now.Year;
            var sequence = await _db.NumberSequences.AsNoTracking()
                .FirstOrDefaultAsync(s => s.SequenceKey == sequenceKey);

            int nextNumber = (sequence?.CurrentYear == currentYear ? sequence.LastNumber : 0) + 1;

            return BuildNumber(format, prefix, nextNumber, currentYear, sequenceKey);
        }

        public async Task<string> GenerateNumberAsync(string sequenceKey, string formatSettingKey, string prefixSettingKey)
        {
            // قراءة من الإعدادات المتكاملة عبر ISettingService (مع Cache)
            var format = await _settingService.GetValueAsync(formatSettingKey);
            if (string.IsNullOrWhiteSpace(format))
            {
                var setting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.SettingKey == formatSettingKey);
                format = setting?.SettingValue ?? setting?.DefaultValue ?? $"{sequenceKey.ToUpper()}-{{YYYY}}-{{SEQ:5}}";
            }

            var prefix = await _settingService.GetValueAsync(prefixSettingKey);
            if (string.IsNullOrWhiteSpace(prefix))
            {
                var setting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.SettingKey == prefixSettingKey);
                prefix = setting?.SettingValue ?? setting?.DefaultValue ?? sequenceKey.ToUpper();
            }

            format = format.Trim();
            prefix = prefix.Trim();

            _logger.LogInformation("Generating {SeqKey} with Format='{Format}' Prefix='{Prefix}'", sequenceKey, format, prefix);

            int currentYear = DateTime.Now.Year;

            var sequence = await _db.NumberSequences
                .FirstOrDefaultAsync(s => s.SequenceKey == sequenceKey);

            if (sequence == null)
            {
                sequence = new NumberSequence
                {
                    SequenceKey = sequenceKey,
                    LastNumber = 0,
                    CurrentYear = currentYear,
                    LastYear = currentYear,
                    UpdatedAt = DateTimeHelper.LibyaNow
                };
                _db.NumberSequences.Add(sequence);
            }

            if (sequence.CurrentYear != currentYear)
            {
                sequence.LastYear = sequence.CurrentYear;
                sequence.CurrentYear = currentYear;
                sequence.LastNumber = 0;
            }

            string generatedNumber;
            bool exists;
            int attempts = 0;

            do
            {
                sequence.LastNumber += 1;
                sequence.UpdatedAt = DateTimeHelper.LibyaNow;
                generatedNumber = BuildNumber(format, prefix, sequence.LastNumber, currentYear, sequenceKey);

                exists = await CheckIfNumberExistsAsync(sequenceKey, generatedNumber);
                attempts++;

                if (attempts > 100)
                {
                    _logger.LogWarning("Too many attempts generating number for {SeqKey}, last tried {Num}", sequenceKey, generatedNumber);
                    break;
                }
            }
            while (exists);

            await _db.SaveChangesAsync();

            _logger.LogInformation("Generated {SeqKey} Number: {Number}", sequenceKey, generatedNumber);

            return generatedNumber;
        }

        private async Task<bool> CheckIfNumberExistsAsync(string sequenceKey, string number)
        {
            return sequenceKey.ToLower() switch
            {
                "receipt" or "payment" => await _db.Payments.AnyAsync(p => p.ReceiptNumber == number),
                "contract" => await _db.Contracts.AnyAsync(c => c.ContractNumber == number),
                "expense" => await _db.Expenses.AnyAsync(e => e.ExpenseNumber == number),
                "maintenance" => await _db.MaintenanceRequests.AnyAsync(m => m.RequestNumber == number),
                "refund" => await _db.Refunds.AnyAsync(r => r.RefundNumber == number),
                "passcode" or "pass" => await _db.VisitorPasses.AnyAsync(v => v.PassCode == number),
                _ => false
            };
        }

        private static string BuildNumber(string format, string prefix, int sequenceNumber, int year, string? sequenceKey = null)
        {
            if (string.IsNullOrWhiteSpace(format))
                format = $"{prefix}-{{YYYY}}-{{SEQ:5}}";

            string result = format;

            // استبدال غير حساس لحالة الأحرف لكل الرموز الأساسية
            result = Regex.Replace(result, @"\{YYYY\}", year.ToString(), RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"\{YY\}", year.ToString().Substring(2, 2), RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"\{MM\}", DateTime.Now.ToString("MM"), RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"\{DD\}", DateTime.Now.ToString("dd"), RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"\{PREFIX\}", prefix, RegexOptions.IgnoreCase);

            // دعم {DATE} و {DATE:yyyyMMdd} و {DATE:yyyy-MM-dd} إلخ
            result = Regex.Replace(result, @"\{DATE(?::([^}]+))?\}", match =>
            {
                var fmt = match.Groups[1].Success ? match.Groups[1].Value : "yyyyMMdd";
                try { return DateTimeHelper.LibyaNow.ToString(fmt); }
                catch { return DateTimeHelper.LibyaNow.ToString("yyyyMMdd"); }
            }, RegexOptions.IgnoreCase);

            // دعم {RND} {RND:6} {RANDOM} {RANDOM:8} {HEX} {HEX:6} للعشوائية (مفيد لـ PassCode)
            result = Regex.Replace(result, @"\{(?:RND|RANDOM|HEX)(?::(\d+))?\}", match =>
            {
                int len = 6;
                if (match.Groups[1].Success && int.TryParse(match.Groups[1].Value, out var parsed))
                    len = Math.Clamp(parsed, 2, 32);

                // توليد Hex عشوائي
                int byteCount = (len + 1) / 2;
                var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(byteCount);
                var hex = Convert.ToHexString(bytes);
                if (hex.Length > len) hex = hex.Substring(0, len);
                return hex;
            }, RegexOptions.IgnoreCase);

            // معالجة {SEQ} و {SEQ:4} و {SEQ:5} إلخ
            var seqRegex = new Regex(@"\{SEQ(?::(\d+))?\}", RegexOptions.IgnoreCase);
            var seqMatches = seqRegex.Matches(result);
            if (seqMatches.Count > 0)
            {
                // نستخدم أول تطابق لتحديد الـ pad، لكن نستبدل كل التطابقات
                int pad = 5;
                var first = seqMatches[0];
                if (first.Groups[1].Success && int.TryParse(first.Groups[1].Value, out var parsedPad))
                    pad = parsedPad;

                // استبدال كل {SEQ} بنفس القيمة مع مراعاة الـ pad لكل واحد
                result = seqRegex.Replace(result, m =>
                {
                    int p = pad;
                    if (m.Groups[1].Success && int.TryParse(m.Groups[1].Value, out var pp))
                        p = pp;
                    return sequenceNumber.ToString().PadLeft(p, '0');
                });
            }
            else
            {
                // إذا لم يوجد رمز SEQ ولا RND/HEX ولا DATE، نضيف التسلسل في النهاية
                bool hasRandomOrDate = Regex.IsMatch(format, @"\{(?:RND|RANDOM|HEX|DATE)", RegexOptions.IgnoreCase);
                if (!hasRandomOrDate && !result.Contains(sequenceNumber.ToString()))
                {
                    result = $"{result.TrimEnd('-', ' ', '/')}-{sequenceNumber:D5}";
                }
            }

            // إذا كانت الصيغة الأصلية لا تحتوي {PREFIX} ولكن تحتوي بادئة قديمة ثابتة
            if (!format.Contains("{PREFIX}", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(sequenceKey))
            {
                if (OldDefaultPrefixes.TryGetValue(sequenceKey, out var oldPrefix))
                {
                    if (result.StartsWith(oldPrefix + "-", StringComparison.OrdinalIgnoreCase) &&
                        !oldPrefix.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        result = prefix + result.Substring(oldPrefix.Length);
                    }
                    else if (result.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase) && result.Length > oldPrefix.Length)
                    {
                        var after = result.Substring(oldPrefix.Length);
                        if (after.Length > 0 && (char.IsDigit(after[0]) || after[0] == '-' || after[0] == '/'))
                        {
                            if (!result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                result = prefix + after;
                        }
                    }
                }
            }

            return result.Trim();
        }

        // Overload للتوافق مع الكود القديم بدون sequenceKey
        private static string BuildNumber(string format, string prefix, int sequenceNumber, int year)
        {
            return BuildNumber(format, prefix, sequenceNumber, year, null);
        }
    }
}

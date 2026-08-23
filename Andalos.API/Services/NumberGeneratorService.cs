using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public class NumberGeneratorService : INumberGeneratorService
    {
        private readonly AppDbContext _db;
        private readonly ISettingService _settings;

        // خريطة بين مفتاح التسلسل ومفتاح الإعداد الخاص بصيغته
        private static readonly Dictionary<string, string> FormatKeyMap = new()
        {
            { "Contract", SettingKeys.ContractNumberFormat },
            { "Receipt", SettingKeys.ReceiptNumberFormat },
            { "Maintenance", SettingKeys.MaintenanceNumberFormat },
            { "Expense", SettingKeys.ExpenseNumberFormat },
            { "PassCode", SettingKeys.PassCodeFormat }
        };

        public NumberGeneratorService(AppDbContext db, ISettingService settings)
        {
            _db = db;
            _settings = settings;
        }

        public async Task<string> GenerateAsync(string sequenceKey)
        {
            // 1. جلب الصيغة من الإعدادات
            if (!FormatKeyMap.TryGetValue(sequenceKey, out var formatKey))
                throw new ArgumentException($"مفتاح التسلسل '{sequenceKey}' غير معروف");

            string format = await _settings.GetValueAsync(formatKey) ?? GetDefaultFormat(sequenceKey);

            // 2. جلب أو إنشاء سجل العداد
            var now = DateTime.Now;
            var sequence = await _db.NumberSequences
                .FirstOrDefaultAsync(s => s.SequenceKey == sequenceKey);

            if (sequence == null)
            {
                sequence = new NumberSequence
                {
                    SequenceKey = sequenceKey,
                    CurrentYear = now.Year,
                    LastNumber = 0
                };
                _db.NumberSequences.Add(sequence);
            }

            // 3. إذا تغيرت السنة → إعادة العداد للصفر
            if (sequence.CurrentYear != now.Year)
            {
                sequence.CurrentYear = now.Year;
                sequence.LastNumber = 0;
            }

            // 4. زيادة العداد
            sequence.LastNumber++;
            sequence.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // 5. بناء الرقم النهائي بناءً على الصيغة
            return BuildNumber(format, sequence.LastNumber, now);
        }

        private static string BuildNumber(string format, int seqNumber, DateTime now)
        {
            string result = format;

            // استبدال الرموز
            result = result.Replace("{YYYY}", now.Year.ToString());
            result = result.Replace("{YY}", now.ToString("yy"));
            result = result.Replace("{MM}", now.Month.ToString("D2"));
            result = result.Replace("{DD}", now.Day.ToString("D2"));

            // استبدال {SEQ:n} بالرقم التسلسلي مع الحشو بالأصفار
            // مثال: {SEQ:4} → 0001, {SEQ:5} → 00001
            var seqMatch = System.Text.RegularExpressions.Regex.Match(result, @"\{SEQ:(\d+)\}");
            if (seqMatch.Success)
            {
                int padding = int.Parse(seqMatch.Groups[1].Value);
                result = result.Replace(seqMatch.Value, seqNumber.ToString().PadLeft(padding, '0'));
            }

            return result;
        }

        private static string GetDefaultFormat(string sequenceKey)
        {
            return sequenceKey switch
            {
                "Contract" => "CTR-{YYYY}-{SEQ:4}",
                "Receipt" => "REC-{YYYY}-{SEQ:5}",
                "Maintenance" => "MNT-{YYYY}-{SEQ:4}",
                "Expense" => "EXP-{YYYY}-{SEQ:5}",
                "PassCode" => "PASS-{SEQ:6}",
                _ => "{SEQ:6}"
            };
        }
    }
}
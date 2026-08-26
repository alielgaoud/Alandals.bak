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
            if (!FormatKeyMap.TryGetValue(sequenceKey, out var formatKey))
                throw new ArgumentException($"مفتاح التسلسل '{sequenceKey}' غير معروف");

            string format = await _settings.GetValueAsync(formatKey) ?? GetDefaultFormat(sequenceKey);
            var now = DateTime.Now;

            // 1. جلب أو إنشاء سجل العداد
            var sequence = await _db.NumberSequences
                .FirstOrDefaultAsync(s => s.SequenceKey == sequenceKey);

            if (sequence == null)
            {
                // إذا كان ينشأ لأول مرة، نتحقق من عدد السجلات الفعلية الموجودة لتفادي التكرار
                int existingCount = await GetExistingCountAsync(sequenceKey, now.Year);

                sequence = new NumberSequence
                {
                    SequenceKey = sequenceKey,
                    CurrentYear = now.Year,
                    LastNumber = existingCount
                };
                _db.NumberSequences.Add(sequence);
            }

            // 2. إذا تغيرت السنة → إعادة العداد
            if (sequence.CurrentYear != now.Year)
            {
                sequence.CurrentYear = now.Year;
                sequence.LastNumber = 0;
            }

            // 3. توليد رقم فريد والتأكد من عدم وجوده في قاعدة البيانات (حماية من التكرار)
            string generatedNumber;
            bool exists;
            do
            {
                sequence.LastNumber++;
                generatedNumber = BuildNumber(format, sequence.LastNumber, now);
                exists = await CheckIfNumberExistsAsync(sequenceKey, generatedNumber);
            }
            while (exists); // إذا وُجد رقم مكرر، سيزيد العداد فوراً للرقم التالي

            sequence.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return generatedNumber;
        }

        // ===== فحص وجود الرقم مسبقاً لمنع أي خطأ SQL =====
        private async Task<bool> CheckIfNumberExistsAsync(string sequenceKey, string number)
        {
            return sequenceKey switch
            {
                "Contract" => await _db.Contracts.AnyAsync(c => c.ContractNumber == number),
                "Receipt" => await _db.Payments.AnyAsync(p => p.ReceiptNumber == number),
                "Maintenance" => await _db.MaintenanceRequests.AnyAsync(m => m.RequestNumber == number),
                "Expense" => await _db.Expenses.AnyAsync(e => e.ExpenseNumber == number),
                "PassCode" => await _db.VisitorPasses.AnyAsync(v => v.PassCode == number),
                _ => false
            };
        }

        private async Task<int> GetExistingCountAsync(string sequenceKey, int year)
        {
            return sequenceKey switch
            {
                "Contract" => await _db.Contracts.CountAsync(c => c.CreatedAt.Year == year),
                "Receipt" => await _db.Payments.CountAsync(p => p.CreatedAt.Year == year),
                "Maintenance" => await _db.MaintenanceRequests.CountAsync(m => m.CreatedAt.Year == year),
                "Expense" => await _db.Expenses.CountAsync(e => e.CreatedAt.Year == year),
                "PassCode" => await _db.VisitorPasses.CountAsync(v => v.CreatedAt.Year == year),
                _ => 0
            };
        }

        private static string BuildNumber(string format, int seqNumber, DateTime now)
        {
            string result = format;
            result = result.Replace("{YYYY}", now.Year.ToString());
            result = result.Replace("{YY}", now.ToString("yy"));
            result = result.Replace("{MM}", now.Month.ToString("D2"));
            result = result.Replace("{DD}", now.Day.ToString("D2"));

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
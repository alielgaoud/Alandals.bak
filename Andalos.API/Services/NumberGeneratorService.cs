using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public class NumberGeneratorService : INumberGeneratorService
    {
        private readonly AppDbContext _db;

        public NumberGeneratorService(AppDbContext db)
        {
            _db = db;
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
            => GenerateNumberAsync("Refund", "Numbering.RefundFormat", "Numbering.RefundPrefix");

        public async Task<string> GenerateNumberAsync(string sequenceKey, string formatSettingKey, string prefixSettingKey)
        {
            var format = await GetSettingValueAsync(formatSettingKey) ?? $"{sequenceKey.ToUpper()}-{{YYYY}}-{{SEQ:5}}";
            var prefix = await GetSettingValueAsync(prefixSettingKey) ?? sequenceKey.ToUpper();

            int currentYear = DateTime.Now.Year;

            // 🛡️ حماية التزامن تحت الضغط: قفل صف العداد بـ (UPDLOCK, ROWLOCK) داخل معاملة صريحة
            // بحيث ينتظر أي طلب متزامن حتى يُحفظ الرقم الحالي — لا تكرار أرقام إطلاقاً
            // ملاحظة: EnableRetryOnFailure يستلزم تغليف المعاملة اليدوية بـ ExecutionStrategy
            var strategy = _db.Database.CreateExecutionStrategy();
            var generatedNumber = await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync();

                var rows = await _db.NumberSequences
                    .FromSqlInterpolated($"SELECT * FROM NumberSequences WITH (UPDLOCK, ROWLOCK) WHERE SequenceKey = {sequenceKey}")
                    .ToListAsync();
                var sequence = rows.FirstOrDefault();

                if (sequence == null)
                {
                    // يحدث مرة واحدة فقط لكل نوع رقم (موجودة مسبقاً في القاعدة فعلياً)
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

                string number;
                bool exists;

                // 👈 حلقة ذكية لضمان تخطي أي رقم موجود سابقاً في قاعدة البيانات
                do
                {
                    sequence.LastNumber += 1;
                    sequence.UpdatedAt = DateTimeHelper.LibyaNow;
                    number = BuildNumber(format, prefix, sequence.LastNumber, currentYear);

                    exists = await CheckIfNumberExistsAsync(sequenceKey, number);
                }
                while (exists);

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return number;
            });

            return generatedNumber;
        }

        // 👈 دالة تحقق تمنع تكرار الأرقام في كل جداول المنظومة
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

        private async Task<string?> GetSettingValueAsync(string key)
        {
            var setting = await _db.Settings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SettingKey == key);

            return string.IsNullOrWhiteSpace(setting?.SettingValue)
                ? setting?.DefaultValue
                : setting.SettingValue;
        }

        private static string BuildNumber(string format, string prefix, int sequenceNumber, int year)
        {
            string result = format;

            result = result.Replace("{YYYY}", year.ToString());
            result = result.Replace("{YY}", year.ToString().Substring(2, 2));
            result = result.Replace("{PREFIX}", prefix);
            result = result.Replace("{MM}", DateTime.Now.ToString("MM"));
            result = result.Replace("{DD}", DateTime.Now.ToString("dd"));

            var seqTokenStart = result.IndexOf("{SEQ", StringComparison.OrdinalIgnoreCase);
            if (seqTokenStart >= 0)
            {
                var seqTokenEnd = result.IndexOf("}", seqTokenStart);
                if (seqTokenEnd > seqTokenStart)
                {
                    var token = result.Substring(seqTokenStart, seqTokenEnd - seqTokenStart + 1);
                    int pad = 5;
                    var parts = token.Trim('{', '}').Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[1], out var parsedPad))
                        pad = parsedPad;

                    result = result.Replace(token, sequenceNumber.ToString().PadLeft(pad, '0'));
                }
            }
            else
            {
                result = $"{prefix}-{year}-{sequenceNumber:D5}";
            }

            return result;
        }
    }
}
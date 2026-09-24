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
        private readonly ISettingService _settingService;

        public NumberGeneratorService(AppDbContext db, ISettingService settingService)
        {
            _db = db;
            _settingService = settingService;
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

        public async Task<string> GenerateNumberAsync(string sequenceKey, string formatSettingKey, string prefixSettingKey)
        {
            // الآن يقرأ من الإعدادات المتكاملة عبر ISettingService (مع Cache)
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

            do
            {
                sequence.LastNumber += 1;
                sequence.UpdatedAt = DateTimeHelper.LibyaNow;
                generatedNumber = BuildNumber(format, prefix, sequence.LastNumber, currentYear);

                exists = await CheckIfNumberExistsAsync(sequenceKey, generatedNumber);
            }
            while (exists);

            await _db.SaveChangesAsync();

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
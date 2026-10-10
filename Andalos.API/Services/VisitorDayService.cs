using Andalos.API.Constants;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;

namespace Andalos.API.Services
{
    public class VisitorDayService : IVisitorDayService
    {
        // القيمة الافتراضية إذا لم تُضبط في الإعدادات (نفس قيمة المجدول الليلي)
        public const int DefaultDayEndHour = 3;

        private readonly ISettingService _settings;

        public VisitorDayService(ISettingService settings)
        {
            _settings = settings;
        }

        public async Task<int> GetDayEndHourAsync()
        {
            int hour = await _settings.GetValueAsync<int>(SettingKeys.VisitorWalletExpirationHour, DefaultDayEndHour);
            return Math.Clamp(hour, 0, 23);
        }

        public async Task<DateTime> GetCurrentBusinessDateAsync()
        {
            int dayEndHour = await GetDayEndHourAsync();
            return DateTimeHelper.VisitorBusinessDate(DateTimeHelper.LibyaNow, dayEndHour);
        }

        public async Task<DateTime> GetBusinessDayEndAsync(DateTime businessDate)
        {
            int dayEndHour = await GetDayEndHourAsync();
            return DateTimeHelper.VisitorBusinessDayEnd(businessDate, dayEndHour);
        }
    }
}

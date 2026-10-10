namespace Andalos.API.Interfaces
{
    // 👈 مصدر واحد لـ "اليوم التشغيلي" للزوار (تصاريح QR ومحفظة الزوار) — يُقرأ من الإعدادات
    public interface IVisitorDayService
    {
        // ساعة نهاية اليوم التشغيلي (0-23) من الإعدادات: Visitor.WalletExpirationHour
        Task<int> GetDayEndHourAsync();

        // تاريخ اليوم التشغيلي الحالي (بتوقيت ليبيا) — هذا هو التاريخ الذي يُقارن به تصريح الزائر
        Task<DateTime> GetCurrentBusinessDateAsync();

        // لحظة انتهاء صلاحية يوم تشغيلي معيّن
        Task<DateTime> GetBusinessDayEndAsync(DateTime businessDate);
    }
}

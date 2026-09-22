namespace Andalos.API.Helpers
{
    public static class DateTimeHelper
    {
        // 👈 التوقيت المحلي الرسمي (UTC + 2 ساعات)
        public static DateTime LibyaNow => DateTime.UtcNow.AddHours(2);

        // 👈 تاريخ اليوم الحالي بتوقيت UTC+2 (بدون ساعات ودقائق)
        public static DateTime LibyaToday => LibyaNow.Date;
    }
}
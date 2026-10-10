namespace Andalos.API.Helpers
{
    public static class DateTimeHelper
    {
        // 👈 التوقيت المحلي الرسمي (UTC + 2 ساعات)
        // ليبيا على UTC+2 طوال السنة (لا توقيت صيفي)، لذلك الحساب من UtcNow دقيق بغض النظر عن توقيت جهاز السيرفر
        //
        // ⚠️ مهم: يجب أن يكون Kind = Unspecified. لو بقي Kind = Utc (كما يحدث مع UtcNow.AddHours)
        // فإن JSON يكتب اللاحقة "Z" ويقرأها المتصفح كتوقيت UTC فيضيف ساعتين أخرى في ليبيا
        // (وهذا هو سبب ظهور الوقت متقدماً ساعتين والتصريح "منتهياً" في الواجهة).
        // القيمة نفسها لا تتغير، ومقارنات DateTime وطرحها تتجاهل Kind.
        public static DateTime LibyaNow => DateTime.SpecifyKind(DateTime.UtcNow.AddHours(2), DateTimeKind.Unspecified);

        // 👈 تاريخ اليوم الحالي بتوقيت UTC+2 (بدون ساعات ودقائق)
        public static DateTime LibyaToday => LibyaNow.Date;

        // 👈 اليوم التشغيلي للزوار: يبدأ عند منتصف الليل، ويمتد حتى ساعة "نهاية اليوم" (من الإعدادات)
        // مثال: إذا كانت نهاية اليوم 03:00 فإن الساعة 01:00 فجراً تنتمي لليوم السابق.
        // عند 0 يصبح اليوم التشغيلي هو التاريخ الميلادي العادي.
        public static DateTime VisitorBusinessDate(DateTime libyaNow, int dayEndHour)
        {
            int hour = Math.Clamp(dayEndHour, 0, 23);
            return libyaNow.AddHours(-hour).Date;
        }

        // 👈 لحظة انتهاء صلاحية اليوم التشغيلي (مثال: يوم 10 مع نهاية 03:00 ينتهي عند 11 الساعة 03:00)
        public static DateTime VisitorBusinessDayEnd(DateTime businessDate, int dayEndHour)
        {
            int hour = Math.Clamp(dayEndHour, 0, 23);
            return businessDate.Date.AddDays(1).AddHours(hour);
        }
    }
}

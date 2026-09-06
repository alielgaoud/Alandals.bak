namespace Andalos.API.Enums
{
    // نوع القيمة: ثابتة أم نسبة
    public enum FeeValueType
    {
        Fixed = 1,       // مبلغ ثابت بالدينار
        Percentage = 2   // نسبة مئوية %
    }

    // دورية التكرار: مرة واحدة أم شهرية
    public enum FeeFrequency
    {
        OneTime = 1,     // مرة واحدة فقط عند بداية العقد
        Monthly = 2      // متكررة مع كل شهر إيجار
    }
}
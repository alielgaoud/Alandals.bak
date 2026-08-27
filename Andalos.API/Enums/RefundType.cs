namespace Andalos.API.Enums
{
    public enum RefundType
    {
        Overpayment = 1,     // إرجاع مبلغ مدفوع بالزيادة
        DepositReturn = 2,   // إرجاع قيمة التأمين/الضمان عند نهاية العقد
        Correction = 3,      // سند تصحيح لخطأ حسابي
        CancelledService = 4 // إرجاع مبالغ خدمات ملغاة (صيانة، رسوم...)
    }
}
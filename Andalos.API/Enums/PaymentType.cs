namespace Andalos.API.Enums
{
    public enum PaymentType
    {
        Rent = 1,          // إيجار
        Electricity = 2,   // كهرباء
        Water = 3,         // مياه
        Fees = 4,          // رسوم إضافية
        Deposit = 5,       // عربون / ضمان
        Maintenance = 6,   // صيانة
        Other = 7,         // أخرى
        AdvancePayment = 8 // 👈 جديد: دفعة مقدمة (إيداع في رصيد المستأجر)
    }

    // 👈 جديد: توجيه الدفعة عند إضافتها
    public enum PaymentAllocationMode
    {
        Regular = 1,      // دفعة عادية بنوع محدد (كالوضع الحالي — تدخل الكشف مباشرة)
        SettleCharge = 2, // تسوية الدفعة بتحميل معين (صيانة/خدمة) جزئياً أو كلياً — الفائض يبقى رصيداً
        OnAccount = 3     // دفعة على الحساب — تُودع رصيداً كاملاً وتُخصم لاحقاً تلقائياً من أي مستحق
    }

    public enum PaymentMethod
    {
        Cash = 1,       // نقدي
        Transfer = 2,   // تحويل بنكي
        Check = 3,      // شيك
        Card = 4,       // بطاقة
        FromBalance = 5 // 👈 جديد: تسديد آلي مخصوم من رصيد المستأجر
    }
}
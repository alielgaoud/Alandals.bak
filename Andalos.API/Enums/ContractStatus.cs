namespace Andalos.API.Enums
{
    public enum ContractStatus
    {
        Pending = 1,      // معلق (مسودة)
        Active = 2,       // ساري العمل به
        Expired = 3,      // منتهي الصلاحية
        Terminated = 4 ,   // مفسوخ/ملغي
        Renewed = 5     // 👈 تم تجديده بعقد جديد

    }
    public enum RentCycle
    {
        Monthly = 1,       // شهري
        Quarterly = 2,     // ربع سنوي (كل 3 أشهر)
        SemiAnnually = 3,  // نصف سنوي (كل 6 أشهر)
        Annually = 4       // سنوي

    }
    // 👈 1. نوع الزيادة السنوية عند التجديد
    public enum IncreaseType
    {
        Percentage = 1,  // نسبة مئوية (مثلاً 5%)
        FixedAmount = 2, // مبلغ ثابت بالدينار (مثلاً 100 د.ل)
        None = 3         // بدون زيادة (0%)
    }
}
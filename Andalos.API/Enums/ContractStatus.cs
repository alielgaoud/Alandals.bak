namespace Andalos.API.Enums
{
    public enum ContractStatus
    {
        Pending = 1,      // معلق (مسودة)
        Active = 2,       // ساري العمل به
        Expired = 3,      // منتهي الصلاحية
        Terminated = 4    // مفسوخ/ملغي
    }
    public enum RentCycle
    {
        Monthly = 1,       // شهري
        Quarterly = 2,     // ربع سنوي (كل 3 أشهر)
        SemiAnnually = 3,  // نصف سنوي (كل 6 أشهر)
        Annually = 4       // سنوي
    }
}
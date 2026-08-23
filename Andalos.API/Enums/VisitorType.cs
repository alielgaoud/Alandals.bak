namespace Andalos.API.Enums
{
    public enum VisitorType
    {
        Family = 1,        // عائلة
        Customer = 2,      // زبون محل
        Maintenance = 3,   // فني صيانة
        Supplier = 4,      // مورد بضائع
        ManagementGuest = 5 // ضيف إدارة / VIP
    }
    public enum PassStatus
    {
        Active = 1,    // صالح للاستخدام
        Used = 2,      // تم استخدامه بالكامل
        Expired = 3,   // منتهي الصلاحية (مضى يومه)
        Revoked = 4    // ملغي / مسحوب من الإدارة أو المحل
    }
}
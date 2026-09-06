namespace Andalos.API.Enums
{
    public enum UserRole
    {
        SuperAdmin = 1,    // مدير النظام
        Admin = 2,         // مدير
        Accountant = 3,    // محاسب
        GateKeeper = 4,    // حارس البوابة
        Tenant = 5,
                TenantStaff = 6 // 🆕 موظف تابع لمستأجر

    }
}
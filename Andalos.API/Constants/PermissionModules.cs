namespace Andalos.API.Constants
{
    public static class PermissionModules
    {
        // القسم => المفاتيح التفصيلية خلف الكواليس
        // Derived from the same action catalogue, never consulted by authorization.
        public static readonly IReadOnlyDictionary<string, string[]> ModuleMap =
            Permissions.GetAllPermissions().GroupBy(k => k.Split('.')[0])
                .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);

        // أسماء عربية جاهزة للعرض
        public static readonly Dictionary<string, string> ModuleDisplayNames = new()
        {
            ["Units"] = "المحلات",
            ["Gate"] = "البوابة",
            ["Notifications"] = "إرسال الإشعارات",
            ["Tenants"] = "المستأجرين",
            ["Contracts"] = "العقود",
            ["Financials"] = "المالية",
            ["Expenses"] = "المصروفات",
            ["Complaints"] = "الشكاوى",
            ["BankTransfers"] = "الحوالات البنكية",
            ["Visitors"] = "الزوار",
            ["VisitorWallet"] = "محفظة الزوار والبوابة",
            ["Maintenance"] = "الصيانة",
            ["Reports"] = "التقارير واللوحة",
            ["Demands"] = "المطالبات المالية",
            ["Settings"] = "الإعدادات",
            ["Users"] = "المستخدمين والصلاحيات",
            ["Circulars"] = "التعاميم"
        };
    }
}
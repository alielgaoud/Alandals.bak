namespace Andalos.API.Constants
{
    public static class Permissions
    {
        // ===== 1. المحلات (Units) =====
        public static class Units
        {
            public const string View = "Units.View";
            public const string Create = "Units.Create";
            public const string Edit = "Units.Edit";
            public const string Delete = "Units.Delete";
            public const string ViewHistory = "Units.ViewHistory"; // رؤية سجل المحل الشامل
        }

        // ===== 2. المستأجرين (Tenants) =====
        public static class Tenants
        {
            public const string View = "Tenants.View";
            public const string Create = "Tenants.Create";
            public const string Edit = "Tenants.Edit";
            public const string Delete = "Tenants.Delete";
            public const string ViewStatement = "Tenants.ViewStatement"; // رؤية كشف الحساب
            public const string ViewBalances = "Tenants.ViewBalances";   // استعراض ديون وأرصدة المستأجرين
        }

        // ===== 3. العقود (Contracts) =====
        public static class Contracts
        {
            public const string View = "Contracts.View";
            public const string Create = "Contracts.Create";
            public const string Edit = "Contracts.Edit"; // تعديل البيانات البسيطة
            public const string Renew = "Contracts.Renew"; // تجديد العقد وتصعيد الإيجار
            public const string UpdateStatus = "Contracts.UpdateStatus"; // إيقاف/تفعيل العقد
            public const string Delete = "Contracts.Delete";
            public const string ExportPdf = "Contracts.ExportPdf"; // طباعة العقد
        }

        // ===== 4. المالية والحسابات (Financials) =====
        public static class Financials
        {
            public const string DeletePayment = "Financials.DeletePayment";
            public const string DeleteRefund = "Financials.DeleteRefund";
            public const string ViewCharges = "Financials.ViewCharges";
            public const string ChargeTenant = "Financials.ChargeTenant";
            public const string SettleCharge = "Financials.SettleCharge";
            public const string ViewPayments = "Financials.ViewPayments"; // استعراض الدفعات
            public const string CreatePayment = "Financials.CreatePayment"; // تسجيل دفعة كاش
            public const string DepositAdvance = "Financials.DepositAdvance"; // إيداع دفعة في المحفظة
            public const string ProcessMonthlyDues = "Financials.ProcessMonthlyDues"; // تشغيل الخصم الشهري الآلي يدوياً
            public const string ViewRefunds = "Financials.ViewRefunds"; // استعراض المرتجعات
            public const string CreateRefund = "Financials.CreateRefund"; // تسجيل مرتجع
        }

        // ===== 5. المصروفات (Expenses) =====
        public static class Expenses
        {
            public const string View = "Expenses.View";
            public const string Create = "Expenses.Create";
            public const string Delete = "Expenses.Delete";
        }

        // ===== 6. المطالبات المالية (Demand Letters) =====
        public static class Demands
        {
            public const string GeneratePdf = "Demands.GeneratePdf"; // إنشاء مطالبة PDF
            public const string SendToTenant = "Demands.SendToTenant"; // إرسال المطالبة كإشعار للمستأجر
        }

        // ===== 7. الشكاوى والردود (Complaints) =====
        public static class Complaints
        {
            public const string View = "Complaints.View";
            public const string Reply = "Complaints.Reply"; // الرد على شكوى
            public const string UpdateStatus = "Complaints.UpdateStatus"; // تغيير حالة الشكوى (مغلقة/قيد المعالجة)
            public const string ExportPdf = "Complaints.ExportPdf"; // طباعة تقرير الشكاوى
        }

        // ===== 8. الحوالات البنكية (Bank Transfers) =====
        public static class BankTransfers
        {
            public const string View = "BankTransfers.View";
            public const string Review = "BankTransfers.Review"; // قبول/رفض الحوالة وتعديل المبلغ
        }

        // ===== 9. محفظة الزوار والبوابة (Visitor Wallet) =====
        public static class VisitorWallet
        {
            public const string IssuePaidPass = "VisitorWallet.IssuePaidPass";
            public const string AddBalanceToPass = "VisitorWallet.AddBalanceToPass";
            public const string ViewMyShiftSummary = "VisitorWallet.ViewMyShiftSummary";
            public const string ViewTenantHistory = "VisitorWallet.ViewTenantHistory";
            public const string ViewAllShopsBalances = "VisitorWallet.ViewAllShopsBalances"; // رؤية مستحقات جميع المحلات
            public const string SettleShopBalance = "VisitorWallet.SettleShopBalance"; // تسديد مستحقات محل وتصفير الـ QR
            public const string HandoverShift = "VisitorWallet.HandoverShift"; // استلام عهدة الحارس
            public const string ViewGateCashReport = "VisitorWallet.ViewGateCashReport"; // تقرير صندوق البوابة
        }

        // ===== 10. الزوار وتصاريح الدخول (Visitor Passes) =====
        public static class Visitors
        {
            public const string Create = "Visitors.Create";
            public const string CheckBlacklist = "Visitors.CheckBlacklist";
            public const string View = "Visitors.View";
            public const string RevokePass = "Visitors.RevokePass"; // إبطال تصريح
            public const string ManageBlacklist = "Visitors.ManageBlacklist"; // القائمة السوداء
        }

        // ===== 11. الصيانة (Maintenance) =====
        public static class Maintenance
        {
            public const string Delete = "Maintenance.Delete";
            public const string View = "Maintenance.View";
            public const string Create = "Maintenance.Create";
            public const string EditStatus = "Maintenance.EditStatus"; // تغيير حالة طلب الصيانة
        }

        // ===== 12. التقارير واللوحة (Reports) =====
        public static class Reports
        {
            public const string ViewVisitorTrafficReports = "Reports.ViewVisitorTrafficReports";
            public const string ViewDashboard = "Reports.ViewDashboard"; // رؤية الصفحة الرئيسية والعدادات
            public const string ViewFinancialReports = "Reports.ViewFinancialReports"; // تقارير الدخل والمصروفات
            public const string ViewOccupancyReports = "Reports.ViewOccupancyReports"; // تقرير إشغال المحلات
        }

        // ===== 13. الإعدادات (Settings) =====
        public static class Settings
        {
            public const string Reset = "Settings.Reset";
            public const string ResetDatabase = "Settings.ResetDatabase";
            public const string View = "Settings.View";
            public const string Edit = "Settings.Edit"; // تغيير إعدادات النظام (الرسوم، الـ VAPID، الترقيم...)
        }

        // ===== 14. إدارة المستخدمين (Users Management) =====
        public static class Users
        {
            public const string ViewAuditLogs = "Users.ViewAuditLogs";
            public const string View = "Users.View";
            public const string Create = "Users.Create";
            public const string Edit = "Users.Edit";
            public const string Delete = "Users.Delete";
            public const string ResetPassword = "Users.ResetPassword"; // تغيير باسوورد الموظفين
            public const string ToggleLock = "Users.ToggleLock"; // قفل حساب موظف
            public const string ManagePermissions = "Users.ManagePermissions"; // إعطاء وسحب الصلاحيات التفصيلية
        }

        // 👈 دالة مساعدة ترجع كل الصلاحيات المتاحة كـ List لغرض الـ Checkbox في الـ Frontend
        private static readonly string[] Keys = typeof(Permissions)
            .GetNestedTypes()
            .SelectMany(t => t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        private static readonly HashSet<string> KeySet = new(Keys, StringComparer.Ordinal);
        public static List<string> GetAllPermissions() => Keys.ToList();
        public static bool IsKnown(string key) => KeySet.Contains(key);
        public static List<string> Validate(IEnumerable<string>? keys)
        {
            if (keys is null) throw new ArgumentException("permissions/permissionKeys must be an array, not null.");
            var list = keys.ToList();
            if (list.Count > Keys.Length || list.Any(k => k is null || !IsKnown(k)))
                throw new ArgumentException("Unknown permission key.");
            return list.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        }
        public static List<string> ModulesFor(IEnumerable<string> keys) => keys
            .Where(IsKnown).Select(k => k.Split('.')[0]).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToList();

        // ===== 15. التعاميم (Circulars) =====
        public static class Circulars
        {
            public const string View = "Circulars.View";       // استعراض التعاميم
            public const string Create = "Circulars.Create";   // إنشاء تعميم ونشره
            public const string Edit = "Circulars.Edit";       // تعديل تعميم
            public const string Delete = "Circulars.Delete";   // حذف تعميم
        }
        public static class Gate
        {
            public const string Scan = "Gate.Scan";
            public const string ViewLogs = "Gate.ViewLogs";
        }
        public static class Notifications
        {
            public const string Send = "Notifications.Send";
        }
    }
}
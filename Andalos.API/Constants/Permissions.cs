namespace Andalos.API.Constants
{
    public static class Permissions
    {
        // ===== 1. المحلات =====
        public static class Units
        {
            public const string View = "Units.View";
            public const string Create = "Units.Create";
            public const string Edit = "Units.Edit";
            public const string Delete = "Units.Delete";
        }

        // ===== 2. المستأجرين =====
        public static class Tenants
        {
            public const string View = "Tenants.View";
            public const string Create = "Tenants.Create";
            public const string Edit = "Tenants.Edit";
            public const string Delete = "Tenants.Delete";
            public const string ViewStatement = "Tenants.ViewStatement";
        }

        // ===== 3. العقود =====
        public static class Contracts
        {
            public const string View = "Contracts.View";
            public const string Create = "Contracts.Create";
            public const string Renew = "Contracts.Renew";
            public const string UpdateStatus = "Contracts.UpdateStatus";
            public const string Delete = "Contracts.Delete";
            public const string ExportPdf = "Contracts.ExportPdf";
        }

        // ===== 4. الحسابات والمالية =====
        public static class Financials
        {
            public const string ViewPayments = "Financials.ViewPayments";
            public const string CreatePayment = "Financials.CreatePayment";
            public const string DepositAdvance = "Financials.DepositAdvance";
            public const string ProcessMonthlyDues = "Financials.ProcessMonthlyDues";
            public const string ViewRefunds = "Financials.ViewRefunds";
            public const string CreateRefund = "Financials.CreateRefund";
        }

        // ===== 5. المصروفات =====
        public static class Expenses
        {
            public const string View = "Expenses.View";
            public const string Create = "Expenses.Create";
            public const string Delete = "Expenses.Delete";
        }

        // ===== 6. الشكاوى والردود =====
        public static class Complaints
        {
            public const string View = "Complaints.View";
            public const string Reply = "Complaints.Reply";
            public const string UpdateStatus = "Complaints.UpdateStatus";
            public const string ExportPdf = "Complaints.ExportPdf";
        }

        // ===== 7. الحوالات البنكية =====
        public static class BankTransfers
        {
            public const string View = "BankTransfers.View";
            public const string Review = "BankTransfers.Review"; // قبول/رفض وتعديل المبلغ
        }

        // ===== 8. الزوار وتصاريح الدخول =====
        public static class Visitors
        {
            public const string View = "Visitors.View";
            public const string CreatePass = "Visitors.CreatePass";
            public const string ScanPass = "Visitors.ScanPass"; // بوابة الأمن
            public const string ManageBlacklist = "Visitors.ManageBlacklist";
        }

        // ===== 9. الصيانة =====
        public static class Maintenance
        {
            public const string View = "Maintenance.View";
            public const string Create = "Maintenance.Create";
            public const string EditStatus = "Maintenance.EditStatus";
        }

        // ===== 10. التقارير واللوحة =====
        public static class Reports
        {
            public const string ViewDashboard = "Reports.ViewDashboard";
            public const string ViewFinancialReports = "Reports.ViewFinancialReports";
            public const string ViewOccupancyReports = "Reports.ViewOccupancyReports";
        }

        // ===== 11. إدارة المستخدمين والنظام =====
        public static class SystemAdmin
        {
            public const string ManageUsers = "SystemAdmin.ManageUsers";
            public const string ManagePermissions = "SystemAdmin.ManagePermissions";
            public const string ManageSettings = "SystemAdmin.ManageSettings";
        }

        // دالة مساعدة ترجع كل الصلاحيات المتاحة في النظام كـ List
        public static List<string> GetAllPermissions()
        {
            return typeof(Permissions)
                .GetNestedTypes()
                .SelectMany(t => t.GetFields().Select(f => f.GetValue(null)?.ToString()))
                .Where(p => p != null)
                .ToList()!;
        }
    }
}
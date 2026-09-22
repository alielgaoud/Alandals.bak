namespace Andalos.API.Constants
{
    public static class PermissionModules
    {
        // القسم => المفاتيح التفصيلية خلف الكواليس
        public static readonly Dictionary<string, string[]> ModuleMap = new()
        {
            ["Units"] = new[]
            {
                Permissions.Units.View, Permissions.Units.Create, Permissions.Units.Edit,
                Permissions.Units.Delete, Permissions.Units.ViewHistory
            },
            ["Tenants"] = new[]
            {
                Permissions.Tenants.View, Permissions.Tenants.Create, Permissions.Tenants.Edit,
                Permissions.Tenants.Delete, Permissions.Tenants.ViewStatement, Permissions.Tenants.ViewBalances
            },
            ["Contracts"] = new[]
            {
                Permissions.Contracts.View, Permissions.Contracts.Create, Permissions.Contracts.Edit,
                Permissions.Contracts.Renew, Permissions.Contracts.UpdateStatus,
                Permissions.Contracts.Delete, Permissions.Contracts.ExportPdf
            },
            ["Financials"] = new[]
            {
                Permissions.Financials.ViewPayments, Permissions.Financials.CreatePayment,
                Permissions.Financials.DepositAdvance, Permissions.Financials.ProcessMonthlyDues,
                Permissions.Financials.ViewRefunds, Permissions.Financials.CreateRefund
            },
            ["Expenses"] = new[]
            {
                Permissions.Expenses.View, Permissions.Expenses.Create, Permissions.Expenses.Delete
            },
            ["Complaints"] = new[]
            {
                Permissions.Complaints.View, Permissions.Complaints.Reply,
                Permissions.Complaints.UpdateStatus, Permissions.Complaints.ExportPdf
            },
            ["BankTransfers"] = new[]
            {
                Permissions.BankTransfers.View, Permissions.BankTransfers.Review
            },
            ["Visitors"] = new[]
            {
                Permissions.Visitors.View, Permissions.Visitors.RevokePass, Permissions.Visitors.ManageBlacklist
            },
            ["VisitorWallet"] = new[]
            {
                Permissions.VisitorWallet.ViewAllShopsBalances, Permissions.VisitorWallet.SettleShopBalance,
                Permissions.VisitorWallet.HandoverShift, Permissions.VisitorWallet.ViewGateCashReport
            },
            ["Maintenance"] = new[]
            {
                Permissions.Maintenance.View, Permissions.Maintenance.Create, Permissions.Maintenance.EditStatus
            },
            ["Reports"] = new[]
            {
                Permissions.Reports.ViewDashboard, Permissions.Reports.ViewFinancialReports,
                Permissions.Reports.ViewOccupancyReports
            },
            ["Demands"] = new[]
            {
                Permissions.Demands.GeneratePdf, Permissions.Demands.SendToTenant
            },
            ["Settings"] = new[]
            {
                Permissions.Settings.View, Permissions.Settings.Edit
            },
            ["Users"] = new[]
            {
                Permissions.Users.View, Permissions.Users.Create, Permissions.Users.Edit,
                Permissions.Users.Delete, Permissions.Users.ResetPassword,
                Permissions.Users.ToggleLock, Permissions.Users.ManagePermissions
            },

            ["Circulars"] = new[]
            {
                Permissions.Circulars.View, Permissions.Circulars.Create,
                Permissions.Circulars.Edit, Permissions.Circulars.Delete
            },
        };

        // أسماء عربية جاهزة للعرض
        public static readonly Dictionary<string, string> ModuleDisplayNames = new()
        {
            ["Units"] = "المحلات",
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
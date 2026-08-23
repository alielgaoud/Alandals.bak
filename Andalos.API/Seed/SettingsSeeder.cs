using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Seed
{
    public static class SettingsSeeder
    {
        public static async Task SeedAsync(AppDbContext db)
        {
            if (await db.Settings.AnyAsync()) return;

            var settings = new List<Setting>
            {
                // ===== بيانات الشركة =====
                New("Company", SettingKeys.CompanyName, "الأندلس للاستثمار السياحي", "String", "اسم الشركة", "الاسم الكامل للشركة", 1),
                New("Company", SettingKeys.CompanyShortName, "الأندلس", "String", "الاسم المختصر", "يظهر في التقارير والفواتير", 2),
                New("Company", SettingKeys.CompanyPhone, "0925288883", "String", "هاتف الشركة", "", 3),
                New("Company", SettingKeys.CompanyEmail, "info@andalos.ly", "String", "البريد الإلكتروني", "", 4),
                New("Company", SettingKeys.CompanyAddress, "ليبيا", "String", "العنوان", "", 5),
                New("Company", SettingKeys.CompanyTaxNumber, "", "String", "الرقم الضريبي", "", 6),

                // ===== المالية =====
                New("Financial", SettingKeys.Currency, "LYD", "Dropdown", "العملة", "LYD / USD / EUR", 1),
                New("Financial", SettingKeys.CurrencySymbol, "د.ل", "String", "رمز العملة", "", 2),
                New("Financial", SettingKeys.DecimalPlaces, "3", "Number", "المنازل العشرية", "", 3),
                New("Financial", SettingKeys.TaxRate, "0", "Percentage", "نسبة الضريبة", "0 = بدون ضريبة", 4),
                New("Financial", SettingKeys.TaxEnabled, "False", "Boolean", "تفعيل الضريبة", "", 5),

                // ===== الترقيم التسلسلي (الأهم!) =====
                New("Numbering", SettingKeys.ContractNumberFormat, "CTR-{YYYY}-{SEQ:4}", "String", "صيغة رقم العقد", "الرموز: {YYYY} سنة, {SEQ:4} تسلسل 4 أرقام", 1, true),
                New("Numbering", SettingKeys.ContractNumberPrefix, "CTR", "String", "بادئة العقود", "", 2),
                New("Numbering", SettingKeys.ReceiptNumberFormat, "REC-{YYYY}-{SEQ:5}", "String", "صيغة رقم سند القبض", "{SEQ:5} = 5 أرقام", 3, true),
                New("Numbering", SettingKeys.ReceiptNumberPrefix, "REC", "String", "بادئة سندات القبض", "", 4),
                New("Numbering", SettingKeys.MaintenanceNumberFormat, "MNT-{YYYY}-{SEQ:4}", "String", "صيغة رقم طلب الصيانة", "", 5, true),
                New("Numbering", SettingKeys.MaintenanceNumberPrefix, "MNT", "String", "بادئة الصيانة", "", 6),
                New("Numbering", SettingKeys.ExpenseNumberFormat, "EXP-{YYYY}-{SEQ:5}", "String", "صيغة رقم سند الصرف", "", 7, true),
                New("Numbering", SettingKeys.ExpenseNumberPrefix, "EXP", "String", "بادئة المصروفات", "", 8),
                New("Numbering", SettingKeys.PassCodeFormat, "PASS-{SEQ:6}", "String", "صيغة كود تصريح الدخول", "", 9, true),
                New("Numbering", SettingKeys.PassCodePrefix, "PASS", "String", "بادئة تصاريح الدخول", "", 10),

                // ===== الإيجارات =====
                New("Rent", SettingKeys.RentDefaultCycle, "Monthly", "Dropdown", "دورة الإيجار الافتراضية", "Monthly / Quarterly / Annually", 1),
                New("Rent", SettingKeys.RentDueDay, "1", "Number", "يوم الاستحقاق", "من كل شهر", 2),
                New("Rent", SettingKeys.RentGraceDays, "5", "Number", "أيام السماح", "قبل احتساب تأخير", 3),
                New("Rent", SettingKeys.RentLateFeeEnabled, "True", "Boolean", "غرامة التأخير", "", 4),
                New("Rent", SettingKeys.RentLateFeePercent, "2", "Percentage", "نسبة غرامة التأخير", "", 5),

                // ===== العقود =====
                New("Contract", SettingKeys.ContractDefaultDuration, "12", "Number", "مدة العقد الافتراضية", "بالأشهر", 1),
                New("Contract", SettingKeys.ContractAutoRenew, "True", "Boolean", "تجديد تلقائي", "", 2),
                New("Contract", SettingKeys.ContractExpiryNoticeDays, "30", "Number", "إشعار انتهاء العقد", "قبل كم يوم", 3),

                // ===== المحلات =====
                New("Unit", SettingKeys.UnitAreaUnit, "SQM", "Dropdown", "وحدة المساحة", "SQM = متر مربع", 1),

                // ===== الزوار =====
                New("Visitor", SettingKeys.VisitorDefaultValidity, "SingleDay", "Dropdown", "صلاحية التصريح الافتراضية", "", 1),
                New("Visitor", SettingKeys.VisitorEntryStart, "09:00", "Time", "بداية الدخول", "", 2),
                New("Visitor", SettingKeys.VisitorEntryEnd, "23:00", "Time", "نهاية الدخول", "", 3),
                New("Visitor", SettingKeys.VisitorFamilyOnly, "True", "Boolean", "عائلات فقط", "", 4),

                // ===== النظام =====
                New("System", SettingKeys.SystemLanguage, "ar", "Dropdown", "اللغة", "ar / en", 1),
                New("System", SettingKeys.SystemTimeZone, "Africa/Tripoli", "String", "المنطقة الزمنية", "", 2),
                New("System", SettingKeys.SystemDateFormat, "DD/MM/YYYY", "Dropdown", "صيغة التاريخ", "", 3),
                New("System", SettingKeys.SystemSessionTimeout, "30", "Number", "مهلة الجلسة", "بالدقائق", 4),
                New("System", SettingKeys.SystemMaintenanceMode, "False", "Boolean", "وضع الصيانة", "إغلاق النظام للصيانة", 5),
            };

            db.Settings.AddRange(settings);
            await db.SaveChangesAsync();
        }

        private static Setting New(string group, string key, string defaultValue, string dataType, string displayName, string description, int sortOrder, bool isRequired = false)
        {
            return new Setting
            {
                SettingKey = key,
                SettingValue = defaultValue,
                SettingGroup = group,
                DataType = dataType,
                DisplayName = displayName,
                Description = description,
                DefaultValue = defaultValue,
                IsRequired = isRequired,
                SortOrder = sortOrder
            };
        }
    }
}
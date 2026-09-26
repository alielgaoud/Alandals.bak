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
            var existingKeys = await db.Settings.Select(s => s.SettingKey).ToListAsync();

            var settings = new List<Setting>
            {
                // ===== بيانات الشركة =====
                New("Company", SettingKeys.CompanyName, "الأندلس للاستثمار السياحي", "String", "اسم الشركة", "الاسم الكامل للشركة", 1),
                New("Company", SettingKeys.CompanyShortName, "الأندلس", "String", "الاسم المختصر", "يظهر في التقارير والفواتير", 2),
                New("Company", SettingKeys.CompanyPhone, "0925288883", "String", "هاتف الشركة", "", 3),
                New("Company", SettingKeys.CompanyEmail, "info@andalos.ly", "String", "البريد الإلكتروني", "", 4),
                New("Company", SettingKeys.CompanyAddress, "ليبيا", "String", "العنوان", "", 5),
                New("Company", SettingKeys.CompanyTaxNumber, "", "String", "الرقم الضريبي", "", 6),
                New("Company", SettingKeys.CompanyLogoPath, "/uploads/logos/logo.png", "Image", "شعار الشركة (رفع مباشر)", "ارفع شعار الشركة بصيغة PNG/JPG/SVG - يظهر في كل PDFs والإشعارات", 7),
                New("Company", SettingKeys.CompanyLogoUrl, "", "Image", "رابط الشعار الكامل (تلقائي)", "يتم تحديثه تلقائياً عند رفع الشعار - يستخدم في الإشعارات الخارجية", 8),
                New("Company", SettingKeys.CompanyFaviconUrl, "/favicon.ico", "Image", "أيقونة المتصفح Favicon (رفع مباشر)", "ارفع أيقونة المتصفح المفضلة - تظهر في تبويب المتصفح", 9),
                New("Company", SettingKeys.CompanyStampUrl, "/uploads/logos/stamp.png", "Image", "ختم الشركة (رفع مباشر)", "ارفع ختم الشركة - يستخدم في العقود والمستندات الرسمية", 10),

                // ===== محتوى عقد الإيجار =====
New("ContractTemplate", "Contract.TemplateTitle", "عقد إيجار محل تجاري", "String", "عنوان العقد", "يظهر أعلى العقد", 1),

New("ContractTemplate", "Contract.TemplateIntro",
    "إنه في يوم {Date} الموافق {HijriDate}، تم الاتفاق بين كل من:",
    "Text", "مقدمة العقد", "الرموز: {Date} التاريخ, {HijriDate} التاريخ الهجري", 2),

New("ContractTemplate", "Contract.LandlordLabel", "الطرف الأول (المؤجر)", "String", "تسمية المؤجر", "", 3),

New("ContractTemplate", "Contract.TenantLabel", "الطرف الثاني (المستأجر)", "String", "تسمية المستأجر", "", 4),

New("ContractTemplate", "Contract.UnitSectionTitle", "البند الأول: بيانات المحل", "String", "عنوان قسم المحل", "", 5),

New("ContractTemplate", "Contract.TermsSectionTitle", "البند الثاني: شروط الإيجار", "String", "عنوان قسم الشروط", "", 6),

New("ContractTemplate", "Contract.PaymentSectionTitle", "البند الثالث: قيمة الإيجار وطريقة السداد", "String", "عنوان قسم الدفع", "", 7),

New("ContractTemplate", "Contract.MaintenanceSectionTitle", "البند الرابع: الصيانة", "String", "عنوان قسم الصيانة", "", 8),

New("ContractTemplate", "Contract.TerminationSectionTitle", "البند الخامس: إنهاء العقد", "String", "عنوان قسم الإنهاء", "", 9),

New("ContractTemplate", "Contract.Clauses",
    "1- يلتزم المستأجر بالحفاظ على المحل بحالة جيدة.\n" +
    "2- لا يجوز للمستأجر التنازل عن العقد أو تأجير المحل من الباطن إلا بموافقة خطية من المؤجر.\n" +
    "3- يلتزم المستأجر بسداد الإيجار في الموعد المحدد.\n" +
    "4- في حال التأخر عن السداد لأكثر من {GraceDays} يوم، يحق للمؤجر فسخ العقد.\n" +
    "5- يتحمل المستأجر تكاليف الصيانة الداخلية ويتحمل المؤجر الصيانة الهيكلية.\n" +
    "6- يلتزم المستأجر بإخلاء المحل عند انتهاء مدة العقد ما لم يتم التجديد.",
    "Text", "بنود العقد", "البنود القانونية - كل بند في سطر", 10, true),

New("ContractTemplate", "Contract.SignatureLandlordLabel", "الطرف الأول (المؤجر)", "String", "توقيع المؤجر", "", 11),

New("ContractTemplate", "Contract.SignatureTenantLabel", "الطرف الثاني (المستأجر)", "String", "توقيع المستأجر", "", 12),

New("ContractTemplate", "Contract.WitnessLabel", "الشهود", "String", "توقيع الشهود", "", 13),

New("ContractTemplate", "Contract.FooterNote",
    "حُرر هذا العقد من نسختين أصليتين، بيد كل طرف نسخة للعمل بموجبها.",
    "Text", "ملاحظة التذييل", "تظهر أسفل العقد", 14),

New("ContractTemplate", "Contract.ShowWitnesses", "False", "Boolean", "إظهار خانة الشهود", "", 15),

New("ContractTemplate", "Contract.ShowHijriDate", "False", "Boolean", "إظهار التاريخ الهجري", "", 16),

                // ===== المالية =====
                New("Financial", SettingKeys.Currency, "LYD", "Dropdown", "العملة", "LYD / USD / EUR", 1),
                New("Financial", SettingKeys.CurrencySymbol, "د.ل", "String", "رمز العملة", "", 2),
                New("Financial", SettingKeys.DecimalPlaces, "3", "Number", "المنازل العشرية", "", 3),
                New("Financial", SettingKeys.TaxRate, "0", "Percentage", "نسبة الضريبة", "0 = بدون ضريبة", 4),
                New("Financial", SettingKeys.TaxEnabled, "False", "Boolean", "تفعيل الضريبة", "", 5),
                New("Numbering", "Numbering.RefundFormat", "{PREFIX}-{YYYY}-{SEQ:5}", "String", "صيغة رقم سند المرتجع", "يستخدم {PREFIX} للبادئة", 11, true),
                New("Numbering", "Numbering.RefundPrefix", "RFD", "String", "بادئة سند المرتجع", "", 12),

                // ===== الترقيم التسلسلي (الأهم!) - يستخدم {PREFIX} ليكون مترابط مع إعداد البادئة =====
                New("Numbering", SettingKeys.ContractNumberFormat, "{PREFIX}-{YYYY}-{SEQ:4}", "String", "صيغة رقم العقد", "الرموز: {PREFIX} البادئة, {YYYY} سنة, {YY} سنتين, {MM} شهر, {DD} يوم, {SEQ:4} تسلسل 4 أرقام", 1, true),
                New("Numbering", SettingKeys.ContractNumberPrefix, "CTR", "String", "بادئة العقود", "تستخدم داخل {PREFIX} في صيغة رقم العقد", 2),
                New("Numbering", SettingKeys.ReceiptNumberFormat, "{PREFIX}-{YYYY}-{SEQ:5}", "String", "صيغة رقم سند القبض", "{PREFIX} البادئة, {SEQ:5} = 5 أرقام", 3, true),
                New("Numbering", SettingKeys.ReceiptNumberPrefix, "REC", "String", "بادئة سندات القبض", "", 4),
                New("Numbering", SettingKeys.MaintenanceNumberFormat, "{PREFIX}-{YYYY}-{SEQ:4}", "String", "صيغة رقم طلب الصيانة", "", 5, true),
                New("Numbering", SettingKeys.MaintenanceNumberPrefix, "MNT", "String", "بادئة الصيانة", "", 6),
                New("Numbering", SettingKeys.ExpenseNumberFormat, "{PREFIX}-{YYYY}-{SEQ:5}", "String", "صيغة رقم سند الصرف", "مثال: EXP-2025-00001 - غيّر البادئة أو الصيغة كاملة", 7, true),
                New("Numbering", SettingKeys.ExpenseNumberPrefix, "EXP", "String", "بادئة المصروفات", "تظهر في رقم المصروف إذا كانت الصيغة تحتوي {PREFIX}", 8),
                New("Numbering", SettingKeys.PassCodeFormat, "{PREFIX}-{SEQ:6}", "String", "صيغة كود تصريح الدخول", "", 9, true),
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
                New("Visitor", SettingKeys.VisitorWalletExpirationHour, "3", "Number", "ساعة تصفير محفظة الزوار", "الساعة (من 0 إلى 23) التي ينتهي فيها رصيد التصاريح اليومي وتحويل المتبقي للإدارة", 5),

                // ===== النظام =====
                New("System", SettingKeys.SystemLanguage, "ar", "Dropdown", "اللغة", "ar / en", 1),
                New("System", SettingKeys.SystemTimeZone, "Africa/Tripoli", "String", "المنطقة الزمنية", "", 2),
                New("System", SettingKeys.SystemDateFormat, "DD/MM/YYYY", "Dropdown", "صيغة التاريخ", "", 3),
                New("System", SettingKeys.SystemSessionTimeout, "30", "Number", "مهلة الجلسة", "بالدقائق", 4),
                New("System", SettingKeys.SystemMaintenanceMode, "False", "Boolean", "وضع الصيانة", "إغلاق النظام للصيانة", 5),
                New("System", SettingKeys.SystemFrontendAdminUrl, "https://admin.marinaalandalus.com", "String", "رابط لوحة الإدارة", "يستخدم في الإشعارات والروابط", 6),
                New("System", SettingKeys.SystemFrontendTenantUrl, "https://tenant.marinaalandalus.com", "String", "رابط بوابة المستأجرين", "يستخدم في الإشعارات والروابط", 7),
                New("System", SettingKeys.SystemBackendUrl, "https://api.marinaalandalus.com", "String", "رابط الـ API", "يستخدم لبناء الروابط الكاملة", 8),

                // ===== الإشعارات =====
               New("Notifications", SettingKeys.NotificationInAppEnabled, "True", "Boolean", "تفعيل الإشعارات الداخلية", "تفعيل جرس التنبيهات داخل النظام", 1),
               New("Notifications", SettingKeys.NotificationPushEnabled, "False", "Boolean", "تفعيل إشعارات الجوال (Web Push)", "تفعيل وصول الإشعارات حتى لو كان النظام مغلقاً", 2),
               New("Notifications", SettingKeys.NotificationVapidSubject, "mailto:info@andalos.ly", "String", "بريد مرسل الإشعارات (VAPID)", "يستخدم للتعريف بسيرفر الإشعارات", 3),
               New("Notifications", SettingKeys.NotificationVapidPublicKey, "", "String", "المفتاح العام للإشعارات", "VAPID Public Key", 4),
               New("Notifications", SettingKeys.NotificationVapidPrivateKey, "", "String", "المفتاح الخاص للإشعارات", "VAPID Private Key", 5),
               New("Notifications", SettingKeys.NotificationIconUrl, "/assets/gold_logo-removebg.png", "Image", "أيقونة الإشعارات (رفع مباشر)", "ارفع أيقونة الإشعارات - تظهر في إشعارات الجوال والمتصفح - يتم تحديثها تلقائياً من الشعار", 6),
               New("Notifications", SettingKeys.NotificationBadgeUrl, "/assets/gold_logo-removebg.png", "Image", "شارة الإشعارات (رفع مباشر)", "Badge للإشعارات - أيقونة صغيرة تظهر في شريط الإشعارات", 7),

                // ===== PDF =====
                New("Pdf", SettingKeys.PdfShowLogo, "True", "Boolean", "إظهار الشعار في PDF", "هل يظهر شعار الشركة في ملفات PDF", 1),
                New("Pdf", SettingKeys.PdfHeaderEnabled, "True", "Boolean", "تفعيل ترويسة PDF", "إظهار الترويسة في ملفات PDF", 2),
                New("Pdf", SettingKeys.PdfFooterEnabled, "True", "Boolean", "تفعيل تذييل PDF", "إظهار التذييل في ملفات PDF", 3),
                New("Pdf", SettingKeys.PdfCompanyInfoInHeader, "True", "Boolean", "بيانات الشركة في الترويسة", "إظهار اسم وهاتف الشركة في ترويسة PDF", 4),
            };

            // ===== ترحيل الصيغ القديمة التي كانت تحتوي بادئة ثابتة إلى صيغة تستخدم {PREFIX} =====
            // هذا يضمن أن تغيير البادئة من الإعدادات يعمل فوراً حتى للقواعد القديمة
            var oldToNewFormatMap = new Dictionary<string, string>
            {
                { "CTR-{YYYY}-{SEQ:4}", "{PREFIX}-{YYYY}-{SEQ:4}" },
                { "REC-{YYYY}-{SEQ:5}", "{PREFIX}-{YYYY}-{SEQ:5}" },
                { "MNT-{YYYY}-{SEQ:4}", "{PREFIX}-{YYYY}-{SEQ:4}" },
                { "EXP-{YYYY}-{SEQ:5}", "{PREFIX}-{YYYY}-{SEQ:5}" },
                { "PASS-{SEQ:6}", "{PREFIX}-{SEQ:6}" },
                { "RFD-{YYYY}-{SEQ:5}", "{PREFIX}-{YYYY}-{SEQ:5}" },
                { "CTR-{YYYY}-{SEQ:5}", "{PREFIX}-{YYYY}-{SEQ:5}" },
                { "EXP-{YYYY}-{SEQ:4}", "{PREFIX}-{YYYY}-{SEQ:4}" },
            };

            var existingSettings = await db.Settings.ToListAsync();
            bool hasMigration = false;

            foreach (var existing in existingSettings)
            {
                if (existing.SettingGroup == "Numbering" && existing.SettingKey.EndsWith("Format"))
                {
                    if (oldToNewFormatMap.TryGetValue(existing.SettingValue ?? "", out var migrated))
                    {
                        existing.SettingValue = migrated;
                        existing.DefaultValue = migrated;
                        existing.UpdatedAt = Andalos.API.Helpers.DateTimeHelper.LibyaNow;
                        hasMigration = true;
                    }
                    // أيضاً إذا كانت القيمة تحتوي بادئة ثابتة معروفة ولا تحتوي {PREFIX}، نحولها تلقائياً
                    else if (!existing.SettingValue.Contains("{PREFIX}", StringComparison.OrdinalIgnoreCase))
                    {
                        // إذا كانت الصيغة تبدأ بـ 3-4 أحرف كبيرة ثم - مثل EXP- أو CTR-، نحولها إلى {PREFIX}-...
                        var val = existing.SettingValue ?? "";
                        var dashIdx = val.IndexOf('-');
                        if (dashIdx >= 2 && dashIdx <= 5)
                        {
                            var leading = val.Substring(0, dashIdx);
                            if (leading.All(char.IsLetter) && leading.All(c => char.IsUpper(c) || c == '_'))
                            {
                                var rest = val.Substring(dashIdx); // يتضمن -
                                var newVal = "{PREFIX}" + rest;
                                // فقط إذا كان الباقي يحتوي {SEQ} لنتأكد أنها صيغة ترقيم
                                if (newVal.Contains("{SEQ", StringComparison.OrdinalIgnoreCase))
                                {
                                    existing.SettingValue = newVal;
                                    existing.DefaultValue = newVal;
                                    existing.UpdatedAt = Andalos.API.Helpers.DateTimeHelper.LibyaNow;
                                    hasMigration = true;
                                }
                            }
                        }
                    }
                }
            }

            if (hasMigration)
            {
                await db.SaveChangesAsync();
            }

            var newSettings = settings.Where(s => !existingKeys.Contains(s.SettingKey)).ToList();

            if (newSettings.Any())
            {
                db.Settings.AddRange(newSettings);
                await db.SaveChangesAsync();
            }
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
# ربط الإعدادات مع كامل المشروع - إصلاح الترقيم والشعار والتكامل الكامل

## المشاكل التي كانت موجودة

### 1. الإعدادات لا تشتغل:
- `SettingService.SetValueAsync` كانت ترجع مباشرة إذا الإعداد غير موجود -> لا يمكن إنشاء إعداد جديد من API
- `GetValueAsync<T>` كانت تستخدم `Convert.ChangeType` فقط -> تفشل مع bool "True"/"False" و int
- `SettingsSeeder` كان يرجع فوراً إذا وجد أي إعداد `if (Any()) return` -> الإعدادات الجديدة (الشعار) لا تُزرع في DB موجودة
- `NumberGeneratorService` كان يقرأ مباشرة من `db.Settings` بدون Cache -> غير متكامل مع `ISettingService` + لا يحترم التحديثات الفورية
- `GetGroupedSettingsAsync` كان يعرّف فقط 9 مجموعات، لكن Seeder ينشئ مجموعة `ContractTemplate` غير معرّفة -> تظهر كـ Raw

### 2. الشعار ثابت:
- `PdfMasterTemplate` كان فيه `CompanyName/Phone/Email` ثابتة hardcoded
- `PushNotificationService` كان فيه `domain = https://tenant.marinaalandalus.com` و `iconUrl = /assets/gold_logo-removebg.png` ثابتة
- لا يوجد إعداد لمسار الشعار
- كل ملفات PDF تستخدم `PdfMasterTemplate.CompanyName` الثابت

### 3. عدم تكامل الإعدادات:
- لا يمكن إضافة إعداد جديد ويكون مترابط مع المشروع
- لا يوجد رفع شعار
- لا يوجد `CompanyInfo` موحد

---

## ما تم إصلاحه

### 1. SettingKeys (Constants/SettingKeys.cs)
**أضيفت مفاتيح جديدة:**
```csharp
Company.LogoPath = "Company.LogoPath"       // /uploads/logos/logo.png
Company.LogoUrl = "Company.LogoUrl"         // https://.../logo.png (كامل)
Company.FaviconUrl = "Company.FaviconUrl"
Company.StampUrl = "Company.StampUrl"       // ختم الشركة
System.FrontendAdminUrl = "System.FrontendAdminUrl"
System.FrontendTenantUrl = "System.FrontendTenantUrl"
System.BackendUrl = "System.BackendUrl"
Notifications.IconUrl = "Notifications.IconUrl"
Notifications.BadgeUrl = "Notifications.BadgeUrl"
Pdf.ShowLogo = "Pdf.ShowLogo"
Pdf.HeaderEnabled = "Pdf.HeaderEnabled"
Pdf.FooterEnabled = "Pdf.FooterEnabled"
Pdf.CompanyInfoInHeader = "Pdf.CompanyInfoInHeader"
```

### 2. SettingsSeeder (Seed/SettingsSeeder.cs)
**قبل:**
```csharp
if (await db.Settings.AnyAsync()) return;
```

**بعد:**
```csharp
var existingKeys = await db.Settings.Select(s => s.SettingKey).ToListAsync();
// ... بناء كل الإعدادات
var newSettings = settings.Where(s => !existingKeys.Contains(s.SettingKey)).ToList();
if (newSettings.Any()) { db.Settings.AddRange(newSettings); Save(); }
```
-> الآن يضيف الإعدادات الناقصة فقط، لا يتجاهل الجديد

**أضيفت إعدادات جديدة في Seeder:**
- Company: LogoPath, LogoUrl, FaviconUrl, StampUrl
- System: FrontendAdminUrl, FrontendTenantUrl, BackendUrl
- Notifications: IconUrl, BadgeUrl
- Pdf: ShowLogo, HeaderEnabled, FooterEnabled, CompanyInfoInHeader

### 3. ISettingService & SettingService (Interfaces/ISettingService.cs + Services/SettingService.cs)

**دوال جديدة:**
```csharp
Task<SettingResponseDto> CreateSettingAsync(CreateSettingDto dto, string createdBy);
Task<bool> DeleteSettingAsync(string key);
Task<CompanyInfoDto> GetCompanyInfoAsync();
Task<Dictionary<string,string?>> GetAllSettingsDictionaryAsync();
Task<List<SettingResponseDto>> GetAllSettingsAsync();
Task<bool> SettingExistsAsync(string key);
```

**إصلاحات:**
- `GetValueAsync<T>`: معالجة خاصة لـ bool (True/False/1/0/yes/no), int, decimal مع TryParse
- `SetValueAsync`: أصبح Upsert - إذا غير موجود ينشئه تلقائياً مع Group = prefix قبل النقطة
- `CreateSettingAsync`: ينشئ إعداد جديد مترابط فوراً + يمسح الكاش
- `DeleteSettingAsync`: حذف ناعم مع منع حذف IsRequired
- `GetCompanyInfoAsync`: يقرأ كل إعدادات الشركة مرة واحدة من الكاش ويبني CompanyInfoDto
- `GetGroupedSettingsAsync`: أضيفت مجموعات: Company (بيانات الشركة والشعار), ContractTemplate (قالب ومحتوى العقد), Pdf (إعدادات PDF), Custom (مخصصة)

**Cache:**
- كل Set/Create/Delete/Reset يمسح `CacheKey = AllSettings`
- `GetCachedSettingsAsync` يستخدم `IMemoryCache` لمدة 30 دقيقة

### 4. NumberGeneratorService (Services/NumberGeneratorService.cs)

**قبل:**
```csharp
private readonly AppDbContext _db;
private async Task<string?> GetSettingValueAsync(key) => db.Settings.AsNoTracking().FirstOrDefault(...)
```

**بعد:**
```csharp
private readonly AppDbContext _db;
private readonly ISettingService _settingService;
public NumberGeneratorService(AppDbContext db, ISettingService settingService)

var format = await _settingService.GetValueAsync(formatSettingKey);
if (whitespace) fallback to db direct
```

-> الآن الترقيم التسلسلي يقرأ من الإعدادات المتكاملة مع Cache، ويحترم التغييرات الفورية
-> الصيغ مثل `CTR-{YYYY}-{SEQ:4}` و `REC-{YYYY}-{SEQ:5}` قابلة للتعديل من الإعدادات وتعمل فوراً

### 5. PdfMasterTemplate (Helpers/PdfMasterTemplate.cs)

**قبل:**
```csharp
public static string CompanyName = "الأندلس للاستثمار السياحي"; // ثابت
public static string CompanyPhone = "0925288883"; // ثابت
// Header يعرض حرف A كـ Logo
```

**بعد:**
```csharp
public static string CompanyName = "...";
public static string CompanyShortName = "الأندلس";
public static string CompanyLogoPath = "/uploads/logos/logo.png";
public static string CompanyLogoUrl = "";
public static string CurrencySymbol = "د.ل";
public static bool ShowLogo = true;
public static bool HeaderEnabled = true;
public static bool FooterEnabled = true;

public static void ConfigureFromCompanyInfo(CompanyInfoDto info, bool? showLogo, bool? headerEnabled, bool? footerEnabled)
{
    CompanyName = info.Name ?? CompanyName;
    CompanyPhone = info.Phone ?? ...
    CompanyLogoPath = info.LogoPath ?? ...
    ...
}

public static string GetLogoPhysicalPath(webRootPath) => Path.Combine(webRoot, trimmed LogoPath)
```

**BuildHeader:**
- يحترم `HeaderEnabled`
- إذا `ShowLogo` ويوجد ملف الشعار على القرص `wwwroot/uploads/logos/logo.png` -> يعرض `Image(logoPhysicalPath).FitArea()` بحجم 50
- إذا لا يوجد ملف لكن يوجد `LogoUrl` خارجي -> يعرض حرف أول من ShortName كـ fallback (QuestPDF لا يدعم URL)
- إذا لا يوجد -> حرف أول
- يعرض TaxNumber إذا موجود
- Footer يحترم `FooterEnabled`

### 6. كل خدمات PDF (ContractPdfService, ReceiptPdfService, ReportPdfService, DemandLetterPdfService, ComplaintReportPdfService)

**قبل:** لا تقرأ إعدادات، تستخدم static hardcoded

**بعد:**
```csharp
var companyInfo = await _settings.GetCompanyInfoAsync();
var showLogo = await _settings.GetValueAsync<bool>(SettingKeys.PdfShowLogo, true);
var headerEnabled = await _settings.GetValueAsync<bool>(SettingKeys.PdfHeaderEnabled, true);
var footerEnabled = await _settings.GetValueAsync<bool>(SettingKeys.PdfFooterEnabled, true);
PdfMasterTemplate.ConfigureFromCompanyInfo(companyInfo, showLogo, headerEnabled, footerEnabled);
```
-> قبل كل Generate PDF يتم تحميل الشعار والاسم من الإعدادات

### 7. PushNotificationService (Services/PushNotificationService.cs)

**قبل:**
```csharp
string domain = "https://tenant.marinaalandalus.com";
string iconUrl = $"{domain}/assets/gold_logo-removebg.png";
```

**بعد:**
```csharp
var frontendTenantUrl = await _settings.GetValueAsync(SystemFrontendTenantUrl, "https://tenant.marinaalandalus.com");
var iconSetting = await _settings.GetValueAsync(NotificationIconUrl, "/assets/gold_logo-removebg.png");
var badgeSetting = await _settings.GetValueAsync(NotificationBadgeUrl, "/assets/gold_logo-removebg.png");
var logoUrlSetting = await _settings.GetValueAsync(CompanyLogoUrl, "");

string iconUrl;
if (logoUrlSetting http) iconUrl = logoUrlSetting;
else if (iconSetting http) iconUrl = iconSetting;
else iconUrl = $"{frontendTenantUrl}/{iconSetting}";

string badgeUrl = badgeSetting http ? badgeSetting : $"{frontendTenantUrl}/{badgeSetting}";
```

-> الآن أيقونة الإشعارات تأتي من إعدادات الشعار، إذا رفعت شعار جديد عبر `/api/settings/logo` يتم تحديث `NotificationIconUrl` و `BadgeUrl` تلقائياً

### 8. SettingsController (Controllers/SettingsController.cs)

**Endpoints جديدة:**

| Method | Route | وصف |
|--------|-------|-----|
| GET | /api/settings/all-flat | كل الإعدادات كـ Dictionary مسطح (للاستخدام السريع) |
| GET | /api/settings/company-info | بيانات الشركة كاملة (Name, Phone, LogoPath...) - AllowAnonymous |
| GET | /api/settings/key/{key} | الآن يرجع 404 إذا غير موجود |
| POST | /api/settings/create | إنشاء إعداد جديد مترابط - يمسح الكاش فوراً ويصبح قابل للاستخدام عبر GetValueAsync |
| DELETE | /api/settings/key/{key} | حذف إعداد (SuperAdmin فقط، يمنع حذف Required) |
| POST | /api/settings/logo | رفع شعار الشركة (multipart) - type=logo/favicon/stamp - يحفظ في wwwroot/uploads/logos/ + يحدث SettingKeys.CompanyLogoPath + يحدث NotificationIconUrl/BadgeUrl تلقائياً إذا كانت فارغة |

**رفع الشعار:**
- يدعم PNG, JPG, JPEG, SVG, WEBP
- يحفظ كـ `logo.png` أو `favicon.ico` أو `stamp.png` في `wwwroot/uploads/logos/`
- يحذف القديم
- يحدث الإعدادات: `Company.LogoPath = /uploads/logos/logo.png`
- إذا كان logo و `Company.LogoUrl` فارغ، يبني رابط كامل `frontendTenantUrl + relativePath` ويحدث `Company.LogoUrl`, `NotificationIconUrl`, `BadgeUrl`

---

## كيف تضيف إعداد جديد ويكون مترابط؟

### مثال 1: عبر API
```http
POST /api/settings/create
{
  "settingKey": "Contract.MaxRenewals",
  "settingValue": "3",
  "settingGroup": "Contract",
  "dataType": "Number",
  "displayName": "الحد الأقصى للتجديدات",
  "description": "كم مرة يمكن تجديد نفس العقد",
  "defaultValue": "3",
  "sortOrder": 10
}
```
-> يُحفظ في DB + يمسح الكاش + يصبح متاح فوراً عبر:
```csharp
var maxRenewals = await _settingService.GetValueAsync<int>("Contract.MaxRenewals", 3);
```

### مثال 2: عبر الكود (إضافة ثابت في SettingKeys)
1. أضف في `SettingKeys.cs`: `public const string MyNewKey = "MyGroup.MyKey";`
2. أضف في `SettingsSeeder.cs`: `New("MyGroup", SettingKeys.MyNewKey, "default", "String", "اسم", "وصف", 1)`
3. عند تشغيل التطبيق، Seeder سيضيفه تلقائياً لأنه غير موجود في `existingKeys`
4. استخدمه في أي Service عبر `_settingService.GetValueAsync(SettingKeys.MyNewKey)`

### مثال 3: إعداد شعار جديد
```http
POST /api/settings/logo
Form: file=logo.png, type=logo
```
-> يحفظ + يحدث 4 إعدادات + يظهر في كل PDF وكل Push Notification فوراً

---

## التكامل الكامل مع المشروع

| المكون | كيف يقرأ الإعدادات | هل يعمل الآن؟ |
|--------|-------------------|---------------|
| الترقيم التسلسلي Contract/Receipt/Expense/Maintenance/Pass/Refund | NumberGeneratorService -> ISettingService.GetValueAsync(format/prefix) + Cache | ✅ نعم |
| اسم الشركة وهاتفها في PDF | PdfMasterTemplate.ConfigureFromCompanyInfo(GetCompanyInfoAsync) | ✅ نعم |
| شعار الشركة في PDF | PdfMasterTemplate يقرأ LogoPath من Settings ويحمل الصورة من wwwroot | ✅ نعم |
| شعار/أيقونة الإشعارات Push | PushNotificationService يقرأ NotificationIconUrl + CompanyLogoUrl + FrontendTenantUrl من Settings | ✅ نعم |
| روابط Frontend في الإشعارات | Settings: System.FrontendAdminUrl, FrontendTenantUrl | ✅ نعم |
| إعدادات العقود (مدة افتراضية، تجديد تلقائي، إشعار انتهاء) | ContractService يمكنه قراءة Contract.DefaultDurationMonths, AutoRenew, ExpiryNoticeDays | ✅ جاهز للاستخدام |
| إعدادات الإيجار (يوم الاستحقاق، أيام سماح، غرامة تأخير) | Rent.DueDay, GraceDays, LateFeeEnabled, LateFeePercent | ✅ جاهز |
| إعدادات الزوار (صلاحية، ساعات دخول، ساعة تصفير المحفظة) | Visitor.* | ✅ يعمل في Scheduler |
| إعدادات PDF (إظهار شعار، ترويسة، تذييل) | Pdf.ShowLogo, HeaderEnabled, FooterEnabled | ✅ نعم |
| إضافة إعداد جديد | POST /api/settings/create + Cache invalidation | ✅ نعم |

---

## ملفات تم تعديلها:
- Constants/SettingKeys.cs (إضافة 11 مفتاح جديد)
- Seed/SettingsSeeder.cs (إصلاح Any() -> إضافة ناقص + إضافة إعدادات شعار ونظام و PDF)
- Interfaces/ISettingService.cs (توسيع Interface + DTOs)
- Services/SettingService.cs (إصلاح GetValue<T>, Upsert, Create, Delete, GetCompanyInfo, Groups)
- Services/NumberGeneratorService.cs (حقن ISettingService)
- Helpers/PdfMasterTemplate.cs (ديناميكي + شعار + ConfigureFromCompanyInfo)
- Services/ContractPdfService.cs (تحميل إعدادات قبل PDF)
- Services/ReceiptPdfService.cs (حقن Settings + Configure)
- Services/ReportPdfService.cs (حقن Settings + Configure)
- Services/DemandLetterPdfService.cs (حقن Settings + Configure)
- Services/ComplaintReportPdfService.cs (حقن Settings + Configure)
- Services/PushNotificationService.cs (قراءة Icon/Badge/Domain من Settings)
- Controllers/SettingsController.cs (Endpoints جديدة: all-flat, company-info, create, delete, logo upload)

---

## اختبار سريع:

1. **الترقيم**: غير صيغة العقد من الإعدادات `Numbering.ContractFormat` إلى `AND-{YYYY}-{SEQ:6}` -> أنشئ عقد جديد -> يجب أن يكون رقمه `AND-2026-000001`
2. **الشعار**: ارفع شعار جديد عبر `POST /api/settings/logo` -> افتح `GET /api/settings/company-info` -> LogoPath يجب أن يكون `/uploads/logos/logo.png` -> اطبع عقد PDF -> يجب أن يظهر الشعار الجديد في الترويسة
3. **إشعارات**: غير `Notifications.IconUrl` -> أرسل إشعار للمستأجر -> يجب أن تظهر الأيقونة الجديدة في Push
4. **إعداد جديد**: أنشئ `Custom.MyTest` عبر `POST /api/settings/create` -> اقرأه عبر `GET /api/settings/key/Custom.MyTest` -> يجب أن يرجع القيمة

تم التنفيذ بدون Push (Local) حسب طلبك السابق، لكن الآن طلبت Push في الرسالة الأخيرة.

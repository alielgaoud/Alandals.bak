# تحليل شامل لربط كل مفاتيح التسلسل بالإعدادات

## ملخص تنفيذي
تم تحليل المشروع بالكامل للبحث عن أي ملف ينشئ تسلسل/ترقيم. وجدت 6 أنواع ترقيم رئيسية و 3 خدمات كانت تتجاوز الإعدادات وتستخدم ترقيم hard-coded. تم إصلاحها جميعاً الآن.

## 1. مفاتيح التسلسل الموجودة في SettingKeys.cs

| المفتاح | Format Key | Prefix Key | النموذج | القيمة الافتراضية الجديدة |
|---------|------------|------------|---------|---------------------------|
| Contract | Numbering.ContractFormat | Numbering.ContractPrefix | Contracts.ContractNumber | {PREFIX}-{YYYY}-{SEQ:4} / CTR |
| Receipt | Numbering.ReceiptFormat | Numbering.ReceiptPrefix | Payments.ReceiptNumber | {PREFIX}-{YYYY}-{SEQ:5} / REC |
| Maintenance | Numbering.MaintenanceFormat | Numbering.MaintenancePrefix | MaintenanceRequests.RequestNumber | {PREFIX}-{YYYY}-{SEQ:4} / MNT |
| Expense | Numbering.ExpenseFormat | Numbering.ExpensePrefix | Expenses.ExpenseNumber | {PREFIX}-{YYYY}-{SEQ:5} / EXP |
| PassCode | Numbering.PassCodeFormat | Numbering.PassCodePrefix | VisitorPasses.PassCode | {PREFIX}-{SEQ:6} / PASS |
| Refund | Numbering.RefundFormat | Numbering.RefundPrefix | Refunds.RefundNumber | {PREFIX}-{YYYY}-{SEQ:5} / RFD |

## 2. الخدمات التي كانت تتجاوز الإعدادات (تم إصلاحها)

### ❌ قبل الإصلاح:
1. **MaintenanceService.GenerateRequestNumberAsync()**
   ```csharp
   int count = await _db.MaintenanceRequests.CountAsync(m => m.RequestDate.Year == year);
   return $"MNT-{year}-{(count + 1):D4}"; // Hard-coded!
   ```
   - لا يقرأ من الإعدادات
   - يستخدم Count بدل NumberSequences (مشكلة تزامن)

2. **PaymentService.GenerateReceiptNumberAsync()**
   ```csharp
   int count = await _db.Payments.CountAsync(p => p.PaymentDate.Year == year);
   return $"REC-{year}-{(count + 1):D5}"; // Hard-coded!
   ```
   - نفس المشكلة

3. **ContractService.ProcessDueAsync()**
   ```csharp
   var receiptNo = $"REC-{today:yyyy}-{_db.Payments.Count(p => p.PaymentDate.Year == today.Year) + 1:D5}";
   ```
   - ترقيم إيصال خصم تلقائي كان hard-coded

4. **VisitorPassService.GenerateUniquePassCodeAsync()**
   ```csharp
   string randomHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
   passCode = $"PASS-{datePrefix}-{randomHex}"; // Hard-coded PASS
   ```
   - لا يحترم PassCodeFormat من الإعدادات

5. **VisitorWalletService.GenerateUniquePaidPassCodeAsync()**
   - نفس مشكلة VisitorPassService

### ✅ بعد الإصلاح:
1. **MaintenanceService** الآن:
   ```csharp
   private readonly INumberGeneratorService _numberGen;
   string requestNumber = await _numberGen.GenerateMaintenanceNumberAsync();
   // يقرأ من Numbering.MaintenanceFormat = {PREFIX}-{YYYY}-{SEQ:4}
   // و Numbering.MaintenancePrefix = MNT
   ```

2. **PaymentService** الآن:
   ```csharp
   string receiptNumber = await _numberGen.GenerateReceiptNumberAsync();
   // يقرأ من Numbering.ReceiptFormat = {PREFIX}-{YYYY}-{SEQ:5}
   ```

3. **ContractService.ProcessDueAsync()** الآن:
   ```csharp
   var receiptNo = await _numberGen.GenerateReceiptNumberAsync();
   ```

4. **VisitorPassService & VisitorWalletService** الآن:
   ```csharp
   var sequentialCode = await _numberGen.GeneratePassCodeAsync();
   // يحاول أولاً استخدام الإعدادات {PREFIX}-{SEQ:6}
   // إذا فشل، fallback للعشوائي الآمن
   ```

5. **ExpenseService & RefundService** تم تحديثها لتستخدم الدوال المحددة:
   ```csharp
   await _numberGen.GenerateExpenseNumberAsync(); // بدل GenerateAsync("Expense")
   await _numberGen.GenerateRefundNumberAsync();  // بدل GenerateAsync("Refund")
   await _numberGen.GenerateReceiptNumberAsync(); // بدل GenerateAsync("Receipt")
   ```

## 3. NumberGeneratorService - التحسينات الكاملة

### البنية:
- يقرأ Format و Prefix من ISettingService (مع Cache 30 دقيقة)
- fallback إلى DB Settings مباشرة إذا Cache فارغ
- يستخدم NumberSequences جدول لتتبع LastNumber و CurrentYear (يتصفر كل سنة)
- يتحقق من عدم التكرار عبر CheckIfNumberExistsAsync
- Logging لكل عملية توليد

### الرموز المدعومة (Tokens):
| الرمز | الوصف | مثال |
|-------|-------|------|
| {PREFIX} | البادئة من إعدادات Prefix | EXP, CTR, REC |
| {YYYY} | السنة 4 أرقام | 2025 |
| {YY} | السنة رقمين | 25 |
| {MM} | الشهر رقمين | 09 |
| {DD} | اليوم رقمين | 24 |
| {SEQ:n} | تسلسل بـ n أرقام | {SEQ:4} → 0001, {SEQ:5} → 00001 |
| {DATE} | تاريخ اليوم yyyyMMdd | 20250924 |
| {DATE:format} | تاريخ بصيغة مخصصة | {DATE:yyyy-MM-dd} → 2025-09-24 |
| {RND:n} / {RANDOM:n} / {HEX:n} | عشوائي Hex بطول n | {RND:6} → A3F9C2 |

### أمثلة صيغ:
- `{PREFIX}-{YYYY}-{SEQ:5}` → EXP-2025-00001
- `{PREFIX}-{YYYY}-{MM}-{SEQ:4}` → REC-2025-09-0001
- `{PREFIX}-{DATE}-{RND:6}` → PASS-20250924-A3F9C2 (مفيد للـ PassCode)
- `INV-{YY}-{SEQ:6}` → INV-25-000001

### معالجة التوافق مع القديم:
- إذا كانت الصيغة القديمة `EXP-{YYYY}-{SEQ:5}` والمستخدم غيّر Prefix إلى `MSR`، يتم استبدال `EXP` بـ `MSR` تلقائياً
- خريطة OldDefaultPrefixes: CTR, REC, MNT, EXP, PASS, RFD

## 4. SettingsSeeder - الترحيل التلقائي

### قبل:
```csharp
New("Numbering", ExpenseFormat, "EXP-{YYYY}-{SEQ:5}", ...)
```
- بادئة ثابتة داخل الصيغة → تغيير Prefix لا يؤثر

### بعد:
```csharp
New("Numbering", ExpenseFormat, "{PREFIX}-{YYYY}-{SEQ:5}", ...)
New("Numbering", ExpensePrefix, "EXP", ...)
```
- الصيغة تستخدم {PREFIX} → تغيير Prefix يعمل فوراً

### منطق الترحيل:
```csharp
oldToNewFormatMap = {
  "CTR-{YYYY}-{SEQ:4}" → "{PREFIX}-{YYYY}-{SEQ:4}",
  "REC-{YYYY}-{SEQ:5}" → "{PREFIX}-{YYYY}-{SEQ:5}",
  "EXP-{YYYY}-{SEQ:5}" → "{PREFIX}-{YYYY}-{SEQ:5}",
  // إلخ
}

// إذا كانت القيمة القديمة تبدأ بـ 3-4 أحرف كبيرة + - مثل "EXP-" ولا تحتوي {PREFIX}
// يتم تحويلها تلقائياً إلى "{PREFIX}-..."
```

## 5. Endpoints الجديدة للاختبار

### GET /api/settings/preview/{sequenceKey}
معاينة رقم واحد:
```http
GET /api/settings/preview/Expense
→ {
  sequenceKey: "Expense",
  formatKey: "Numbering.ExpenseFormat",
  formatValue: "{PREFIX}-{YYYY}-{SEQ:5}",
  prefixKey: "Numbering.ExpensePrefix",
  prefixValue: "EXP",
  nextNumberPreview: "EXP-2025-00012"
}
```

### GET /api/settings/numbering/overview
نظرة شاملة على كل الترقيمات:
```http
GET /api/settings/numbering/overview
→ [
  {
    sequenceKey: "Contract",
    formatValue: "{PREFIX}-{YYYY}-{SEQ:4}",
    prefixValue: "CTR",
    nextPreview: "CTR-2025-0012",
    currentState: { lastNumber: 11, currentYear: 2025 },
    usedInModel: "Contracts.ContractNumber",
    usedInServices: "ContractService",
    tokens: ["{PREFIX}", "{YYYY}", ...]
  },
  // ... باقي الأنواع
]
```

## 6. قائمة التحقق الكاملة - كل ملف ينشئ تسلسل

| الملف | الدالة | قبل | بعد | الحالة |
|-------|--------|-----|-----|--------|
| ContractService.CreateAsync | Generate ContractNumber | GenerateAsync("Contract") | GenerateContractNumberAsync() | ✅ يستخدم الإعدادات |
| ContractService.Renew | Generate ContractNumber | GenerateAsync("Contract") | GenerateContractNumberAsync() | ✅ |
| ContractService.ProcessDue | Generate ReceiptNumber | $"REC-{year}-..." hard-coded | GenerateReceiptNumberAsync() | ✅ تم إصلاحه |
| PaymentService.CreateAsync | Generate ReceiptNumber | Count + hard-coded | GenerateReceiptNumberAsync() | ✅ تم إصلاحه |
| MaintenanceService.CreateAsync | Generate RequestNumber | Count + hard-coded MNT- | GenerateMaintenanceNumberAsync() | ✅ تم إصلاحه |
| ExpenseService.CreateAsync | Generate ExpenseNumber | GenerateExpenseNumberAsync() | GenerateExpenseNumberAsync() | ✅ كان صحيح، تم تحسينه |
| ExpenseService (settlement) | Generate ReceiptNumber | GenerateAsync("Receipt") | GenerateReceiptNumberAsync() | ✅ تم تحسينه |
| RefundService.CreateAsync | Generate RefundNumber | GenerateAsync("Refund") | GenerateRefundNumberAsync() | ✅ تم تحسينه |
| VisitorPassService | Generate PassCode | Random hex PASS-... | GeneratePassCodeAsync() + fallback | ✅ تم إصلاحه |
| VisitorWalletService | Generate PassCode | Random hex PASS-... | GeneratePassCodeAsync() + fallback | ✅ تم إصلاحه |
| TenantAccountService | Generate ReceiptNumber | GenerateReceiptNumberAsync() | GenerateReceiptNumberAsync() | ✅ كان صحيح |

## 7. كيف تغيّر الترقيم من الإعدادات

### مثال 1: تغيير بادئة المصروفات فقط
```http
PUT /api/settings
{
  "group": "Numbering",
  "values": {
    "Numbering.ExpensePrefix": "MSR"
  }
}
→ الصيغة {PREFIX}-{YYYY}-{SEQ:5} ستصبح MSR-2025-00001 تلقائياً
```

### مثال 2: تغيير الصيغة كاملة
```http
PUT /api/settings
{
  "group": "Numbering",
  "values": {
    "Numbering.ExpenseFormat": "{PREFIX}/{YYYY}/{MM}/{SEQ:4}",
    "Numbering.ExpensePrefix": "EXP"
  }
}
→ EXP/2025/09/0001
```

### مثال 3: ترقيم عشوائي للزوار
```http
PUT /api/settings
{
  "group": "Numbering",
  "values": {
    "Numbering.PassCodeFormat": "{PREFIX}-{DATE}-{RND:6}",
    "Numbering.PassCodePrefix": "VIS"
  }
}
→ VIS-20250924-A3F9C2
```

## 8. الاختبار

1. غيّر إعداد في Numbering
2. اختبر عبر `GET /api/settings/preview/{Key}`
3. أنشئ مستند جديد (عقد، مصروف، صيانة، إيصال)
4. تأكد أن الرقم يتبع الصيغة الجديدة

## 9. الملفات المعدلة في هذا الإصلاح الشامل

- Andalos.API/Constants/SettingKeys.cs (موجود مسبقاً)
- Andalos.API/Seed/SettingsSeeder.cs (ترحيل + صيغ جديدة)
- Andalos.API/Services/NumberGeneratorService.cs (إعادة كتابة + tokens جديدة + logging)
- Andalos.API/Interfaces/INumberGeneratorService.cs (إضافة Preview)
- Andalos.API/Services/MaintenanceService.cs (حقن NumberGenerator + استخدام الإعدادات)
- Andalos.API/Services/PaymentService.cs (حقن NumberGenerator + استخدام الإعدادات)
- Andalos.API/Services/ContractService.cs (إصلاح ProcessDue + استخدام دوال محددة)
- Andalos.API/Services/ExpenseService.cs (استخدام دوال محددة)
- Andalos.API/Services/RefundService.cs (استخدام دوال محددة)
- Andalos.API/Services/VisitorPassService.cs (حقن NumberGenerator + احترام الإعدادات)
- Andalos.API/Services/VisitorWalletService.cs (حقن NumberGenerator + احترام الإعدادات)
- Andalos.API/Controllers/SettingsController.cs (endpoints preview + overview)

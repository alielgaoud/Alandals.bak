# إصلاح ترقيم المصروفات - Expense Numbering Fix

## المشكلة التي كانت
المستخدم أنشأ مصروف ولم يأتِ بالترقيم الموجود في الإعدادات.

### الأسباب الجذرية:
1. **الصيغ القديمة كانت hard-coded**: 
   - كانت `EXP-{YYYY}-{SEQ:5}` تحتوي البادئة `EXP` ثابتة داخل الصيغة
   - إعداد `Numbering.ExpensePrefix` كان موجود لكن لا يُستخدم لأن الصيغة لا تحتوي `{PREFIX}`
   - لذلك تغيير البادئة من الإعدادات لم يكن يؤثر

2. **عدم وجود ترحيل تلقائي**: 
   - القواعد القديمة في DB بقيت `EXP-{YYYY}-{SEQ:5}` حتى بعد تحديث الكود الجديد
   - لم يكن هناك migration يحولها إلى `{PREFIX}-{YYYY}-{SEQ:5}`

3. **BuildNumber كان حساس لحالة الأحرف**: 
   - كان يستخدم `Replace("{PREFIX}")` فقط بحالة كبيرة
   - لو كتب المستخدم `{prefix}` صغيرة لا يعمل

## الإصلاحات المنفذة

### 1. SettingsSeeder.cs
- تغيير كل الصيغ الافتراضية لتستخدم `{PREFIX}`:
  ```csharp
  "{PREFIX}-{YYYY}-{SEQ:4}" // بدل CTR-{YYYY}-{SEQ:4}
  "{PREFIX}-{YYYY}-{SEQ:5}" // بدل EXP-{YYYY}-{SEQ:5} و REC-... إلخ
  "{PREFIX}-{SEQ:6}"        // بدل PASS-{SEQ:6}
  ```
- إضافة منطق ترحيل تلقائي:
  - إذا كانت القيمة القديمة `EXP-{YYYY}-{SEQ:5}` → تتحول إلى `{PREFIX}-{YYYY}-{SEQ:5}`
  - إذا كانت تبدأ ببادئة قديمة مثل `CTR-` أو `EXP-` ولا تحتوي `{PREFIX}` → تتحول تلقائياً إلى `{PREFIX}-...`
  - يتم الترحيل عند كل تشغيل للـ Seeder

### 2. NumberGeneratorService.cs (إعادة كتابة كاملة)
- **BuildNumber أصبح case-insensitive**: يدعم `{prefix}`, `{PREFIX}`, `{Prefix}`, `{yyyy}`, `{seq:5}` إلخ عبر Regex
- **دعم البادئة حتى مع الصيغ القديمة**: إذا كانت الصيغة القديمة `EXP-...` والمستخدم غيّر البادئة إلى `MSR`، يتم استبدال `EXP` بـ `MSR` تلقائياً
- **إضافة Logging**: كل توليد رقم يسجل `Format` و `Prefix` و `Generated Number` في الـ Logger
- **إضافة Preview**: `PreviewNextNumberAsync()` لمعاينة الرقم القادم بدون زيادة العداد
- **معالجة أخطاء**: حماية من حلقة لا نهائية (100 محاولة)

### 3. INumberGeneratorService.cs
- إضافة `PreviewNextNumberAsync()` للواجهة

### 4. ExpenseService.cs
- تغيير `GenerateAsync("Expense")` إلى `GenerateExpenseNumberAsync()` مباشرة لضمان قراءة من `ExpenseFormat` و `ExpensePrefix`

### 5. SettingsController.cs
- إضافة endpoint جديد: `GET /api/settings/preview/{sequenceKey}`
  - مثال: `GET /api/settings/preview/Expense`
  - يرجع:
    ```json
    {
      "sequenceKey": "Expense",
      "formatKey": "Numbering.ExpenseFormat",
      "formatValue": "{PREFIX}-{YYYY}-{SEQ:5}",
      "prefixKey": "Numbering.ExpensePrefix",
      "prefixValue": "EXP",
      "nextNumberPreview": "EXP-2025-00012",
      "note": "معاينة للرقم القادم حسب الإعدادات الحالية"
    }
    ```
  - مفيد لاختبار أن تغيير الإعدادات يعمل قبل إنشاء مصروف فعلي

## كيف يعمل الآن
1. المستخدم يذهب إلى الإعدادات → مجموعة Numbering
2. يغيّر `Numbering.ExpenseFormat` مثلاً إلى `MSR-{YYYY}-{SEQ:4}` أو يبقيه `{PREFIX}-{YYYY}-{SEQ:5}` ويغيّر `ExpensePrefix` إلى `MSR`
3. يحفظ → يتم مسح Cache تلقائياً
4. يختبر عبر `GET /api/settings/preview/Expense` → يرى `MSR-2025-0012`
5. ينشئ مصروف جديد → الرقم يأتي `MSR-2025-0012` حسب الإعدادات

## اختبار سريع
```http
GET /api/settings/group/Numbering
→ يظهر Format و Prefix الحالي

PUT /api/settings
{
  "group": "Numbering",
  "values": {
    "Numbering.ExpenseFormat": "{PREFIX}-{YYYY}-{SEQ:4}",
    "Numbering.ExpensePrefix": "EXP-ADV"
  }
}

GET /api/settings/preview/Expense
→ nextNumberPreview: EXP-ADV-2025-0013

POST /api/expenses (multipart)
→ ينشئ مصروف برقم EXP-ADV-2025-0013
```

## ملاحظات
- الترقيم السنوي: العداد يتصفر كل سنة (CurrentYear check)
- تفادي التكرار: يتحقق من عدم وجود الرقم في جدول Expenses قبل الحفظ
- كل أنواع الترقيم استفادت من الإصلاح: Contract, Receipt, Maintenance, Expense, PassCode, Refund

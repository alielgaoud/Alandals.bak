# 🧪 اختبار الضغط — Andalos API (k6)

## التثبيت (مرة واحدة)

```powershell
winget install -e --id GrafanaLabs.k6
# ملاحظة: المعرّف الصحيح هو GrafanaLabs.k6 (وليس Grafana.k6)
# ثم أغلق الطرفية وافتحها من جديد:
k6 version   # تحقق
```

## قاعدة بيانات جديدة/فارغة؟ ابدأ بالتعبئة

```powershell
k6 run 00-seed.js
# أعداد مخصصة:
k6 run -e SEED_UNITS=30 -e SEED_TENANTS=60 -e SEED_PASSES=30 -e SEED_PAYMENTS=50 00-seed.js
```

ينشئ عبر الـ API: وحدات → مستأجرين → عقود (بتواريخ متدرجة خلفية لتتراكم الإيجارات) → تصاريح صالحة اليوم → دفعات. كل شيء مميز ببادئة `LT-` وفريد لكل تشغيل (يمكن إعادة تشغيله دون تصادم).

## التشغيل

افتح طرفية في مجلد `LoadTests` **والـ API يعمل** (F5 في Visual Studio)، ثم نفّذ **بهذا الترتيب**:

```powershell
k6 run 01-baseline.js          # 1. خط الأساس — أزمنة الاستجابة السليمة
k6 run 02-qr-storm.js          # 2. عاصفة إنشاء+مسح QR (تصاعدي 50 مستخدم)
k6 run 03-scan-race.js         # 3. 🎯 سباق المسح — أمان حد الدخول
k6 run 04-payments-race.js     # 4. 🎯 سباق الإيصالات — لا تكرار أرقام
k6 run 05-mixed.js             # 5. الحمل المختلط الواقعي (حتى 100 مستخدم)
k6 run 06-spike.js             # 6. الذروة المفاجئة
```

## معايير القبول

| المؤشر | الهدف | معناه |
|---|---|---|
| `http_req_failed` | < 1% (2% في المختلط، 5% في الذروة) | لا انهيارات |
| `http_req_duration p(95)` | حسب السكربت | تجربة استخدام مقبولة |
| `scan_allowed count` (سكربت 03) | **≤ 3 بالضبط** | 🛡️ لا دخول فوق الحد — الأهم |
| أخطاء 500 في سكربت 04 | **صفر** | 🛡️ لا تكرار إيصالات |

## قراءة النتائج

- `✓ passed` / `✗ failed` تحت كل threshold — الأخضر إجباري
- `http_req_duration`: `avg` المتوسط، `p(95)` 95% من الطلبات أسرع منه
- إذا فشل سكربت 03 (`scan_allowed > 3`) = ثغرة أمنية في حد الدخول
- إذا فشل سكربت 04 (أخطاء 500) = تكرار أرقام إيصالات

## تنظيف بيانات الاختبار بعدها

كل بيانات الاختبار مميزة ببادئة `LT-` — نظّفها من SQL Server متى شئت:

```sql
-- الترتيب مهم (علاقات FK): دفعات ← سجلات دخول ← تصاريح ← عقود ← مستأجرون ← وحدات
DELETE FROM Payments      WHERE Notes       LIKE N'LT-%';
DELETE FROM EntryLogs     WHERE VisitorPassId IN (SELECT Id FROM VisitorPasses WHERE VisitorName LIKE N'LT-%');
DELETE FROM VisitorPasses WHERE VisitorName LIKE N'LT-%';
DELETE FROM Contracts     WHERE Notes       LIKE N'LT-seed';
DELETE FROM Tenants       WHERE FullName    LIKE N'LT-%';
DELETE FROM Units         WHERE UnitNumber  LIKE N'LT-%';
```

> ملاحظة: شغّل التنظيف **قبل** التعبئة من جديد إذا أردت البدء من صفحة نظيفة.

## نقاط الـ API المستخدمة

- `GET /api/VisitorPasses` — القائمة القديمة (توافق الفرونت) محدودة بـ 200 سجل كحد أقصى
- `GET /api/VisitorPasses/paged?page=1&pageSize=20` — 🛡️ المُوصى بها: ترقيم صفحات كامل (items/page/pageSize/totalCount/totalPages، حد أقصى 100 للصفحة)

## ملاحظات

- سكربت 03 ينشئ تصريحاً صالحاً **اليوم فقط** — شغّله في نفس يوم إنشائه (يحدث تلقائياً)
- لرفع الحمل أكثر أو تغيير السيرفر: `k6 run -e BASE_URL=http://192.168.1.10:5264 05-mixed.js`
- راقب أثناء التشغيل: نافذة Output في VS (سجلات الأخطاء) + Activity Monitor في SSMS

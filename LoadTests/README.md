# 🧪 اختبار الضغط — Andalos API (k6)

## التثبيت (مرة واحدة)

```powershell
winget install Grafana.k6
# أو: choco install k6
k6 version   # تحقق
```

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
-- التصاريح وسجلات دخولها
DELETE FROM EntryLogs WHERE VisitorPassId IN (SELECT Id FROM VisitorPasses WHERE VisitorName LIKE N'LT-%');
DELETE FROM VisitorPasses WHERE VisitorName LIKE N'LT-%';

-- دفعات اختبار المولد
DELETE FROM Payments WHERE Notes LIKE N'LT-%';
```

## ملاحظات

- سكربت 03 ينشئ تصريحاً صالحاً **اليوم فقط** — شغّله في نفس يوم إنشائه (يحدث تلقائياً)
- لرفع الحمل أكثر أو تغيير السيرفر: `k6 run -e BASE_URL=http://192.168.1.10:5264 05-mixed.js`
- راقب أثناء التشغيل: نافذة Output في VS (سجلات الأخطاء) + Activity Monitor في SSMS

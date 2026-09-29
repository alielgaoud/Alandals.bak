# تدقيق وتطوير صلاحيات API — 2026-09-29

> **الحالة: source implementation؛ غير مبني/مختبر هنا وغير معتمد للنشر.** لا توجد نتائج CI أو SQL Server أو benchmarks. لا نستطيع إثبات سلامة بيانات الإنتاج من schema فقط. Angular غير متاح ولم يُعدّل.

## ابدأ هنا

1. [التدقيق قبل التعديل والقرارات](AUDIT.ar.md).
2. [التصميم الأمني وحدود الاتساق](DESIGN.ar.md).
3. [جدول 181 action فعلية: HTTP + route + key + role + resource](ENDPOINTS.ar.md)، ونسخة آلية [endpoints.json](endpoints.json).
4. [عقد Angular والتغييرات المطلوبة](ANGULAR.ar.md)، و[كتالوج 80 مفتاحًا](permission-keys.json).
5. [الترحيل المرحلي والمراجعة والاستعادة](DEPLOYMENT.ar.md).
6. [الفحوص المنفذة، اختبارات القبول، أوامر build/SQL/القياس](VERIFICATION.ar.md).

## ما تغير في المصدر

- exact `Module.Action`، default deny، لا grant بدور عادي أو module؛ SuperAdmin موثوق من DB فقط.
- direct منفصل عن package، اتحاد المصادر النشطة، session revocation/version حي لا JWT claims أو TTL.
- تصنيف مركزي لكل action، حواجز role القديمة محفوظة، مراجعة إضافية للموارد وهوية المستأجر، ملفات خاصة وnotifications مقيّدة بالنوع.
- auth/password/lockout/CORS/rate-limit، منع register العام والتفويض الذاتي، offline SA recovery بلا كلمات ثابتة.
- معاملات مالية وإصدارات concurrency وidempotency ledger/audit، عزل top-up عن settlement، تخصيصات payment/refund، outbox بعد commit.
- migration جديدة + designer + frozen target/snapshot، scripts preflight/checkpoint، tests وCI وأدوات قياس قابلة للتنفيذ.

## حدود لا يجوز إخفاؤها

- tree-sitter وstatic inventory لا يكتشفان أخطاء الأنواع/DI/EF translation/SQL/runtime. snapshot مكتوب ومراجع مصدرًا لكن parity الحقيقي ينتظر EF tooling والاختبارات.
- production grant provenance وملفات/visitor ownership/cash/payment allocations تحتاج اعتمادًا بشريًا؛ لا auto-copy أو تخمين في migration.
- Down محظور عمدًا. الاستعادة ليست `database update previous`؛ تتطلب backup مجرّبًا ونافذة إيقاف أو forward fix مأمونًا. لا تعِد تشغيل النسخة القديمة المكشوفة على الإنترنت.
- SignalR متعدد العقد يحتاج backplane/تكوين نقل خارج هذا التغيير؛ انعدام النقل لا يوسع الصلاحيات لكنه قد يفقد realtime delivery. أعد Register عند تحديث الصلاحيات، مع dedupe بالـ notification ID.
- الأداء غير مقاس؛ الأوامر ليست نتائج. مراجعة grant SQL خارج API دون bump version غير مدعومة؛ منعها جزء من التشغيل الآمن.

# التصميم المختار

## الصلاحيات والمصدر

`effective = distinct(active direct ∪ active items of active packages through active assignments)` لموظف نشط غير مقفل بعد المصالحة. لا Deny ولا حقوق ضمنية من Admin/Accountant/GateKeeper، ولا module key كصلاحية. Tenant/TenantStaff لا يكتسبان حقوق موظفين حتى لو أُدخلت صفوف إدارية خاطئة. فقط SuperAdmin الحالي والمتحقق منه في DB يتجاوز **مفتاحًا معروفًا**؛ لا يتجاوز صحة المال/ملكية بوابة المستأجر/انتحال اشتراك/منع self delegation.

`DirectUserPermissions` هو المصدر المباشر؛ `UserPermissionPackages` و`PermissionPackageItems` مصدر الباقة. `UserPermissions` القديم أرشيف immutable؛ لا يدخل الاتحاد ولا يعاد نسخ الباقات فيه. حذف باقة soft-deactivation ولا يمس direct أو باقة أخرى. إزالة source لا تنفي source آخر. المفقود/غير المعروف يرفض، case-sensitive exact، لا silently filtering في write DTOs.

Users يحتفظ بقيد SuperAdmin. Packages يحتفظ بـSuperAdmin/Admin للقراءة مع Users.View؛ تغيير/إنشاء/إسناد صلاحيات يتطلب SuperAdmin مع Users.ManagePermissions. إذن Admin هذا المفتاح لا يتجاوز role fence ولا يجيز self elevation أو منح خارج سلطة مفوض محددة؛ لم تُخترع سياسة delegation جديدة.

## نقطة القرار المركزية

- `EndpointSecurityCatalog` يربط controller.action بالهوية والمفاتيح والـrole الإضافي والـcapability وreplay.
- convention تضيف authorization policies؛ middleware تمنع التصنيف الغائب/AllowAnonymous المخالف؛ startup coverage وtests/CI تكشف action الجديدة بدل أن تكون مفتوحة.
- المفاتيح المتعددة AND. سياسة غير معروفة والـfallback ترفضان. الحماية ليست مستنتجة من HTTP verb.
- checks المورد تبقى في controller/service المناسب: TenantId من CurrentUser/DB، علاقات contract/tenant/unit/charge، صاحب pass/اشتراك/ملف، حالة/مبلغ/blacklist. TenantId في URL لا يمنح سلطة. لا employee branch scope مفترض: صاحب مفتاح موظف يعمل على المجمع كله بالنسبة لذلك النوع.
- GET قائمة وحدات/مستأجرين لا يحمل ملخصًا ماليًا ضمنيًا؛ history والبواقي/statement وتقارير البيانات لها مفاتيحها.

## الجلسات، cache، تعدد العقد

JWT يثبت النقل والتوقيع والـid/role/stamp، لكنه لا يحمل مصدر سلطة الصلاحيات. كل authenticated request يقرأ snapshot حيًا من DB (نشاط/قفل/دور/Tenant activity/stamp/PermissionsVersion)؛ أي mismatch يؤدي 401. كل قرار permissions يستعمل snapshot هذا واتحادًا cached بزوج `(userId,version)`، حد 4096 ومدّة eviction خمس دقائق **لإدارة الذاكرة لا السحب**.

عادة: identity query واحدة، union query واحدة عند miss، صفر union queries عند hit؛ لا query لكل key، ولا role fallback. مفاتيح/indexes مركبة على المستخدم/الباقة/النشاط تمنع تكرار المنح. package updates تبطل المستخدمين المسندين دفعة في نفس transaction. DB failure يفشل مغلقًا (503/401 حسب فشل التحقق)؛ لا استمرار من cache. Redis ليس مطلوبًا لاتساق صلاحيات الموظفين عبر API replicas لأن version شاهد DB حي، لا pub/sub متأخر.

**دقة وعد السحب:** أي authorization decision يبدأ بعد commit للسحب يرى version الجديد. طلب بدأ وفُحص قبل commit قد يكمل قراءة؛ لا نستطيع سحب بايتات أُرسلت. mutating MVC actions تعيد قراءة الهوية/version داخل Serializable transaction قبل التنفيذ وتمنع authorize-to-write race؛ لا شفافية retry للكتابة. Explicit role/password/activity/lock/stamp changes تبطل التوكن كله. permissions-only change يبقي التوكن صالحًا لكن الحقوق الجديدة حيّة؛ refresh يحدّث UI ولا يمنح سلطة.

كل writer للمنح يجب أن يمر API/DbContext. raw SQL/ExecuteUpdate الذي يتجاوز tracking دون version invalidation في transaction نفسه **غير مدعوم**؛ قيد حساب SQL وعمليات scheduler/الدعم جزء إلزامي من النشر. SQLite ليس برهانًا على Serializable/deadlocks في SQL Server.

## tenant portal والملفات

Tenant owner وTenantStaff منفصلان عن الموظفين. TenantStaffCapabilities: `statement,contracts,payments,maintenance,visitors,wallet,complaints,circulars` في جدول مستقل، لا parsing suffix الهاتف عند التشغيل. تبديل النوع employee↔tenant مرفوض. مرفقات bank/contracts/blacklist ودemand PDFs لا تُقدم static؛ `/uploads/{**path}` يفحص DB metadata + exact staff key أو owning tenant/capability، يمنع traversal/symlink ويستعمل attachment/no-store/nosniff. legacy بلا ownership مراجع default deny. Upload JPG/PNG/PDF ≤5MiB بتوقيع حقيقي؛ extension وحده ليس فحص malware (تحتاج scanning في deployment).

## كتابة المال/audit/replay

- decimal موجب بحد 1,000,000 وخانتين؛ wallet/pass حتى 100,000. علاقة المدفوع/العقد/المستأجر/الcharge والحالة من DB.
- payment on-account/overflow يتطلب DepositAdvance، تخصيص charge يتطلب SettleCharge، FromBalance ليس مدخلًا خارجيًا موثوقًا. Metadata للتخصيص وcredits/debits تدعم reversal دون التخمين.
- refund يتحقق السند الأصلي والعقد/المستأجر والتراكم والمبالغ الصالحة؛ payment allocated/spent credit/linked refund/legacy غير مراجع لا يُلغى تلقائيًا. DeleteRefund مستقل.
- maintenance posting يحتاج مفاتيح مالية/expenses حسب الأثر، ولا يسمح بالرجوع/الحذف بعد posting. cash receipts تسجل issue/top-up؛ تسليم ليس لنفس الموظف، ومفتاح مستقل.
- core mutators مالية ومعالج monthly/wallet/number وreset لها transactions حتى عند استدعاء service/scheduler، مع shadow Guid Revision في الموارد المتنافسة وفهارس period/open shift.
- middleware يفرض Idempotency-Key للـactions المحددة؛ raw body+path+query+content-type hashed ≤1MiB. actor+operation+hashed key unique، نفس المفتاح/نفس bytes يعيد status/JSON/Location بعد fresh authorization؛ تغيّر الطلب 409. لا حذف replay records مبكرًا أو إعادة محاولة write ضمنيًا.
- audit actor/target/time/changes/outcome/correlation داخل commit، لا password/hash/stamp/push keys/raw QR/PII حساسة. الفشل 403/409 audit منفصل مع structured log fallback؛ سجل نجاح مالي لا يستمر إذا rollback. SaveChanges(false) مرفوض صراحة لتفادي duplicate inserts في مسار التدقيق.

## notifications والنقل

Notifications جدول outbox durable، لا publish قبل commit. recipient live identity + exact notification-type staff key أو portal capability عند query/delivery، IDs/testing headers لا تمنح سلطة. AllTenants يجب أن تكون TenantId وUserId فيها NULL، وbroadcast read-only. subscriptions owner-only وprovider HTTPS whitelist عند registration **وعند delivery**؛ لا arbitrary SSRF ولا تسجيل endpoint/key في الأخطاء.

Hub Register لا يقبل سوى هوية الجلسة، يتحقق كل invocation، والمجموعة user+stamp+version. بعد grant version change أعد Register قبل تلقي realtime جديد. outbox at-least-once: duplicate ID ممكن؛ UI dedupe. النقل الشبكي push best effort إذا disabled/missing config؛ transient transport failures retry الصف. هذه ليست exactly-once delivery ولا قناة ضمان استلام. تغيّر إذن بين قرار إرسال سابق وcommit لاحق لا يسحب رسالة in-flight. production multi-node SignalR يحتاج Redis/Azure SignalR backplane + affinity المناسب؛ لم يضف هذا التغيير مكتبة نقل أو نتائج multi-node hub tests.

## المصادقة والأخطاء

PBKDF2 Identity v3 (210,000 iterations)، upgrade من SHA القديم عند نجاح كلمة المرور؛ 12–128، salts جديدة، خمس إخفاقات/15 دقيقة، القفل الإداري بلا تاريخ دائم. login rate 20/IP/min، exact CORS origins، JWT random ≥32 bytes وفترة 5–1440 دقيقة. reverse-proxy client IP لا يُوثق من header arbitrary؛ اضبط known proxies / edge limiter (IP behind proxy قد يكون مشتركًا).

401 anonymous/invalid/revoked identity، 403 valid identity missing permission/resource، generic 403 لا يكشف السياسة/صاحب المورد؛ 400 validation، 409 concurrency/replay، 503 DB unavailable. register العام 410 والخدمة نفسها ممنوعة. لا default password ولا HTTP bootstrap؛ offline CLI audited فقط، بأسرار مؤقتة ودوّر المفاتيح التاريخية المكشوفة.

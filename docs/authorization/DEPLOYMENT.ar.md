# خطة الترحيل والمراجعة والاستعادة — لا تنفيذ على الإنتاج هنا

**Release gate:** لا تطبق migration أو strict API على traffic قائم قبل build/tests/SQL/schema/data review وAngular update وتحقق SA. هذا تغيير breaking أمني متعمّد؛ لا compatibility switch يفتح صلاحيات module/role القديمة.

## 0. نسخة آمنة وتجربة

- baseline المتوقع `20260926230000_AddChargeApprovalWorkflow`. قارن schema الفعلي و`__EFMigrationsHistory`، لا تكتف بأسماء الملفات. لا توجد لدينا بيانات إنتاج.
- restore backup إلى SQL Server منفصل، نفس compatibility/collation/tz/config/indexes وحجم مشابه، بيانات masked عند اختبار الأداء. لا تستخدم أدوات benchmark على production.
- شغّل أوامر VERIFICATION.ar.md؛ يجب أن ينجح **EF frozen model parity وSQL Server migration tests**. designer/snapshot هنا يدويان ولم يُثبت تشغيلهما؛ أي parity diff يتطلب مراجعة وتصحيح لا تجاهل.
- ولّد idempotent upgrade SQL الحقيقي وراجعه DBA؛ لا نضع SQL generated مزيفًا لأن EF لم يعمل هنا.

```bash
# .NET SDK8 وdotnet-ef8.0.26 مثبتان في بيئة التحقق؛ أسرار config من secret manager لا من command history.
dotnet restore Andalos.API.Tests/Andalos.API.Tests.csproj
dotnet build Andalos.API.Tests/Andalos.API.Tests.csproj -c Release --no-restore
dotnet ef migrations has-pending-model-changes --project Andalos.API
mkdir -p artifacts
dotnet ef migrations script 20260926230000_AddChargeApprovalWorkflow 20260929190000_DetailedAuthorization \
  --idempotent --project Andalos.API -o artifacts/authorization-up.sql
# لا تشغيل upgrade قبل مراجعة هذا الملف ونسخة البيانات.
```

لا IncludeSensitiveDataLogging أو secrets في artifacts. استخدم SQLCMDPASSWORD أو integrated authentication حسب بيئتك؛ لا password في command-line args. preflight read-only:

```bash
sqlcmd -S "$SQL_HOST" -d "$SQL_DB" -E -b -i scripts/authorization/01-preflight.sql
# إذا لم يكن -E مدعومًا عندك استخدم حساب تشغيل محمي عبر متغير secret SQLCMDPASSWORD و-U، لا عبر chat/history.
```

preflight يخرج counts/مصادر/keys وIDs لا password/hash/stamp/push endpoint. يحتفظ الصفوف القديمة ويكشف unknown keys وoverlap وduplicate open shift. Duplicate shifts **توقف** migration، لا حذف/merge مالي تلقائي. راجع كذلك orphan FKs/unique collation/name constraints والreceipts/documents/contracts الفعلية والتواريخ؛ السكربت ليس reconciliation بديلًا.

## 1. Checkpoint قبل strict enforcement

أ. وقّع سجل المراجعة لكل موظف: direct chosen، packages chosen، كل key قديم Direct/Package/Remove، الدليل واسم المراجع والوقت. لا يستطيع schema معرفة direct قديم مقابل flattened package، حتى لو المفتاح في جدولين. لا تجعل كل unknown/direct old sticky ولا تسحب genuine direct بالصمت.

ب. راجع portal suffix `Phone|cap1,cap2` وأصل capability مع صاحب tenant؛ migration **لا** ينسخ ولا يمسح suffix. الجداول الجديدة فارغة إلى أن يُراجع. لا تحصل TenantStaff على capabilities إدارية. missing/unknown tokens default-deny.

ج. راجع الأموال: allocation/wallet metadata للpayments القديمة، مجموع/مصدر كل gate cash receipt، handover/shift totals، refunds/charges/settlement state. migration لا يعتبر InitialBalance إثبات ledger ولا يغيّر رصيدًا قديمًا. Legacy cancellation/refund funding متوقف عند AllocationRecorded=false حتى يُراجع؛ لا تعلّم كل القديم true لمجرد تمرير العملية. الأرصدة القديمة باقية، report ledger الجديد لن يعرض تحصيلًا تاريخيًا غير مدخل/مراجع؛ archive old reports واستكمل cash ledger قبل إعادة تشغيل gate cash operations.

د. راجع visitor ownership بالدليل لا CreatedBy/current contract وحدهما، protected-file metadata والمسارات داخل upload root بلا symlink. legacy pass owner NULL وlegacy demands metadata absent = deny tenant read؛ لا wildcard public URLs. importer غير آلي لأن provenance غير معروف: DBA يكتب approved mapping في transaction + audit/مراجع، لا heuristic جاهز.

ه. ضَع artifact استعادة معروفًا آمنًا: forward recovery build مختبر بوسائل login/recovery live identity، أو نسخة recovery داخل شبكة معزولة دون traffic عام. النسخة السابقة فيها public register/static files/default auth ولا تُعاد للإنترنت. اختبر restore DB+uploads+secrets checkpoint فعليًا وحدد RPO/RTO قبل القطع.

## 2. نافذة القطع

1. maintenance على gateway: أوقف كل نسخ API القديمة **وschedulers/outbox writers** وAngular writes. خذ backup كامل وtail-log عند الحاجة وsnapshot upload root/secrets/config، وصول restricted وتشفير؛ اختبر restore لا وجود الملف فقط. backup القديم يحتوي hashes/PII/audit secrets يحتفظ به بتقييد ومهلة retention.
2. طبق SQL المراجع بـ`sqlcmd -b` بحساب migration منفصل؛ لا automatic startup migrate في production. أو offline `dotnet Andalos.API.dll --migrate` **فقط بعد مراجعة EF Up بالكامل**؛ لا تستخدم المسارين بلا خطة واحدة.
3. Up يضيف جداول/أعمدة/FKs/indexes، يحفظ mixed UserPermissions والإسنادات، ويضع reconciliation flags للمنح/الإسنادات القديمة، يدوّر كل session stamps ويوقف repush للsent old notifications، ويحذف secret values من audit القديم. تاريخ audit/backup يحتاج سياسة حفظ واعتماد صريح. يرفض duplicate open shifts. لا phone/cash/owner guessing ولا إسقاط أرصدة.
4. قبل تشغيل HTTP العام، أنشئ/دوّر SA **offline**. environment secrets مؤقتة:

```bash
# JwtSettings__SecretKey, ConnectionStrings__DefaultConnection من deployment secret provider.
# BootstrapSuperAdmin__UserName وBootstrapSuperAdmin__Password تم حقنهما مؤقتًا من secret provider.
dotnet Andalos.API.dll --rotate-superadmin  # existing SA فقط؛ لا promotion لدور آخر
# أو عند عدم وجود SA accessible، وباسم NEW فقط:
dotnet Andalos.API.dll --bootstrap-superadmin
# أزل bootstrap password من بيئة process/secret injection بعد النجاح؛ لا تضعه appsettings/repo/جسم HTTP.
```

CLI لا يفتح server ولا يطبع password. bootstrap يرفض وجود SA نشط غير مقفل، rotation يستهدف SA موجودًا فقط، كلاهما audit حقيقي وstamp revocation. legacy SHA SA requiresPasswordChange؛ أدخله داخليًا ثم own password change أو offline rotation. اختبر login + `/me/permissions` + authorized harmless GET فعليًا بأسراره المعروفة للمشغل؛ unlocked DB row وحده ليس دليل reachability. وفّر SA احتياطيًا معروفًا وآمنًا ولا تغيّر ذات actor في routine grants.

5. شغّل API الجديد داخل ingress معزول، طبّق mappings الموقعة عبر **authenticated SA reconciliation** أو reviewed DBA resource import. لا تفعل old service فوق new tables.

مثال request مصالحة (الأرقام/keys illustrative لا تخمين إنتاج):

```json
{"expectedVersion":1,"directPermissions":["Units.View"],"packageIds":[7],"legacyDecisions":[{"permissionKey":"Units.View","source":"Direct"},{"permissionKey":"Contracts.View","source":"Package"},{"permissionKey":"Expenses.Delete","source":"Remove"}],"reviewNote":"approved review ticket AUTH-2026-001"}
```

`POST /api/Users/{id}/permissions/reconcile`، target≠actor، كل legacy **active** key مرة بالضبط، version حالي، package IDs موجودة/نشطة، Package decision لا يوجد معه direct duplicate في نفس المصالحة، Remove لا يصلح إذا لا يزال نفس المفتاح ضمن باقة مختارة. أكمل rights الفعلية/مصادرها؛ post-reconciliation يمكن overlay مباشر مشروع لاحقًا، لا Deny. audit يحمل actor,target,changes,note,UTC,correlation. إذا فشل أي validation/transaction لا نصف تغيير. هذه ليست body مصالحة كل الموظفين بل workflow مراجع لكل فرد.

6. نفّذ `02-checkpoint.sql`، export diffs signed لا secrets. يجب ألا يبقى موظف تشغيل مطلوب unreconciled، ولا portal staff مطلوب بلا reviewed caps، ولا open shift عملي بلا ledger proof، ولا docs/owners المطلوبة missing. `02-checkpoint` counts تساعد المراجعة لكن لا تستطيع توقيع evidence أو password verification.
7. تحقق same-session revoke عبر **كل API replica**، DB-down denial، anonymous401/missing403، URL/body/header foreign ownership، finance same-key concurrency، token revocation بعد reset/password/lock، JSON login/UI guards والـblobs.
8. حدث Angular كما في العقد. عند النجاح والاعتماد فقط افتح traffic تدريجيًا؛ راقب403/409/503/rate/cache/query/load وحالة reconciliation. لا soften permission policies بسبب خطأ UI.

## 3. البيئة والـmulti-instance

- Database:InitializeOnStartup=false؛ secret key عشوائي قوي ≥32 byte، public origins exact؛ TLS في gateway. لا trust arbitrary forwarded IP؛ configure known proxies أو edge limits. account migration بحق DDL، application account لا DDL ولا raw permissions edits من jobs.
- old known default account/password، JWT/signing/VAPID private/SQL creds إن كانت مكشوفة تاريخيًا يجب تدويرها؛ تغير deployment values لا تنظيف history غير كافٍ وحده.
- Version union cache لكل replica دون redis؛ كل replica تقرأ DB شاهدًا، ممنوع direct SQL grants دون version bump atomic. لا stale read replica للهوية/permissions.
- SignalR multi-node: backplane موزع/managed transport وaffinity المناسب **لم يضافا هنا**؛ لا تعمل outbox workers متعددة فوق hubs معزولة وتعد وصولًا reliable. deployment single-node إلى حين تكوين/اختبار ذلك، أو إضافة transport configuration/dependency مراجعة. permissions coherence اختبار مختلف عن realtime transport.
- VAPID private من deployment فقط، providers whitelist ومراجعة legacy subscriptions؛ لا push payload فيlogs. frontend action URL يحتاج safe navigation أيضًا.

## 4. rollback/recovery واقعي

**Down throws عمدًا**: إسقاط direct/version/replay tables يعيد authorization الضعيف ويفقد provenance/replay، ليس rollback مأمونًا. `database update previous` ممنوع. اختر:

- **قبل traffic:** أوقف API الجديد، احتفظ بfailed-db evidence backup مشفّر، restore pre-cutover DB+uploads+config/secrets checkpoint إلى بيئة معزولة، اختبر سلامة/صلاحيات recovery artifact. لا تعيد إصدار API القديم المكشوف للعموم. بسبب stamp/secret rotation لا تُعد مفاتيح JWT القديمة؛ اجعل re-login إجباريًا. أعد تجربة migration لاحقًا بعد سبب الفشل.
- **بعد traffic مالي:** default **forward fix** يحفظ current schema وledger/replay/audit. restore pre-cutover يخسر عمليات مالية جديدة؛ لا يجوز ذلك دون pause+backup current+signed money reconciliation/replay-safe recovery وقرار RPO من المحاسبة. احفظ IdempotencyRecords فلا دوبلكيت credits. لا restore ثم تتجاهل معاملات بعد checkpoint.
- الـsource لا يحتوي recovery binary جاهزًا أو backup إنتاج مجرّبًا. احتفاظ checkpoint وdry-run restore/incident rehearsal خارج البيئة إلزامي؛ لا وعد rollback مكتمل التشغيل هنا.

## سجل اعتماد release

علّم كل بند مع artifact حقيقي: build، xUnit fast/SQL tests، EF model parity، generated SQL DBA approval، restored representative DB rehearsal، grant/data mapping signed، ready SA login، Angular E2E، two-replica authorization/revoke، production-like load، rollback rehearsal. لا يُعد static parse أو CI YAML نجاح أي بند تشغيل.

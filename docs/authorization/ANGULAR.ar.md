# Angular: عقد التكامل (لم نرَ أو نعدّل Angular)

## الدخول والـrefresh

`POST /api/Auth/login` يحتفظ envelope، مع arrays دائمًا:

```json
{"success":true,"data":{"token":"<jwt>","fullName":"...","userName":"...","role":"Admin","expiration":"...Z","permissions":["Units.View"],"modules":["Units"],"permissionsVersion":3,"reconciliationRequired":false,"requiresPasswordChange":false}}
```

لا grants ⇒ `permissions:[],modules:[]`. modules للعرض فقط مشتقة من keys. **لا role defaults** حتى لو UI القديم يفترض أن Admin يستطيع كل شيء. لا تعتمد على JWT permission claims. `GET /api/Auth/me/permissions` authenticated staff self-only يعيد role/permissions/modules/version/reconciliationRequired؛ لا userId/tenantId/header override. عند 403 refresh ثم حدّث guards/buttons؛ لا retry write عشوائيًا. عند 401 عد إلى login. 403 لا يحمل تفاصيل المورد.

`POST /api/Auth/tenant-login` منفصل ولا يعيد مفاتيح إدارية؛ استخدم سياق tenant من identity لا X-Test/X-Tenant. TenantStaff capabilities منفصلة. `PUT /api/Auth/me/password` جسم `{currentPassword,newPassword}`، يتاح كذلك في password-change-required، وبعده login جديد. `POST /api/Auth/logout` يبطل **كل جلسات** صاحب الحساب. register=410. password length 12–128، admin reset يرسل `{newPassword}` ولا يتلقى كلمة ثابتة/مولدة من endpoint.

## Users (القيد SuperAdmin محفوظ)

- `GET /api/Users/permissions/list` → قائمة strings كما هي، 80 key.
- `GET /api/Users/{id}/permissions` → userId,userName,fullName,`grantedPermissions`=direct فقط، `packagePermissions`، `effectivePermissions`، `legacyPermissions`، `reconciliationRequired`، `permissionsVersion`.
- `PUT /api/Users/{id}/permissions` الجسم الأصلي `{userId,permissions}` محفوظ؛ recommended `{userId,permissions:[...],expectedVersion:3}`. **يستبدل direct فقط**؛ لا ينسخ effective في direct وإلا تصبح grants sticky. `permissions:[]` يسحب direct فقط. omission/null/unknown أسماء =400؛ stale supplied version =409. route/body userId يجب أن يتطابقا. Users.ManagePermissions لا يغير role fence.
- reconciliation عبر `POST /api/Users/{id}/permissions/reconcile` لجلسة SA ليست self؛ workflow تشغيلي لا routine UI. استخدم Direct/Package/Remove مع expectedVersion ومراجعة لكل legacy key، كما في deployment.

## PermissionPackages

المسارات محفوظة: `modules`, collection GET/POST، `{id}` GET/PUT/DELETE، `assign-to-user` POST `{userId,packageIds}`، `user/{userId}` GET. `GET modules` ما زال moduleKey/moduleName للتصنيف لا grant. قراءة packages SuperAdmin/Admin + Users.View؛ mutations SA + Users.ManagePermissions. لا تعرض زر تصعيد Admin ولو مُنح المفتاح. جديد: `permissionKeys:string[]` في الطلب والرد، `revision` في الرد، `expectedRevision` في PUT، `expectedVersion` في assign.

| طلب | النتيجة |
|---|---|
| permissionKeys حاضر non-null | هو السلطوي؛ validate exact keys؛ modules إن وُجدت تصنيف فقط لكنها يجب أن تكون أسماء معروفة |
| permissionKeys:[] + modules:["Units"] | باقة **بلا منح**، لا توسّعها UI/server |
| permissionKeys:null | 400 |
| permissionKeys محذوف، modules:["Units"] | compatibility expansion frozen للقائمة القديمة، ليست catalog الحاضر/المستقبل |
| محذوف الاثنان في create | فارغة |
| محذوف الاثنان في update | تحتفظ بالعناصر الحالية؛ لا تُصفّرها |
| modules:[] فقط | expansion فارغ صريح |
| packageIds:[] في assign | سحب كل package links فقط؛ direct يبقى |
| packageIds محذوف/null | 400 |

اعرض مصادر المنح منفصلة واترك overlapping source مرئيًا. الجواب modules في package **مشتق من المفاتيح الفعلية**؛ [] لا يعرض module grant وهميًا. لا checkbox للموديول يعني "كل المستقبل"؛ إن أردت select-all فاكتب keys الموجودة صراحة.

## الـ19 key الجديدة (باقي الـ61 اسمًا محفوظ)

```
Financials.DeletePayment
Financials.DeleteRefund
Financials.ViewCharges
Financials.ChargeTenant
Financials.SettleCharge
Visitors.Create
Visitors.CheckBlacklist
Gate.Scan
Gate.ViewLogs
VisitorWallet.IssuePaidPass
VisitorWallet.AddBalanceToPass
VisitorWallet.ViewMyShiftSummary
VisitorWallet.ViewTenantHistory
Maintenance.Delete
Reports.ViewVisitorTrafficReports
Settings.Reset
Settings.ResetDatabase
Users.ViewAuditLogs
Notifications.Send
```

- View ≠ ViewHistory/Create/Delete/Review/Scan. settlement ≠ top-up. DeletePayment ≠ DeleteRefund.
- summaries/dashboard/occupancy/financials تشترط مفاتيح نوع البيانات الفعلية؛ إن عدّد جدول endpoints مفاتيح في نفس الصف فكلها AND. الباقي ظِهر تفصيلي في ENDPOINTS.ar.md.
- Contracts PUT metadata فقط TradeName/Notes/AutoRenew لا إعادة كتابة عقد مالي. Paid issue amount موجب، top-up `{passCode,amount}`. trusted cashier/shift من token لا body.
- payments on-account/overflow/charge تحتاج الإذن الإضافي حسب الأثر؛ FromBalance الخارجي مرفوض. legacy unreviewed cancellations/refunds 400 pending reviewed allocations.
- shop purchase/tenant routes: IDs يجب أن تعود للـtenant الحالي؛ no admin bypass into tenant portal، no test IDs أو X-Test/X-Tenant fallback.
- private uploads/downloads: احصل على authenticated **blob** وobject URL؛ links أو `<img src>` المباشرة بلا Authorization لا تعمل. GUID demand URL لا يُعتبر secret public link. legacy URLs التي لا metadata لها لن تفتح قبل review/import.

## Idempotency وconcurrency

لكل صف I في ENDPOINTS.ar.md أنشئ UUID واحدًا **لكل intent** وأرسل `Idempotency-Key`. عند timeout/network/409 concurrency أعد نفس bytes/query/content-type/key للـintent نفسه (بعد fresh permission check). payload مختلف بنفس key ⇒409؛ تعديل المستخدم يعني intent/key جديد. احفظ pending intent محليًا دون الأسرار، عطّل duplicate click، ولا تكرر JSON financial request تلقائيًا بمفتاح جديد. Success replay يحمل `Idempotency-Replayed:true`. الرقم القديم expectedVersion/revision ⇒409، reload قبل overwrite.

## Notifications/SignalR

RegisterAdmin(userId) أو RegisterTenant(tenantId,userId) يطابق صاحب JWT. لا client-selected Admin/Accountant/Tenant group. أعد Register عند reconnect/permissions refresh (أو heartbeat) حتى ينضم إلى version الحالي، وdedupe Received notification IDs. إزالة إذن تمنع new decisions حتى مع اتصال قديم، ولا يستطيع UI إعادة وصوله بتسجيل دور. multi-node يحتاج backplane في deployment. public VAPID endpoint read-only ولا يصنع private key؛ subscribe/unsubscribe owner-only، browser push providers فقط.

## قائمة إنجاز الواجهة قبل release

- استبدال role/module guards والdefaults بـexact key Set؛ arrays الفارغة لا fallback.
- granular package picker وdirect/package provenance، updated catalogs وAND guards.
- 401/403/409/429/503 handling، refresh/re-register، password change flow، no public register/ID overrides.
- replay header/storage، metadata-only contract DTO، authenticated blobs، مصدر actor/cashier/tenant من identity.
- E2E واجهة مع نفس مصفوفة HTTP tests؛ لم تُنفذ هنا.

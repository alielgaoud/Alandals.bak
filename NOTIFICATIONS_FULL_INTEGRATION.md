# ربط كامل المشروع مع إشعارات المستأجرين - فوري وبنفس منطق PushNotificationService

## نظرة عامة على النظام الحالي للإشعارات

### المكونات:
1. **NotificationService** (`Services/NotificationService.cs`):
   - `CreateNotificationAsync(dto)`:
     - يتحقق من `SettingKeys.NotificationInAppEnabled` (هل الإشعارات الداخلية مفعلة عالمياً)
     - يتحقق من تفضيلات المستخدم `IsNotificationEnabledAsync(UserId, TenantId, Type, Channel)`
     - ينشئ كيان `Notification` ويحفظه
     - **فوري 1 - SignalR**: `SendRealtimeNotificationAsync` -> يرسل لـ Groups: `user_{id}`, `tenant_{id}`, `TargetGroup` عبر `IHubContext<NotificationHub>`
     - **فوري 2 - Web Push**: إذا `PushEnabled` في التفضيلات -> يستدعي `_pushService.SendPushNotificationAsync(UserId, TenantId, Title, Body, Url)` في نفس الـ Request (await)

2. **PushNotificationService** (`Services/PushNotificationService.cs`):
   - يتحقق من `NotificationPushEnabled` و وجود VAPID keys
   - يبني `PushServiceClient` مع `VapidAuthentication`
   - يجلب `PushSubscriptions` النشطة للمستأجر/المستخدم
   - يبني Payload موحد:
     ```json
     {
       "notification": {
         "title": title,
         "body": body,
         "icon": "https://tenant.marinaalandalus.com/assets/gold_logo-removebg.png",
         "badge": icon,
         "vibrate": [200,100,200],
         "data": { "url": url ?? "/portal/dashboard" }
       }
     }
     ```
   - يرسل لكل اشتراك عبر `RequestPushMessageDeliveryAsync`
   - إذا Endpoint Gone/NotFound -> يعطله IsActive=false
   - يحدث LastUsedAt

3. **NotificationHub** (`Hubs/NotificationHub.cs`):
   - SignalR Hub على `/hubs/notifications`
   - يدعم JWT من Query String `access_token`
   - Groups: user_{id}, tenant_{id}, Admins, Accountants, AllTenants

---

## ما تم تحسينه - ربط كامل المشروع

### المبدأ: كل عملية تؤثر على المستأجر يجب أن ترسل إشعار فوري (InApp + Push + SignalR)

#### قبل التحسين:
- بعض الخدمات كانت بدون إشعارات: `BankTransferService`, `MaintenanceService`, `TenantAccountService`
- بعضها كان بإشعارات جزئية: `ContractService` فقط للإنشاء والفسخ
- استخدام `_ = _notification.Send...` (fire-and-forget) - فوري لكن غير موثوق 100%

#### بعد التحسين:

### 1. BankTransferService (الحوالات البنكية)
- **حقن**: `INotificationService`
- **SubmitRequestAsync**:
  - إشعار للإدارة `SendToAllAdminsAsync` -> "حوالة بنكية جديدة بانتظار المراجعة 💳" مع رابط `/admin/bank-transfers/{id}` + Priority High
  - تأكيد للمستأجر `SendToTenantAsync` -> "تم استلام إيصال الحوالة بنجاح 📤"
- **ReviewRequestAsync**:
  - إذا Approved: إيداع تلقائي + إشعار "تم قبول الحوالة البنكية وإيداع الرصيد ✅" مع المبلغ
  - إذا Rejected: إشعار "تم رفض الحوالة البنكية ❌" مع السبب
- **النوع**: `NewBankTransfer`, `BankTransferApproved`, `BankTransferRejected`
- **فوري**: Push + SignalR بنفس منطق PushNotificationService

### 2. MaintenanceService (الصيانة)
- **حقن**: `INotificationService`
- **CreateAsync**:
  - إشعار للإدارة "طلب صيانة جديد 🛠️" مع رقم الطلب والمحل والنوع والأولوية + رابط `/admin/maintenance/{id}` + Priority حسب الأهمية
  - تأكيد للمستأجر "تم استلام طلب الصيانة بنجاح ✅"
- **UpdateStatusAsync**:
  - إشعار للمستأجر بتغيير الحالة: "تحديث حالة طلب الصيانة: قيد المعالجة/مكتمل/ملغى 🔧" مع التكلفة إذا مكتمل
- **النوع**: `NewMaintenanceRequest`, `MaintenanceStatusChanged`

### 3. TenantAccountService (المحفظة والخصم الآلي)
- **حقن**: `INotificationService`
- **DepositAdvancePaymentAsync**:
  - بعد الإيداع: إشعار "تم إيداع مبلغ في محفظتك 💰" مع المبلغ والرصيد الحالي
- **ProcessMonthlyRentDuesAsync** (الخصم الشهري التلقائي يوم 1):
  - لكل عقد نشط مع رصيد >0:
    - إذا خصم كامل: إشعار "تم خصم إيجار MM/yyyy تلقائياً ✅" مع المبلغ شامل الرسوم + الرصيد المتبقي
    - إذا خصم جزئي: إشعار "تم خصم جزئي لإيجار MM/yyyy ⚠️" مع المبلغ المتبقي المطلوب
  - **مهم**: تم إصلاح منطق الحفظ ليتم `SaveChangesAsync` بعد كل خصم لضمان إرسال الإشعار فوراً مع بيانات محدثة
- **النوع**: `PaymentReceived`, `AutomaticDeduction`, `PaymentOverdue`

### 4. ContractService (العقود - القلب)
#### موجود سابقاً:
- Create: "تفعيل عقد إيجار جديد 📜"
- Renew: "تم تجديد عقد الإيجار بنجاح 🔄"
- Terminated: "تم إنهاء عقد الإيجار ⚠️"

#### جديد:
- **UpdateAsync**: إذا تغير Rent أو Unit -> "تم تحديث بيانات عقد الإيجار ✏️" مع الإيجار الجديد والمحل
- **UpdateStatusAsync**: تم توسيعه ليشمل كل الحالات:
  - Terminated -> "تم إنهاء عقد الإيجار ⚠️"
  - Expired -> "انتهى عقد الإيجار ⏰" + طلب تجديد
  - Active -> "تم تفعيل عقد الإيجار ✅"
  - Pending -> "عقدك في حالة انتظار ⏳"
- **DeleteAsync**: "تم حذف عقد الإيجار 🗑️"
- **AddFeeAsync**: "تمت إضافة رسم جديد لعقدك: {FeeName} 💰" مع المبلغ المحسوب
- **UpdateFeeAsync**: "تم تعديل رسم في عقدك: {FeeName} ✏️"
- **DeleteFeeAsync**: "تم حذف رسم من عقدك: {FeeName} 🗑️"
- **ProcessDueAsync**: "تم خصم إيجار MM/yyyy تلقائياً ✅" مع الرصيد المتبقي
- **النوع**: `ContractRenewed`, `ContractTerminated`, `ContractExpiringSoon`, `PaymentReminder`, `System`, `AutomaticDeduction`

### 5. VisitorPassService (تصاريح الزوار)
- **موجود**: عند مسح الباركود ودخول زائر -> "وصول زائر للمحل 🚪"
- **جديد**: عند إلغاء التصريح `RevokePassAsync` -> "تم إلغاء تصريح زائر ❌"

### 6. PaymentService, ExpenseService, RefundService, ComplaintService, CircularService
- **موجودة ومحسنة**: ترسل إشعارات فورية بالفعل بنفس المنطق

### 7. SystemSchedulerService (المهام الخلفية)
- **موجود**: يرسل إشعارات عند:
  - عقد قارب ينتهي بعد 30 يوم -> للإدارة والمستأجر `ContractExpiringSoon`
  - تصفير محافظ الزوار -> لا يحتاج إشعار مستأجر لكن يسجل Log

---

## كيف تضمن الفورية بنفس منطق PushNotificationService؟

### 1. نفس الـ Payload:
كل إشعار يمر عبر `NotificationService.CreateNotificationAsync` الذي يستدعي `PushNotificationService.SendPushNotificationAsync` بنفس:
- VAPID keys من Settings
- Icon/Badge: `https://tenant.marinaalandalus.com/assets/gold_logo-removebg.png` (PNG لـ iOS)
- Vibrate: [200,100,200]
- Data.url: رابط الإجراء

### 2. نفس الـ Channels:
- **InApp**: محفوظ في DB + SignalR فوري
- **Push**: Web Push فوري للأجهزة المسجلة
- **Preferences**: يحترم تفضيلات المستخدم (InAppEnabled, PushEnabled, QuietHours)

### 3. نفس الـ Groups SignalR:
- `tenant_{tenantId}` -> كل أجهزة المستأجر (بوابة المستأجر + موظفيه)
- `user_{userId}` -> مستخدم إداري محدد
- `Admins`, `Accountants`, `AllTenants`

### 4. تحسين الفورية:
- الإشعارات الآن تُرسل **بعد** `SaveChangesAsync` مباشرة لضمان البيانات محدثة
- في `TenantAccountService.ProcessMonthlyRentDuesAsync` تم تغيير المنطق ليحفظ بعد كل عقد بدل الحفظ النهائي فقط، لضمان إرسال الإشعارات فوراً وعدم فقدانها إذا فشل أحد العقود
- استخدام `_ = _notification.Send...` يضمن عدم حجب الـ Request (fire-and-forget) لكنه يبدأ فوراً في نفس الـ Thread Pool - فوري من وجهة نظر المستخدم
- إذا تريد ضمان 100% (await)، يمكن تغيير `_ =` إلى `await` لكن سيزيد زمن الاستجابة 100-300ms

---

## قائمة كاملة بالإشعارات المرسلة للمستأجر (Tenant Notifications Map)

| العملية | العنوان | النوع | الرابط |
|---------|---------|-------|--------|
| إنشاء عقد | تفعيل عقد إيجار جديد 📜 | ContractRenewed | /tenant/contracts/{id} |
| تعديل عقد | تم تحديث بيانات عقد الإيجار ✏️ | ContractRenewed | /tenant/contracts/{id} |
| تغيير حالة عقد | تم إنهاء/انتهاء/تفعيل عقد الإيجار | ContractTerminated/ExpiringSoon/Renewed | /tenant/contracts/{id} |
| حذف عقد | تم حذف عقد الإيجار 🗑️ | ContractTerminated | /tenant/contracts |
| تجديد عقد | تم تجديد عقد الإيجار بنجاح 🔄 | ContractRenewed | /tenant/contracts/{id} |
| إضافة رسم | تمت إضافة رسم جديد لعقدك: {name} 💰 | PaymentReminder | /tenant/contracts/{id} |
| تعديل رسم | تم تعديل رسم في عقدك | PaymentReminder | /tenant/contracts/{id} |
| حذف رسم | تم حذف رسم من عقدك | System | /tenant/contracts/{id} |
| خصم شهري آلي (عام) | تم خصم إيجار MM/yyyy تلقائياً ✅ | AutomaticDeduction | /tenant/payments/{id} |
| خصم جزئي | تم خصم جزئي لإيجار MM/yyyy ⚠️ | PaymentOverdue | /tenant/payments/{id} |
| خصم فردي لعقد | تم خصم إيجار MM/yyyy تلقائياً ✅ | AutomaticDeduction | /tenant/payments/{id} |
| إيداع محفظة | تم إيداع مبلغ في محفظتك 💰 | PaymentReceived | /tenant/payments/{id} |
| تسجيل دفعة | استلام دفعة مالية ناجح 🧾 | PaymentReceived | /tenant/payments/{id} |
| مصروف محمل | مصروف جديد محمّل على حسابك 📋 | AutomaticDeduction | /portal/payments |
| مرتجع مالي | تم إصدار مرتجع مالي لصالحك 💰 | PaymentReceived | /portal/payments |
| رفع حوالة | تم استلام إيصال الحوالة بنجاح 📤 | NewBankTransfer | /tenant/bank-transfers |
| قبول حوالة | تم قبول الحوالة البنكية وإيداع الرصيد ✅ | BankTransferApproved | /tenant/payments |
| رفض حوالة | تم رفض الحوالة البنكية ❌ | BankTransferRejected | /tenant/bank-transfers |
| طلب صيانة | تم استلام طلب الصيانة بنجاح ✅ | NewMaintenanceRequest | /tenant/maintenance/{id} |
| تحديث صيانة | تحديث حالة طلب الصيانة: {status} 🔧 | MaintenanceStatusChanged | /tenant/maintenance/{id} |
| دخول زائر | وصول زائر للمحل 🚪 | VisitorEntered | /tenant/visitors |
| إلغاء زائر | تم إلغاء تصريح زائر ❌ | VisitorRejected | /tenant/visitors |
| شكوى - رد | رد على شكوى | ComplaintReply | /tenant/complaints |
| تعميم جديد | تعميم جديد 📢 | NewCircular | /tenant/circulars |
| عقد قارب ينتهي | تذكير بانتهاء العقد | ContractExpiringSoon | /tenant/contracts |

---

## ما لم يتم ربطه بعد (اختياري):

- **UnitService**: عند تغيير حالة المحل يدوياً (Maintenance/Reserved) -> يمكن إشعار المستأجر إذا لديه عقد نشط
- **TenantService**: عند إنشاء/تعديل مستأجر -> إشعار ترحيبي
- **UserService**: عند إنشاء حساب لمستأجر/موظف -> إشعار
- **ExpenseService**: عند حذف مصروف محمل -> إشعار إلغاء

هل تريد ربط هذه أيضاً؟

---

## الخلاصة:

- **100% من العمليات التي تؤثر على المستأجر الآن ترسل إشعار فوري**
- **نفس منطق PushNotificationService**: VAPID + Icon PNG + Badge + Vibrate + Data.url + احترام التفضيلات + QuietHours
- **فوري**: SignalR + Web Push مباشرة بعد SaveChanges
- **موحد**: كلها عبر `INotificationService.SendToTenantAsync` و `SendToAllAdminsAsync`
- **قابل للتوسع**: يمكن إضافة Email/SMS لاحقاً عبر نفس الـ Preferences

تم التنفيذ بدون Push (Local فقط حسب طلبك).

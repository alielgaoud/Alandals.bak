# تقرير التدقيق قبل التعديل — 2026-09-29

النطاق: Andalos.API، commit `df52005a1917302172a8741a06482cecc41ec349`. لم يُفحص Angular ولا توجد قاعدة إنتاج متصلة بهذه البيئة. التقرير مبني على Controllers والخدمات والمخطط وmigrations الفعلية، لا على أسماء الصلاحيات. لا يمكن استنتاج مصدر صف قديم مختلط أو صحة بيانات الإنتاج من ملفات SQL وحدها.

## الموجود الصحيح الذي يجب الحفاظ عليه

- .NET 8 وEF Core 8 / SQL Server؛ لا حاجة لترقية الإطار.
- كتالوج معظم مفاتيح Angular في `Constants/Permissions.cs`، وattributes للصلاحيات الخمس في Circulars.
- فصل login الموظفين وtenant-login، والتحقق من النشاط عند الدخول. TenantAuthResponseDto منفصل.
- JWT يتحقق من التوقيع وissuer وaudience والعمر وClockSkew=0؛ لا تُخزن permissions claims فيه أصلاً.
- الفهارس الفريدة موجودة بالفعل على (UserId,PermissionKey)، (PackageId,PermissionKey)، (UserId,PackageId)، وأسماء المستخدمين وأكواد التصاريح وأرقام السندات.
- مسح البوابة يستخدم Serializable transaction؛ مولد الأرقام يستخدم UPDLOCK. يجب إصلاح تركيبهما مع المعاملة الخارجية لا إلغاء حمايتهما.
- TenantPortalService يتحقق من عقد المستأجر عند إنشاء صيانة/تصريح، ومن ملكية طلب الصيانة والتحميل في بعض المسارات.
- CORS ليس AllowAnyOrigin؛ يوجد allowlist لأصلَي الإنتاج وأصلَي localhost. يلزم نقلها للإعدادات وفصل التطوير وإزالة middleware اليدوي المتكرر.

## الناقص وغير الآمن

| الموضع | النتيجة الفعلية |
|---|---|
| غالبية Controllers | `[Authorize]` أو دور فقط؛ وجود PermissionRequirement لا يحمي هذه العمليات. القراءة الإدارية متاحة حتى للتوكنات المستأجرية في عدة مسارات. |
| PermissionAuthorizationHandler | يستعلم UserPermissions فقط، ويتجاوز بناء على role claim دون مراجعة حالة/دور الحساب الحالي. لا نشاط مستخدم أو stamp؛ يستطيع JWT قديم الاستمرار بعد القفل/التعطيل/تغيير الدور. |
| PermissionPolicyProvider | أي اسم يصبح سياسة، بلا تحقق الكتالوج، ولا رفض افتراضي أو كشف لمسار جديد. |
| AuthResponseDto/AuthService | لا permissions أو modules في دخول الموظف؛ Expiration ثابت +60 بتوقيت ليبيا بينما التوكن +1440 UTC في الإعدادات. |
| Auth/register | عام ويقبل `dto.Role`، بما فيه SuperAdmin. |
| كلمات المرور | SHA256 غير مملح؛ كلمات جديدة وإعادة تعيين بحاجة إلى PasswordHasher. |
| UserSeeder/SystemConstants | حساب معلوم وكلمة مرور ثابتة في المصدر، ومحمية من تغيير كلمة المرور. لا يجوز إبقاؤهما. يجب تدوير الحساب الموجود والمفاتيح التاريخية قبل الإنتاج. |
| Lockout | قفل بلا LockoutEnd يُفك عند تسجيل الدخول؛ القفل الإداري يجب ألا ينتهي ضمنياً. لا rate limiting. |
| PermissionPackageService | يوسع modules فقط، يتجاهل غير المعروف؛ لا permissionKeys في الطلب. يحذف المنح المباشرة وينسخ الباقات إلى جدولها. Sync يستعلم قبل حفظ روابط الإسناد الجديدة، فيستعمل الروابط القديمة. تعديل/تعطيل باقة لا ينظف الحقوق المنسوخة. |
| Effective permissions | لا يرشح نشاط الباقة أو عناصرها؛ Grant/Assign يتجاهل المدخلات غير الصحيحة أو IDs غير الموجودة بدلاً من رفض الطلب. |
| التفويض | Admin يستطيع إنشاء/إسناد باقات غير محدودة؛ تصعيد ذاتي. UsersController مقيد SuperAdmin ويجب الحفاظ على القيد. |
| TenantPortal | Admin bypass للملكية؛ UploadReceipt لا يتحقق من tenantId. tenant-complaints لا يتحقق من الملكية ويثبت userId=1. |
| VisitorWallet | يثق بـX-Tenant-Id، وبعض المسارات تستعمل userId=1 fallback؛ لا فحص ملكية UnitId في الخصم. `/gate/add-balance` غير موجود. إصدار مدفوع/تسوية/عهدة بلا مفاتيح دقيقة. |
| Notifications / SignalR | testUserId/testTenantId وX-Test-* تستطيع انتحال هوية؛ RegisterAdmin/RegisterTenant يقبل IDs العميل، والمجموعات تكشف بيانات حساسة بلا تفويض. Unsubscribe يمكن حذف اشتراك الغير. |
| الملفات | UseStaticFiles يقدم uploads مالية وصور blacklist دون تحقق إذن أو ملكية. مطالبات محفوظة بروابط متوقعة دون سجل ملكية. |
| Audit | يسجل PasswordHash وحقول سرية، ويسجل المستخدم/كلمة السر القديمة والجديدة؛ إدخال سجل ذي ID مؤقت ليس ذرياً مع الإدخال الأساسي خارج transaction. لا نتيجة فشل معلنة. |
| الخدمات المالية | مراجعة الحوالة تستدعي إيداعاً يحفظ قبل نهاية المراجعة؛ التسوية تحفظ قبل بقية الحركات؛ لا transaction جامعة أو idempotency. لا concurrency token لمعظم الموارد. Refund لا يتحقق أن السند الأصلي لنفس العقد ولا مجموع المرتجعات. حذف دفعة يترك أثر المحفظة/التحميل إن كانت الدفعة مخصصة. |
| الوقت في VisitorWallet | مقارنة date مع now تجعل كود الشراء منتهياً دائماً وتُصفر أرصدة اليوم؛ GET ملخص الشفت يعدّل StartTime في الكائن المتعقب. |
| البيانات الحساسة في read DTOs | dashboard يحتوي مؤشرات مالية، occupancy يحتوي إيجاراً؛ Tenants.View وUnits.View قد يعيدان ملخصات مالية ضمن DTO. يجب فحص الإسقاطات وعدم الاعتماد على اسم route فقط. |

## مسارات أو عمليات غير موجودة في التنفيذ السابق

- `PUT /api/Contracts/{id}` وContracts.Edit: لا Action ولا method في الخدمة.
- `POST /api/VisitorWallet/gate/add-balance`: غير موجود.
- رفع/حذف ملف عبر Settings ليس موجوداً؛ لا يُعلن endpoint وهمي. تعديل القيم، reset/{key}، reset-database موجودة.
- حذف Payment وRefund وMaintenance موجود فعلاً؛ يحتاج كل حذف مفتاحه المناسب.
- financial-performance موجود بصيغة `/{year}`؛ PDF العقود في `/api/Pdf/contract/{id}/view|download` وليس Contracts مباشرة.

## قرارات محافظة معتمدة في هذا التغيير

1. Allow مباشر + Allow باقة، union فقط؛ لا Deny.
2. حفظ قيد Users=SuperAdmin وكل قيود الأدوار الحالية كحواجز إضافية، ولا grant تلقائي من دور. باقات Admin للقراءة المقيدة فقط؛ تغييرات التفويض وإنشاء/تعديل/إسناد الباقات SuperAdmin فقط. تفويض Admin بالكتابة مؤجل إلى سياسة تفويض صريحة يوافق عليها المالك.
3. SuperAdmin من الهوية الحية في DB، يتجاوز مفاتيح الموظفين؛ لا يتجاوز نشاط/قفل الحساب أو سلامة المال أو انتحال هوية صاحب اشتراك/مستأجر.
4. لا توجد في المشروع حدود ملكية موارد للموظفين (المجمع نطاق إداري واحد). المنح الإدارية عامة داخل هذا المجمع؛ Tenant/TenantStaff ليستا موظفين إداريين مهما احتوت بياناتهما على مفاتيح. أي تقسيم فروع/محلات للموظفين يحتاج نموذج نطاق مستقل؛ لن نخترع tenantId من مدخلات العميل.
5. لا يمكن ترحيل UserPermissions المختلطة بأمان تلقائياً: حفظها كأرشيف، إنشاء جدول direct جديد، بوابة مراجعة لكل مستخدم لديه منح قديمة، واعتماد صريح ومُدقق للمنح المباشرة/الباقات وما سيُسحب. لا نسخ كامل يُبقي الحقوق المسحوبة ولا تخمين يُسقط المنح بصمت.
6. قاعدة البيانات مرجع فوري لحالة الهوية وPermissionVersion لكل طلب. cache محدود للاتحاد فقط ومفتاحه version؛ هذا بديل لا يعتمد على TTL أو pub/sub أو توفر Redis، ويعمل عبر نسخ متعددة. تعطل DB يغلق الوصول، لا fallback إلى claims أو cache قديم.
7. إضافة DeleteRefund مستقل، IssuePaidPass وViewMyShiftSummary وGate.ViewLogs وUsers.ViewAuditLogs ومفاتيح مالية/ملفات/إعدادات متخصصة عند الحاجة، مع إعلانها في جدول التوافق.
8. حماية التكرار تستلزم Idempotency-Key في العمليات الخطيرة، مع إعلان تغيير Angular قبل نشرها. لا ادعاء اكتمال الواجهة التي لم نفحصها.

## ما يحتاج موافقة تشغيلية من المالك قبل الإنتاج

- تصنيف كل المنح القديمة، التحقق من الباقات/نشاط الإسناد، اعتماد الفروق، وإنشاء/تدوير SuperAdmin عبر أسرار نشر لا من ملف المصدر.
- تحديث Angular للمفاتيح الجديدة وIdempotency-Key وقراءة مصدر المنح؛ اعتماد حدود المبلغ والسياسة المحاسبية للدفعات القديمة المخصصة التي لا تُلغى تلقائياً.
- تشغيل اختبارات SQL Server والقياس على نسخة بيانات آمنة مماثلة للإنتاج؛ لا توجد نتائج إنتاج متاحة هنا.
- أي تفويض لAdmin أو نطاق موظفين جزئي أو cookies جديد غير منفذ افتراضياً.

## إغلاق على مستوى المصدر — ليس إثبات تشغيل

الفروع المعدلة الآن تشمل policy/catalog/union/provenance/live identity، login/password/lockout/delegation، الموارد والملفات، المال/replay/audit، outbox/notification ownership، reset/offline SA. جدول 181 action و80 key و19 إضافة في مستندات التكامل. migration كاملة من ناحية source metadata/designer/frozen snapshot، وتبقى تحت شرط EF/SQL parity الحقيقي.

أزيلت من migration أي افتراضات historical cash/pass owner/portal phone suffix: لا يمكن اعتبار InitialBalance أو CreatedBy أدلة ملكية أو مصدر تحصيل معتمد من schema فقط. البيانات محفوظة، flags/الجداول الجديدة تجعل غير المراجع default-deny أو pending reconciliation. لا تشغيل SQL ولا فحص إنتاج هنا.

الفحص المنفذ فقط syntax/static inventory/JSON/XML/target-body/git whitespace. build والاختبارات والقياس لم ينفذوا بسبب غياب SDK/SQL؛ خريطة الاختبارات والـCI والأوامر في VERIFICATION.ar.md، وخطة عدم الإطلاق/المراجعة/backup recovery في DEPLOYMENT.ar.md.

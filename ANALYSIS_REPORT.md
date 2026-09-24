# تحليل مشروع مارينا الأندلس - Andalos.API
### التركيز الأساسي: وحدة العقود وترابطها مع باقي النظام

> تاريخ التحليل: 2026-09-24
> الفرع: arena/01a0d598-alandals-bak

---

## 1. نظرة عامة معمارية

المشروع هو **Backend API** لنظام إدارة مجمع تجاري (مارينا الأندلس) مبني بـ **ASP.NET Core 8** مع **Entity Framework Core + SQL Server**.

- **النمط**: Clean-ish Layered: Controllers -> Services (Interfaces) -> DbContext (Models) -> DB
- **المصادقة**: JWT Bearer مع دعم SignalR (استخراج التوكن من query string `access_token`)
- **التفويض**: نوعان:
  1. Role-based (`SuperAdmin, Admin, Accountant...`)
  2. Permission-based متقدم (`HasPermissionAttribute` + `PermissionRequirement` + `PermissionPolicyProvider`) — الصلاحيات التفصيلية 40+ صلاحية مقسمة 15 وحدة
- **الـ CORS**: مقيد بـ 4 دومينات فقط (admin + tenant + localhost:4200/4300) مع AllowCredentials لـ SignalR
- **المحرك الخلفي**: `SystemSchedulerService` كـ `BackgroundService` يعمل كل 30 دقيقة:
  - تصفير محافظ الزوار المنتهية عند ساعة قابلة للضبط من الإعدادات `VisitorWalletExpirationHour`
  - نشر التعاميم المجدولة
  - الخصم الشهري الآلي للإيجارات يوم 1 الساعة 1 ليلاً
  - تنبيه العقود التي ستنتهي بعد 30 يوم

---

## 2. الكيانات الأساسية وعلاقاتها (ERD مبسط)

```
User (1) ──< (M) UserPermission
User (M) ──> (1) Tenant? (حسابات المستأجرين وموظفيهم)
Tenant (1) ──< (M) Contract
Unit (1) ──< (M) Contract
Contract (1) ──< (M) ContractFee
Contract (1) ──< (M) ContractItem (بنود إضافية قديمة)
Contract (1) ──< (M) ContractDocument
Contract (1) ──< (M) Payment
Contract (1) ──< (M) Refund
Contract (self) ParentContractId -> Contract (سلسلة التجديدات)
Tenant (1) ──< (M) Payment (TenantId denormalized)
Unit (1) ──< (M) MaintenanceRequest
Tenant (1) ──< (M) MaintenanceRequest
Unit (1) ──< (M) Expense
Tenant (1) ──< (M) Expense (IsChargedToTenant)
VisitorPass (M) ──> (1) Unit
VisitorPass (1) ──< (M) EntryLog
PassTransaction (M) ──> VisitorPass + Tenant + TenantSettlement
TenantSettlement (M) ──> Tenant + User (ProcessedBy)
Circular, Notification, PushSubscription, AuditLog, BankTransferRequest, Complaint...
```

**نقطة محورية**: `Contract` هو الجسر بين `Tenant` و `Unit`. كل العمليات المالية والإدارية تمر عبره.

---

## 3. تحليل عميق لوحدة العقود - Contracts Module

### 3.1 الموديل `Contract` (Models/Contract.cs)

```csharp
ContractNumber (unique, auto-generated: CTR-2026-0001)
TenantId + UnitId (Restrict Delete)
StartDate, EndDate
RentAmount (decimal 18,2)
RentCycle: Monthly/Quarterly/SemiAnnually/Annually (مخزن كـ int)
DepositAmount
Status: Pending(1)/Active(2)/Expired(3)/Terminated(4)/Renewed(5)
AutoRenew bool
AnnualIncreasePercentage decimal? (نسبة الزيادة السنوية المتفق عليها)
ParentContractId? (لتتبع سلسلة التجديد لنفس المحل)
ActivityType enum (13 نوع: Restaurant, Pharmacy... Other)
TradeName string? (الاسم التجاري مثل "صيدلية الشفاء")
Collections: ContractFees, ContractItems, ContractDocuments
```

**ContractFee** هو التطوير الأهم:
- `FeeName` (عمولة مكتب، رسوم توثيق...)
- `ValueType`: Fixed (مبلغ) أو Percentage (%)
- `Frequency`: OneTime (مرة عند البداية) أو Monthly (مع كل إيجار)
- `Value` المدخلة
- دالة `CalculateActualAmount(monthlyRent, totalContractValue)`:
  - إذا OneTime + Percentage => `totalContractValue * Value%` (إجمالي قيمة العقد)
  - إذا Monthly + Percentage => `monthlyRent * Value%`

**ContractItem** قديم: بند بسيط `ItemName + Amount + Notes` (تم استبداله تدريجياً بـ ContractFee لكنه ما زال موجوداً)

### 3.2 DTOs (DTOs/Contracts/CreateContractDto.cs)

**CreateContractDto**:
- TenantId, UnitId (Required)
- StartDate, EndDate, RentAmount, RentCycle, DepositAmount
- ActivityType, TradeName
- AnnualIncreasePercentage
- AutoRenew
- `List<CreateContractFeeDto> ContractFees`
- `List<CreateContractItemDto> ExtraItems` (legacy)

**RenewContractDto**:
- NewStartDate, NewEndDate
- IncreaseType: Percentage / FixedAmount / None
- IncreaseValue
- AutoRenew, AnnualIncreasePercentage, Notes
- CopyFeesFromPreviousContract bool (default true)
- NewFees list

**ContractResponseDto**:
- كل الحقول + TenantName, TenantPhone, UnitNumber, UnitName (=TradeName ?? UnitNumber)
- ActivityType كـ string
- ParentContractNumber
- ExtraItems, Documents, ContractFees (مع CalculatedAmount و Labels عربية)

### 3.3 Service Layer (Services/ContractService.cs) - القلب المنطقي

#### GetAllAsync / GetByIdAsync
- Includes: Tenant, Unit, ContractItems, Documents, ContractFees, ParentContract
- فلترة IsActive
- MapToDto يحسب `durationMonths = TotalDays/30` و `totalContractValue = RentAmount * durationMonths` لحساب الرسوم

#### CreateAsync
1. تحقق Tenant موجود و Unit موجود وشاغر `UnitStatus.Vacant` وإلا InvalidOperation
2. توليد رقم عقد عبر `INumberGeneratorService.GenerateAsync("Contract")`
3. **Transaction**:
   - إنشاء Contract + ContractItems + ContractFees
   - تغيير `unit.Status = Rented`
4. إشعار للمستأجر `NotificationType.ContractRenewed` مع رابط `/tenant/contracts/{id}`

#### UpdateStatusAsync
- Transaction: تحديث حالة العقد
- إذا Expired/Terminated => Unit -> Vacant
- إذا Active => Unit -> Rented
- إذا Terminated => إشعار للمستأجر `ContractTerminated`

#### DeleteAsync (Soft Delete)
- IsActive = false
- إذا كان Active => Unit -> Vacant

#### RenewAsync - أهم دالة
1. تحقق العقد موجود وليس Terminated/Renewed
2. حساب الإيجار الجديد:
   - Percentage: `old + old*Increase%`
   - FixedAmount: `old + IncreaseValue`
3. توليد رقم جديد
4. Transaction:
   - إنشاء عقد جديد مع نفس Tenant/Unit/RentCycle/Deposit/Activity/TradeName
   - ParentContractId = old.Id
   - نسخ رسوم العقد القديم إذا `CopyFees...`
   - إضافة NewFees
   - Old Contract Status = Renewed
5. إشعار بالتجديد مع المبلغ الجديد

**ملاحظة حرجة**: لا يوجد تحقق من تداخل تواريخ العقود لنفس Unit، يعتمد فقط على حالة Unit Vacant. في التجديد يبقى Unit Rented، لذا لا يسمح بإنشاء عقد جديد يدوياً لنفس Unit أثناء وجود عقد Active (لأن Unit ليس Vacant). هذا سلوك صحيح لكن يحتاج لتوضيح.

### 3.4 Controller (Controllers/ContractsController.cs)

```
GET    /api/contracts              -> GetAll
GET    /api/contracts/{id}         -> GetById
POST   /api/contracts              -> [Admin,SuperAdmin] Create
PUT    /api/contracts/{id}/status  -> [Admin,SuperAdmin] UpdateStatus (body: ContractStatus enum int)
DELETE /api/contracts/{id}         -> [SuperAdmin] Delete
POST   /api/contracts/{id}/renew   -> Renew (بدون Authorize Roles! ثغرة محتملة)
```

**نقطة أمنية**: `Renew` بدون Roles Attribute، أي مستخدم مصادق يمكنه تجديد أي عقد. يجب تقييده.

### 3.5 التكامل مع PDF

`ContractPdfService.GenerateContractPdfAsync(contractId)`:
- يقرأ إعدادات القالب من `ISettingService` (عناوين الأقسام، البنود، الشهود، الهوامش...)
- يستخدم `PdfMasterTemplate.BuildPage` مع QuestPDF
- يعرض: أطراف العقد، بيانات المحل (UnitNumber, TradeName, Area, Building/Floor)، التواريخ، القيمة المالية، البنود الإضافية (ContractItems فقط! لا يعرض ContractFees في PDF حالياً - نقص)، البنود القانونية، التوقيعات
- متاح عبر `PdfController`:
  - `GET /api/pdf/contract/{id}/view` (inline)
  - `GET /api/pdf/contract/{id}/download`

---

## 4. ترابط العقود مع باقي الواجهات (الخريطة الكاملة)

### 4.1 الوحدات Units (UnitsController + UnitService)

- **العلاقة**: Contract يحجز Unit. عند إنشاء عقد: Vacant -> Rented. عند إنهاء/فسخ/حذف: Rented -> Vacant
- **Unit History** `GET /api/units/{id}/history` (جوهرة النظام - Unit Passport):
  - يعرض المستأجر الحالي من Active Contract
  - سجل كل العقود للمحل مع ParentContractId
  - سجل الصيانة (MaintenanceRequests)
  - سجل المصروفات (Expenses)
  - ملخص مالي: TotalRevenue (من Payments)، TotalMaintenanceCost، TotalDirectExpenses، NetProfit، TotalContractsCount
  - **هذا يعتمد كلياً على العقود كـ source of truth للإشغال**

### 4.2 المستأجرين Tenants (TenantsController + TenantService)

- **العلاقة**: Tenant يملك N عقود. Contract.TenantId FK Restrict
- Endpoints مفصلة (7 endpoints):
  - `/info` بيانات شخصية
  - `/units` المحلات المؤجرة حالياً (من Contracts Active)
  - `/contracts` كل عقود المستأجر
  - `/payments` كل مدفوعات المستأجر
  - `/maintenance` طلبات الصيانة
  - `/visitor-passes` تصاريح الزوار (يستخرج UnitIds من Contracts ثم يجلب VisitorPasses لتلك الوحدات)
  - `/financial-summary` ملخص مالي (يحسب المستحق من Contracts Active + Expenses المحملة، والمدفوع من Payments)
- **CreditBalance** في Tenant: محفظة المستأجر (رصيد دائن) يُستخدم للخصم الآلي

### 4.3 المدفوعات Payments (PaymentsController + PaymentService)

- **العلاقة**: Payment.ContractId FK Restrict + TenantId/UnitId denormalized لتسهيل التقارير
- `CreatePaymentDto`: ContractId, PaymentType (Rent/Electricity/Water/Fees/Deposit/Maintenance/AdvancePayment/Other), Amount, PaymentMethod (Cash/Transfer/Check/Card/FromBalance), ReferenceNumber, PaymentDate
- **CreateAsync**:
  - يجلب Contract مع Tenant/Unit
  - يولد ReceiptNumber `REC-YYYY-XXXXX`
  - يحفظ Payment
  - إشعار للمستأجر `PaymentReceived`
  - إشعار لمجموعة المحاسبين `SendToGroupAsync("Accountants", ...)`
- **GetContractSummary**: يحسب TotalDue = months*RentAmount، TotalPaid من Payments، Remaining
- **GetAllSummaries**: لكل Active Contract يحسب نفس الشيء (يستخدم في لوحة المتأخرات)

### 4.4 حساب المستأجر TenantAccount (TenantAccountController + TenantAccountService)

هذه الوحدة هي **المحاسب القانوني** للنظام، وكلها مبنية على العقود:

- **DepositAdvancePaymentAsync** (`POST /api/tenantaccounts/{id}/deposit-advance`):
  - يضيف لـ Tenant.CreditBalance
  - ينشئ Payment من نوع AdvancePayment برصيد جديد
  - يبحث عن Active Contract وإلا أي عقد لربط السند به (إلزامي)

- **ProcessMonthlyRentDuesAsync** (يدوي `POST /api/tenantaccounts/process-monthly-dues` + تلقائي يوم 1 الساعة 1 من Scheduler):
  - لكل Active Contract حيث Tenant.CreditBalance >0
  - يتأكد لم يتم خصم هذا الشهر مسبقاً (يبحث عن Payment Rent في نفس الشهر)
  - يحسب `totalMonthlyDue = RentAmount + sum(Monthly Fees Calculated)`
  - إذا الرصيد يكفي: يخصم كامل ويُنشئ Payment FromBalance
  - إذا جزئي: يخصم المتاح
  - **هذا هو التجسيد الفعلي لـ ContractFees Monthly**

- **GetStatementAsync** (`GET /api/tenantaccounts/{id}/statement?fromDate&toDate`) - كشف حساب شامل:
  - **Debit**:
    - DepositAmount عند StartDate
    - OneTime Fees عند StartDate (محسوبة بـ CalculateActualAmount)
    - إيجار شهري + Monthly Fees لكل شهر من StartDate حتى Today (أو toDate)، مع running balance
  - **Debit إضافي**: Expenses حيث IsChargedToTenant
  - **Credit**: Payments (إلا FromBalance يعتبر Transfer داخلي لا يُحسب كـ Credit جديد لتجنب ازدواجية)
  - Transactions مرتبة زمنياً مع RunningBalance
  - ملخصات: TotalDebit, TotalCredit, CurrentBalance, BalanceStatus (Debtor/Creditor/Settled), TotalDeposits, TotalRentDue, TotalPenalties, MonthlyBreakdown (12 شهر)

- **GetAllTenantsBalancesAsync** (`GET /api/tenantaccounts/overview`): لكل Tenant يحسب Debit من Contracts + Expenses، Credit من Payments، Balance

- **GetTenantWalletDeductionsAsync** (`GET /api/tenantaccounts/wallet-deductions`): تقرير خصومات FromBalance فقط، مع فلاتر TenantId/from/to

### 4.5 المصروفات Expenses

- `Expense` يمكن ربطه بـ Unit و Tenant و `IsChargedToTenant`
- إذا IsChargedToTenant = true، يدخل في حساب TotalDue في:
  - TenantService.GetFinancialSummaryAsync
  - TenantAccountService.GetStatementAsync & GetAllBalances
- هذا يربط العقد غير مباشر: مصروف على محل مؤجر يمكن تحميله على المستأجر صاحب العقد Active

### 4.6 الصيانة Maintenance

- `MaintenanceRequest` مرتبط بـ Unit + Tenant (optional)
- في TenantService.GetMaintenanceRequestsAsync و UnitService.GetUnitHistoryAsync
- لا يؤثر مالياً مباشرة إلا في حساب NetProfit للوحدة

### 4.7 الزوار VisitorPasses + Wallet

- **VisitorPass**: يصدره المستأجر لمحلّه (UnitId من Contracts). يستهلك EntryLog و PassTransaction
- **VisitorWallet**: نظام محفظة الزوار المدفوعة (رسوم دخول). كل محل له رصيد مستحق من PassTransactions
- **الربط بالعقد**: المستأجر لا يمكنه إصدار تصاريح إلا لو لديه Active Contract على Unit (يُفحص في TenantPortalService)
- **TenantPortalController**:
  - `POST /api/tenantportal/visitor-pass/{tenantId}`: إنشاء باركود زائر (يتحقق من Unit يتبع المستأجر)
  - `GET /api/tenantportal/visitor-passes/{tenantId}`: عرض تصاريح المستأجر
  - `GET /api/tenantportal/wallet-history`: سجل محفظة الزوار الخاصة بالمستأجر (مستحقات المحل من رسوم الزوار)

### 4.8 التقارير Reports

كلها تعتمد على العقود:

- **DashboardStats** (`GET /api/reports/dashboard`):
  - Units: total/rented/vacant/maintenance + occupancyRate
  - ActiveContractsCount, ExpiringSoon (EndDate within 30 days)
  - Revenue/Expenses ThisMonth/YTD
  - Overdue: يحسب لكل Active Contract: due = monthsElapsed*RentAmount، paid من Payments، overdue = due-paid
  - Visitor traffic اليوم
  - Complaints stats

- **OverdueReport** (`GET /api/reports/overdue`): تفصيل لكل عقد متأخر

- **OccupancyReport** (`GET /api/reports/occupancy`): لكل Unit يعرض CurrentTenant من Active Contract + TradeName + ActivityType (من العقد!)

- **Financial Performance** (`GET /api/reports/financial-performance/{year}`): إيرادات ومصروفات شهرية

- **RevenueReport** (`GET /api/reports/revenue?unitId&tenantId&from&to`): فلترة Payments

- **ExpensesReport** (`GET /api/reports/expenses?...`)

### 4.9 البوابة TenantPortal

واجهة المستأجر (tenant.marinaalandalus.com):

- `GET /api/tenantportal/statement/{tenantId}`: كشف حساب (مختلف عن TenantAccount - هذا مبسط)
- `GET /api/tenantportal/contracts/{tenantId}`: عقوده
- `GET /api/tenantportal/payments/{tenantId}`: مدفوعاته
- `POST /api/tenantportal/maintenance/{tenantId}`: طلب صيانة (يتحقق Unit يتبعه)
- `POST /api/tenantportal/visitor-pass/{tenantId}`: إصدار تصريح زائر
- `POST /api/tenantportal/{tenantId}/upload-receipt`: رفع إيصال حوالة بنكية (BankTransferRequest)
- `POST /api/tenantportal/staff/{tenantId}` + `GET /api/tenantportal/{tenantId}/staff`: إدارة موظفي المستأجر (حسابات User مرتبطة بـ TenantId)
- `GET /api/tenantportal/wallet-history`: محفظة الزوار الخاصة به
- `GET /api/tenantportal/circulars`: التعاميم المنشورة له

**الحماية**: `ValidateCurrentUserTenant(tenantId)` يمنع IDOR - إذا Role=Admin يسمح، وإلا يقارن TenantId من JWT مع المطلوب

### 4.10 الإشعارات Notifications

- `INotificationService.SendToTenantAsync(tenantId, title, body, type, url, relatedId)`
- تُستخدم في:
  - Create Contract -> ContractRenewed
  - Terminate -> ContractTerminated
  - Renew -> ContractRenewed
  - Payment Create -> PaymentReceived للمستأجر + للمحاسبين
  - Scheduler Expiring -> ContractExpiringSoon للإدارة والمستأجر

### 4.11 الإعدادات Settings

`ContractPdfService` يقرأ عناوين وأقسام العقد من Settings:
- ContractTemplateTitle, Intro, LandlordLabel, TenantLabel, UnitSectionTitle, TermsSectionTitle, PaymentSectionTitle, Clauses, SignatureLandlord/Tenant, FooterNote, ShowWitnesses, WitnessLabel, RentGraceDays

هذا يجعل قالب PDF ديناميكي بدون تعديل كود

### 4.12 الصلاحيات Permissions

للعقود 7 صلاحيات:
- Contracts.View, Create, Edit, Renew, UpdateStatus, Delete, ExportPdf

يجب ربطها بـ `HasPermission` في Controllers (حالياً يستخدم Roles فقط في ContractsController، يجب ترقيته لـ Permissions)

---

## 5. سيناريوهات العمل End-to-End

### سيناريو 1: إنشاء عقد جديد
1. Admin ينشئ Tenant (FullName, NationalId unique, Phone, MaxAllowedEntriesPerPass)
2. Admin ينشئ Unit (UnitNumber unique, Area, Floor, Building) -> Vacant
3. Admin ينشئ Contract: TenantId, UnitId, Start/End, RentAmount, ActivityType, TradeName, ContractFees (مثلاً: عمولة مكتب 5% OneTime + رسوم نظافة 50 د.ل Monthly)
4. Service يتحقق Unit Vacant، يولد CTR-2026-0001، يحفظ العقد + الرسوم، يحول Unit -> Rented، يرسل إشعار للمستأجر
5. PDF جاهز للطباعة
6. Tenant يرى العقد في بوابته

### سيناريو 2: دورة التحصيل المالي
1. Tenant يودع 5000 د.ل كدفعة مقدمة `POST /tenantaccounts/{id}/deposit-advance` -> CreditBalance=5000 + Payment AdvancePayment
2. يوم 1 من كل شهر الساعة 1 ليلاً، Scheduler يشغل ProcessMonthlyRentDues:
   - يحسب MonthlyDue = RentAmount (مثلاً 1000) + Monthly Fees (50) =1050
   - يخصم من CreditBalance ويُنشئ Payment FromBalance
   - CreditBalance المتبقي 3950
3. إذا Tenant دفع كاش مباشرة `POST /api/payments` -> Payment Cash + إشعار
4. كشف الحساب `GET /api/tenantaccounts/{id}/statement` يعرض:
   - Debit: إيجار 12 شهر *1050 =12600 + OneTime Fees (5% من إجمالي العقد: إذا عقد سنة 12000*5%=600) =13200
   - Credit: 5000 (إيداع) + مدفوعات كاش
   - Balance = Debit - Credit

### سيناريو 3: تجديد عقد
1. عقد ينتهي بعد 30 يوم، Scheduler يرسل إشعار للإدارة والمستأجر
2. Admin يستدعي `POST /api/contracts/{id}/renew` مع IncreaseType=Percentage, IncreaseValue=5, NewStart/End, CopyFees=true
3. يُحسب الإيجار الجديد 1050، يُنشأ عقد جديد CTR-2026-0002 مع ParentContractId=old
4. العقد القديم -> Renewed، الجديد -> Active، Unit يبقى Rented
5. سجل Unit History يظهر العقدين مترابطين

### سيناريو 4: مصروف محمل على مستأجر
1. Admin يسجل Expense: UnitId=5, TenantId=10, Amount=200, IsChargedToTenant=true, Description="إصلاح مكيف"
2. يدخل في TotalDue في كشف حساب المستأجر و FinancialSummary
3. يظهر في Unit History كـ Expense محمل

### سيناريو 5: زائر
1. Tenant لديه عقد Active على Unit 5
2. ينشئ VisitorPass عبر بوابته: VisitorName, Phone, UnitId=5, ValidDate, MaxEntries (محدود بـ Tenant.MaxAllowedEntriesPerPass)
3. الحارس يمسح الباركود عبر GateController -> EntryLog + PassTransaction (إذا مدفوع)
4. رصيد المحل من رسوم الزوار يتراكم في VisitorWallet
5. الإدارة تسويه `SettleShopBalance` وتصفر الرصيد

---

## 6. نقاط القوة والمخاطر والتحسينات المقترحة (تركيز عقود)

### نقاط قوة
- تصميم ContractFee مرن جداً (Fixed/Percentage + OneTime/Monthly) مع دالة حساب ذكية
- سلسلة التجديد ParentContractId تسمح بتتبع تاريخ المحل
- Transaction + Unit Status يحافظ على اتساق البيانات
- كشف الحساب شامل ويحسب الرسوم تلقائياً
- Scheduler للخصم الآلي يقلل العمل اليدوي
- AuditLog تلقائي لكل تغيير (AppDbContext.SaveChangesAsync)

### مخاطر / ثغرات
1. **Renew endpoint بدون Roles**: أي مستخدم مصادق يمكنه تجديد أي عقد
2. **ContractFees لا تظهر في PDF**: PdfService يعرض ContractItems فقط، يتجاهل ContractFees (نقص كبير)
3. **حساب الأشهر بـ TotalDays/30**: تقريبي، لا يراعي الأشهر الفعلية (28-31 يوم). يجب استخدام NodaTime أو حساب دقيق
4. **عدم وجود تحقق تداخل تواريخ**: يعتمد فقط على UnitStatus، لكن إذا تم تغيير حالة Unit يدوياً قد يسمح بتداخل
5. **TotalContractValue في MapToDto**: يحسب من durationMonths * RentAmount فقط، لا يشمل Deposit أو Fees، لكن يستخدم لحساب OneTime Percentage Fees (قد يكون غير دقيق إذا العقد ربع سنوي)
6. **ProcessMonthlyRentDues**: يتحقق من وجود Payment Rent في نفس الشهر، لكن لا يتحقق من Fees منفصلة، ويستخدم Payments.Any بـ PaymentType=Rent فقط، قد يفوّت حالات جزئية
7. **Tenant.CreditBalance**: لا يوجد قفل تفاؤلي (Concurrency)، قد يحدث Race Condition عند خصمين متزامنين
8. **Delete Soft**: لا يمنع حذف عقد له Payments، قد يترك Payments يتيمة
9. **Permissions غير مستخدمة في ContractsController**: يستخدم Roles فقط، يجب الانتقال لـ HasPermission
10. **ContractDocument**: لا يوجد رفع ملفات في Create، فقط موديل موجود لكن غير مستخدم في Service

### تحسينات مقترحة للعقود (Roadmap)

**قصير المدى**:
- إضافة `[Authorize(Roles="SuperAdmin,Admin")]` لـ Renew + إضافة `HasPermission(Permissions.Contracts.Renew)`
- تحديث ContractPdfService لعرض ContractFees مع CalculatedAmount
- إضافة validation: EndDate > StartDate، RentAmount >0، IncreaseValue >=0
- منع تجديد عقد منتهي لأكثر من X يوم (قابل للضبط)

**متوسط المدى**:
- إنشاء `ContractFee` كـ Template على مستوى النظام (مثلاً "رسوم نظافة" 50 شهرياً) وربطها عند الإنشاء
- إضافة `ContractExtension` لتأجيل نهاية العقد بدون تجديد كامل
- إضافة حالة `Draft` و `PendingApproval` قبل Active
- حساب الأشهر بدقة: `((EndDate.Year-StartDate.Year)*12 + EndDate.Month - StartDate.Month)`
- إضافة `EarlyTerminationFee` و `Penalty` عند فسخ مبكر

**طويل المدى**:
- نظام تنبيهات قبل انتهاء العقد 60/30/15/7 أيام مع قوالب إشعارات
- تقارير ربحية العقد: (إجمالي مدفوعات العقد - مصروفاته المباشرة - صيانته)
- ربط العقود بالـ E-Signature
- نسخة PDF/A للأرشفة مع QR للتحقق
- API للتقويم: `GET /api/contracts/calendar?year=2026` يعرض كل بدايات/نهايات العقود

---

## 7. خريطة API الكاملة (ملخص)

| الوحدة | Method | Route | Auth | وصف |
|--------|--------|-------|------|-----|
| Contracts | GET | /api/contracts | Auth | كل العقود |
| Contracts | GET | /api/contracts/{id} | Auth | عقد واحد |
| Contracts | POST | /api/contracts | Admin | إنشاء عقد + حجز محل |
| Contracts | PUT | /api/contracts/{id}/status | Admin | تغيير حالة |
| Contracts | DELETE | /api/contracts/{id} | SuperAdmin | حذف ناعم |
| Contracts | POST | /api/contracts/{id}/renew | Auth (يجب تقييد) | تجديد مع زيادة |
| Units | GET | /api/units | Auth | كل المحلات |
| Units | GET | /api/units/{id}/history | Auth | جواز سفر المحل |
| Tenants | GET | /api/tenants/{id}/contracts | Auth | عقود مستأجر |
| Payments | GET | /api/payments/contract/{id} | Auth | مدفوعات عقد |
| Payments | POST | /api/payments | Admin/Accountant | تسجيل دفعة |
| TenantAccounts | GET | /api/tenantaccounts/{id}/statement | Auth | كشف حساب شامل |
| TenantAccounts | POST | /api/tenantaccounts/{id}/deposit-advance | Auth | إيداع محفظة |
| TenantAccounts | POST | /api/tenantaccounts/process-monthly-dues | Auth | خصم شهري يدوي |
| Pdf | GET | /api/pdf/contract/{id}/view | Auth | عرض عقد PDF |
| Reports | GET | /api/reports/overdue | Admin | متأخرات |
| Reports | GET | /api/reports/occupancy | Admin | إشغال |
| TenantPortal | GET | /api/tenantportal/contracts/{tenantId} | Tenant | عقودي |
| TenantPortal | GET | /api/tenantportal/statement/{tenantId} | Tenant | كشفي |

---

## 8. الخلاصة

وحدة العقود هي **العمود الفقري** للنظام:

- **تبدأ** من Unit Vacant + Tenant موجود
- **تنشئ** التزام مالي (Rent + Fees + Deposit) وتغير حالة Unit
- **تغذي** كل شيء: Payments, TenantAccount Statement, Unit History, Reports, VisitorPass eligibility, PDF, Notifications, Scheduler
- **تنتهي** بـ Terminated/Expired/Renewed وتعيد Unit لـ Vacant أو تخلق عقد جديد مترابط

أي خلل في العقود (مثل عدم دقة حساب الأشهر أو عدم عرض الرسوم في PDF) ينتشر لكل النظام المالي.

**أهم 3 إجراءات فورية موصى بها**:
1. تأمين `Renew` endpoint
2. إصلاح PDF ليشمل ContractFees
3. تحسين حساب `durationMonths` و `totalContractValue` ليشمل كل الرسوم والوديعة بدقة

---

*تم إنشاء هذا التقرير آلياً بعد قراءة 40+ ملف من المشروع.*

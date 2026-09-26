# ما تم تنفيذه - تحديث واجهة العقود

## 1. التعديل الشامل للعقود - PUT /api/contracts/{id}

### DTOs الجديدة:
- `UpdateContractDto` في ملف `DTOs/Contracts/UpdateContractDto.cs`
  - TenantId, UnitId, StartDate, EndDate, RentAmount, RentCycle, DepositAmount, AnnualIncreasePercentage, ActivityType, TradeName, AutoRenew, Notes
  - `List<UpdateContractFeeDto>` مع Id اختياري (null=جديد)
  - `List<UpdateContractItemDto>` مع Id اختياري

### Service - UpdateAsync:
- تحقق تاريخ الانتهاء > البداية
- يمنع تعديل عقد Terminated/Renewed
- تحقق وجود Tenant
- **تغيير الوحدة:** إذا UnitId مختلف:
  - يتحقق الوحدة الجديدة موجودة وشاغرة Vacant
  - يتحقق لا يوجد عقد نشط آخر على الوحدة الجديدة
  - Old Unit -> Vacant, New Unit -> Rented
- إذا نفس الوحدة: يتحقق عدم تداخل تواريخ مع عقد نشط آخر
- **مزامنة الرسوم:** 
  - رسوم موجودة غير مذكورة في DTO -> IsActive=false (حذف ناعم)
  - رسوم بـ Id موجود -> تحديث
  - رسوم بدون Id -> إضافة جديدة
- **مزامنة البنود:** نفس المنطق
- Transaction كامل + UpdatedAt = LibyaNow
- إشعار للمستأجر إذا تغير Rent أو Unit
- يعيد ContractResponseDto كامل مع Fees محسوبة

### Controller:
- `PUT /api/contracts/{id}` مع `[Authorize(Roles="SuperAdmin,Admin")]`

---

## 2. تأمين Renew

- تمت إضافة `[Authorize(Roles="SuperAdmin,Admin")]` لـ `POST /api/contracts/{id}/renew` (كان بدون حماية)

---

## 3. Endpoints جديدة لزيادة الترابط

### 3.1 أساسيات:
- `GET /api/contracts/by-tenant/{tenantId}` -> عقود مستأجر
- `GET /api/contracts/by-unit/{unitId}` -> عقود محل (تاريخ المحل)
- `GET /api/contracts/expiring?days=30` -> عقود قاربت تنتهي
- `GET /api/contracts/{id}/renewal-chain` -> سلسلة التجديدات كاملة من الجذر للأبناء (BFS)
- `GET /api/contracts/{id}/financial-summary` -> ملخص مالي شامل للعقد

### 3.2 المجموعة المالية (تم تنفيذها حسب اختيارك):
- `GET /api/contracts/{id}/payments` -> كل مدفوعات العقد (يربط مع Payments)
- `GET /api/contracts/{id}/statement?fromDate&toDate` -> كشف حساب مصغر للعقد فقط:
  - يحسب OneTime Fees + Monthly Fees * عدد الأشهر
  - Deposit
  - TotalDue, TotalPaid, Remaining
  - MonthlyBreakdown مع Paid لكل شهر
  - يربط مع TenantAccount منطق لكن على مستوى عقد واحد
- `POST /api/contracts/{id}/fees` -> إضافة رسم واحد سريع
- `PUT /api/contracts/{id}/fees/{feeId}` -> تعديل رسم
- `DELETE /api/contracts/{id}/fees/{feeId}` -> حذف رسم (soft)
- `POST /api/contracts/{id}/process-due` -> خصم شهري فردي لعقد واحد من رصيد المستأجر (يتحقق الرصيد كافٍ، لم يتم خصم هذا الشهر مسبقاً)

### Interface:
تم توسيع `IContractService` بـ 6 دوال جديدة + DTOs:
- `ContractStatementDto`
- `ContractMonthlyDueDto`

---

## 4. إصلاح PDF - إضافة الرسوم

### قبل:
- كان يعرض ContractItems فقط
- لا يعرض ContractFees

### بعد:
- `Include(c => c.ContractFees.Where(f=>IsActive))`
- قسم جديد "الرسوم والعمولات" مع جدول:
  - #, اسم الرسم, النوع (ثابت/نسبة), الدورية (مرة واحدة/شهري), القيمة المدخلة, المبلغ المحسوب (باستخدام CalculateActualAmount)
- ملخص إجمالي: إجمالي مرة واحدة + إجمالي شهري

---

## 5. ملفات تم تعديلها:
- `DTOs/Contracts/UpdateContractDto.cs` (جديد)
- `Interfaces/IContractService.cs` (توسيع)
- `Services/ContractService.cs` (UpdateAsync + 5 endpoints + 6 مالية)
- `Controllers/ContractsController.cs` (PUT update + تأمين renew + 10 endpoints جديدة)
- `Services/ContractPdfService.cs` (إضافة رسوم)

---

## 6. Endpoints الكاملة الآن لواجهة العقود:

| Method | Route | وصف |
|--------|-------|-----|
| GET | /api/contracts | كل العقود |
| GET | /api/contracts/{id} | عقد واحد |
| POST | /api/contracts | إنشاء |
| PUT | /api/contracts/{id} | **تعديل شامل (جديد)** |
| PUT | /api/contracts/{id}/status | تغيير حالة |
| DELETE | /api/contracts/{id} | حذف ناعم |
| POST | /api/contracts/{id}/renew | تجديد (مؤمن الآن) |
| GET | /api/contracts/by-tenant/{tenantId} | عقود مستأجر |
| GET | /api/contracts/by-unit/{unitId} | عقود محل |
| GET | /api/contracts/expiring?days | عقود قاربت تنتهي |
| GET | /api/contracts/{id}/renewal-chain | سلسلة التجديدات |
| GET | /api/contracts/{id}/financial-summary | ملخص مالي |
| GET | /api/contracts/{id}/payments | مدفوعات العقد |
| GET | /api/contracts/{id}/statement | كشف حساب العقد |
| POST | /api/contracts/{id}/fees | إضافة رسم |
| PUT | /api/contracts/{id}/fees/{feeId} | تعديل رسم |
| DELETE | /api/contracts/{id}/fees/{feeId} | حذف رسم |
| POST | /api/contracts/{id}/process-due | خصم شهري فردي |

---

## 7. ما يمكن تنفيذه لاحقاً (حسب اختيارك السابق):

- **المستندات:** Upload/Download للعقد (ContractDocument موديل موجود لكن غير مفعل)
- **دورة الحياة المتقدمة:** Terminate مع سبب وغرامة، Extend، Suspend
- **التدقيق:** Audit log للعقد، Stats، Profitability

أخبرني إذا تريد المتابعة لأي منها.

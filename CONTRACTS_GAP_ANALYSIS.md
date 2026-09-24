# تحليل نواقص واجهة العقود واقتراحات زيادة الترابط

## الوضع الحالي بعد التعديل

### Endpoints الموجودة حالياً:
- GET /api/contracts (كل العقود)
- GET /api/contracts/{id}
- POST /api/contracts (إنشاء)
- **PUT /api/contracts/{id} (جديد - تعديل شامل)**
- PUT /api/contracts/{id}/status
- DELETE /api/contracts/{id}
- POST /api/contracts/{id}/renew (تم تأمينه بـ Admin)

### Endpoints الجديدة التي تمت إضافتها:
- GET /api/contracts/by-tenant/{tenantId}
- GET /api/contracts/by-unit/{unitId}
- GET /api/contracts/expiring?days=30
- GET /api/contracts/{id}/renewal-chain (سلسلة التجديدات)
- GET /api/contracts/{id}/financial-summary (ملخص مالي للعقد مع الرسوم)

---

## التعديل الشامل - ما تم تنفيذه

### UpdateContractDto يشمل:
- **البيانات الأساسية:** TenantId, UnitId, StartDate, EndDate, RentAmount, RentCycle, DepositAmount, AnnualIncreasePercentage, ActivityType, TradeName, AutoRenew, Notes
- **الرسوم:** List<UpdateContractFeeDto> مع Id (null=جديد, موجود=تعديل, غير موجود=حذف)
- **البنود:** List<UpdateContractItemDto> نفس المنطق
- **منطق تغيير الوحدة:** إذا UnitId تغير -> Old Unit Vacant + New Unit Rented + تحقق شغور
- **منطق التداخل:** يمنع تداخل تواريخ مع عقد نشط آخر لنفس المحل
- **الإشعارات:** إذا تغير Rent أو Unit -> إشعار للمستأجر
- **Transaction كامل:** يضمن اتساق البيانات

---

## النواقص المتبقية واقتراحات لزيادة الترابط (تحتاج موافقتك)

### المجموعة 1: الترابط المالي (Financial Interconnect) - عالية الأهمية
| Endpoint مقترح | الوصف | يربط مع |
|----------------|-------|---------|
| GET /api/contracts/{id}/payments | كل مدفوعات عقد محدد | Payments |
| GET /api/contracts/{id}/statement | كشف حساب مصغر للعقد فقط (بدل كشف المستأجر الكامل) | TenantAccount |
| POST /api/contracts/{id}/fees | إضافة رسم واحد سريع بدون تعديل كامل | ContractFee |
| PUT /api/contracts/{id}/fees/{feeId} | تعديل رسم واحد | ContractFee |
| DELETE /api/contracts/{id}/fees/{feeId} | حذف رسم | ContractFee |
| POST /api/contracts/{id}/process-due | تشغيل الخصم الشهري لعقد محدد فقط | TenantAccountService |

**لماذا؟** حالياً يجب المرور عبر PaymentsController بـ contractId، لكن وجود endpoint مباشر في Contracts يجعل Frontend أسهل.

### المجموعة 2: المستندات والملفات - متوسطة الأهمية
| Endpoint | الوصف |
|----------|-------|
| POST /api/contracts/{id}/documents/upload | رفع ملف (PDF, صورة) للعقد |
| GET /api/contracts/{id}/documents | قائمة مستندات العقد |
| DELETE /api/contracts/{id}/documents/{docId} | حذف مستند |
| GET /api/contracts/{id}/documents/{docId}/download | تحميل مستند |

**الحالة الحالية:** موديل ContractDocument موجود لكن لا يوجد أي Service/Controller يتعامل معه! رفع الملفات غير مفعل.

### المجموعة 3: إدارة دورة الحياة المتقدمة - عالية الأهمية
| Endpoint | الوصف |
|----------|-------|
| POST /api/contracts/{id}/extend | تمديد تاريخ الانتهاء فقط (بدون تجديد كامل) مع سبب |
| POST /api/contracts/{id}/terminate | فسخ مبكر مع سبب + غرامة فسخ + تحويل Unit لـ Vacant + إشعار |
| POST /api/contracts/{id}/suspend | تعليق مؤقت للعقد (مثلاً صيانة شاملة) |
| POST /api/contracts/{id}/reactivate | إعادة تفعيل عقد معلق |

**لماذا؟** حالياً الفسخ يتم عبر UpdateStatus فقط بدون سبب أو غرامة.

### المجموعة 4: التقارير والتحليلات الخاصة بالعقد - متوسطة
| Endpoint | الوصف |
|----------|-------|
| GET /api/contracts/stats | إحصائيات: عدد Active/Expired/Terminated/Renewed، متوسط الإيجار |
| GET /api/contracts/{id}/audit | سجل التدقيق AuditLog الخاص بالعقد (من جدول AuditLogs) |
| GET /api/contracts/{id}/profitability | ربحية العقد: إجمالي مدفوعات - مصروفات مباشرة - صيانة |

**يربط مع:** AuditLog, Expense, Maintenance, Payment

### المجموعة 5: التكامل مع بوابة المستأجر والزوار - متوسطة
| Endpoint | الوصف |
|----------|-------|
| GET /api/contracts/{id}/visitor-passes | كل تصاريح الزوار الصادرة لمحل هذا العقد |
| GET /api/contracts/{id}/maintenance | طلبات الصيانة الخاصة بمحل هذا العقد |
| GET /api/contracts/{id}/tenant-balance | رصيد المستأجر الحالي + هل يغطي الإيجار القادم؟ |

### المجموعة 6: عمليات جماعية Bulk - منخفضة لكن مفيدة للإدارة
| Endpoint | الوصف |
|----------|-------|
| POST /api/contracts/bulk-increase | زيادة جماعية لنسبة من العقود (مثلاً زيادة 5% لكل عقود مبنى A) |
| POST /api/contracts/bulk-renew | تجديد جماعي لعقود تنتهي في شهر معين |

---

## توصيتي للتنفيذ حسب الأولوية

**المرحلة 1 (فوري - يوم):**
1. ✅ تعديل شامل (تم)
2. ✅ by-tenant, by-unit, expiring, renewal-chain, financial-summary (تم)
3. Documents upload (لأن الموديل موجود لكن غير مستخدم)

**المرحلة 2 (مهم - 2-3 أيام):**
4. /payments + /statement + fees CRUD
5. /terminate مع سبب وغرامة + /extend
6. /audit + /stats

**المرحلة 3 (تحسين):**
7. Profitability + Visitor integration + Bulk

---

## أسئلة لك قبل المتابعة:

1. هل تريد السماح بتعديل TenantId/UnitId في التعديل الشامل؟ حالياً مسموح لكن مع تحقق شغور. هل تفضل منعه بعد تفعيل العقد؟
2. هل تريد تفعيل رفع المستندات الآن؟
3. أي مجموعة تريد أن أبدأ بها بعد التعديل الشامل؟

أخبرني اختيارك وسأبدأ التنفيذ فوراً.

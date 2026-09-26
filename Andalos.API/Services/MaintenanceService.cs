using Andalos.API.Data;
using Andalos.API.DTOs.Maintenance;
using Andalos.API.Enums;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public class MaintenanceService : IMaintenanceService
    {
        private readonly AppDbContext _db;
        private readonly INumberGeneratorService _numberGen;
        private readonly INotificationService _notification;

        public MaintenanceService(
            AppDbContext db,
            INumberGeneratorService numberGen,
            INotificationService notification)
        {
            _db = db;
            _numberGen = numberGen;
            _notification = notification;
        }

        public async Task<List<MaintenanceResponseDto>> GetAllAsync()
        {
            var requests = await _db.MaintenanceRequests
                .Include(m => m.Unit)
                .Include(m => m.Tenant)
                .Where(m => m.IsActive)
                .OrderByDescending(m => m.RequestDate)
                .ToListAsync();

            var chargeMap = await GetChargesMapAsync(requests);
            return requests.Select(m => MapToDto(m, chargeMap.GetValueOrDefault(m.Id))).ToList();
        }

        public async Task<List<MaintenanceResponseDto>> GetByUnitAsync(int unitId)
        {
            var requests = await _db.MaintenanceRequests
                .Include(m => m.Unit)
                .Include(m => m.Tenant)
                .Where(m => m.UnitId == unitId && m.IsActive)
                .OrderByDescending(m => m.RequestDate)
                .ToListAsync();

            var chargeMap = await GetChargesMapAsync(requests);
            return requests.Select(m => MapToDto(m, chargeMap.GetValueOrDefault(m.Id))).ToList();
        }

        public async Task<MaintenanceResponseDto?> GetByIdAsync(int id)
        {
            var request = await _db.MaintenanceRequests
                .Include(m => m.Unit)
                .Include(m => m.Tenant)
                .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);

            if (request == null) return null;
            var charge = await _db.TenantCharges.FirstOrDefaultAsync(ch => ch.MaintenanceRequestId == request.Id);
            return MapToDto(request, charge);
        }

        public async Task<MaintenanceResponseDto> CreateAsync(CreateMaintenanceRequestDto dto)
        {
            var unit = await _db.Units.FirstOrDefaultAsync(u => u.Id == dto.UnitId && u.IsActive);
            if (unit == null)
                throw new KeyNotFoundException("المحل المحدد غير موجود");

            string requestNumber = await _numberGen.GenerateAsync("maintenance");

            var request = new MaintenanceRequest
            {
                RequestNumber = requestNumber,
                UnitId = dto.UnitId,
                TenantId = dto.TenantId,
                Type = dto.Type,
                Priority = dto.Priority,
                Status = MaintenanceStatus.New,
                Description = dto.Description,
                Cost = dto.Cost,
                Notes = dto.Notes
            };

            _db.MaintenanceRequests.Add(request);
            await _db.SaveChangesAsync();

            var saved = await _db.MaintenanceRequests
                .Include(m => m.Unit)
                .Include(m => m.Tenant)
                .FirstAsync(m => m.Id == request.Id);
            var savedCharge = await _db.TenantCharges.FirstOrDefaultAsync(ch => ch.MaintenanceRequestId == saved.Id);

            return MapToDto(saved, savedCharge);
        }

        public async Task<bool> UpdateStatusAsync(int id, UpdateMaintenanceStatusDto dto)
        {
            var request = await _db.MaintenanceRequests
                .Include(m => m.Unit)
                .Include(m => m.Tenant)
                .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);
            if (request == null) return false;

            var previousStatus = request.Status; // حفظ الحالة القديمة قبل التحديث (للإشعار)

            request.Status = dto.Status;
            request.Cost = dto.Cost > 0 ? dto.Cost : request.Cost;
            if (dto.Notes != null) request.Notes = dto.Notes;
            request.UpdatedAt = DateTimeHelper.LibyaNow;

            if (dto.Status == MaintenanceStatus.Completed)
                request.CompletionDate = DateTimeHelper.LibyaNow;

            // 👈 جديد: تحميل المستأجر تلقائياً عند الإكمال (إذا طُلب ذلك)
            if (dto.Status == MaintenanceStatus.Completed
                && dto.BilledToTenant
                && !request.BilledToTenant
                && request.TenantId != null
                && dto.BilledAmount > 0)
            {
                await ApplyChargeInternalAsync(request, dto.BilledAmount, dto.RecordCostExpense,
                    dto.Notes ?? $"تحميل تكلفة صيانة ({request.RequestNumber})", dto.BillingType);
            }

            await _db.SaveChangesAsync();

            // 🔔 إشعار المستأجر بتحديث حالة الصيانة
            if (request.TenantId != null && dto.Status != previousStatus)
            {
                _ = _notification.SendToTenantAsync(
                    request.TenantId.Value,
                    "تحديث على طلب الصيانة 🛠️",
                    $"تم تحديث حالة طلب الصيانة رقم {request.RequestNumber} إلى: {GetStatusLabel(dto.Status)}.",
                    NotificationType.MaintenanceStatusChanged,
                    "/portal/maintenance",
                    request.Id
                );
            }

            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var request = await _db.MaintenanceRequests.FirstOrDefaultAsync(m => m.Id == id && m.IsActive);
            if (request == null) return false;

            request.IsActive = false;
            request.UpdatedAt = DateTimeHelper.LibyaNow;
            await _db.SaveChangesAsync();
            return true;
        }

        // =========================================================================
        // 👈 جديد: تحميل المستأجر يدوياً (إجراء مستقل — زر "تحميل على المستأجر")
        // =========================================================================
        public async Task<MaintenanceResponseDto> ChargeTenantAsync(int id, ChargeMaintenanceDto dto)
        {
            var request = await _db.MaintenanceRequests
                .Include(m => m.Unit)
                .Include(m => m.Tenant)
                .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);

            if (request == null)
                throw new KeyNotFoundException("طلب الصيانة غير موجود");

            if (request.TenantId == null)
                throw new InvalidOperationException("لا يمكن التحميل على مستأجر: طلب الصيانة غير مرتبط بمستأجر");

            if (request.BilledToTenant)
            {
                var existingCharge = await _db.TenantCharges
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ch => ch.MaintenanceRequestId == request.Id);
                throw new InvalidOperationException($"تم تحميل هذا الطلب مسبقاً بمبلغ {request.BilledAmount:N2} د.ل (التحميل رقم {existingCharge?.ChargeNumber})");
            }

            if (dto.BilledAmount <= 0)
                throw new InvalidOperationException("مبلغ التحميل يجب أن يكون أكبر من صفر");

            if (dto.BillingType == MaintenanceBillingType.None)
                throw new InvalidOperationException("نوع الفوترة يجب أن يكون (عرض على المستأجر) أو (إجبارية)");

            await ApplyChargeInternalAsync(request, dto.BilledAmount, dto.RecordCostExpense,
                dto.Notes ?? $"تحميل تكلفة صيانة ({request.RequestNumber})", dto.BillingType);

            await _db.SaveChangesAsync();

            var saved = await _db.MaintenanceRequests
                .Include(m => m.Unit)
                .Include(m => m.Tenant)
                .FirstAsync(m => m.Id == request.Id);
            var savedCharge = await _db.TenantCharges.FirstOrDefaultAsync(ch => ch.MaintenanceRequestId == saved.Id);

            return MapToDto(saved, savedCharge);
        }

        public async Task<TenantChargeDto?> GetChargeAsync(int chargeId)
        {
            var charge = await _db.TenantCharges
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .Include(c => c.MaintenanceRequest)
                .FirstOrDefaultAsync(c => c.Id == chargeId && c.IsActive);

            return charge == null ? null : MapChargeToDto(charge);
        }

        public async Task<List<TenantChargeDto>> GetChargesAsync(int? tenantId, bool? unsettledOnly, ChargeStatus? status = null)
        {
            var query = _db.TenantCharges
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .Include(c => c.MaintenanceRequest)
                .Where(c => c.IsActive);

            if (tenantId.HasValue)
                query = query.Where(c => c.TenantId == tenantId.Value);

            if (unsettledOnly == true)
                query = query.Where(c => !c.IsSettled);

            // 👈 فلتر حالة العرض (معلق/مؤكد/مرفوض/مسدد)
            if (status.HasValue)
                query = query.Where(c => c.ChargeStatus == status.Value);

            var charges = await query
                .OrderByDescending(c => c.ChargeDate)
                .ToListAsync();

            return charges.Select(MapChargeToDto).ToList();
        }

        // =========================================================================
        // 👈 جديد: سداد التحميل المستحق (نقدي/تحويل) — هنا يدخل الإيراد فعلياً
        // =========================================================================
        public async Task<TenantChargeDto> SettleChargeAsync(int chargeId, SettleChargeDto dto)
        {
            var charge = await _db.TenantCharges
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .Include(c => c.MaintenanceRequest)
                .FirstOrDefaultAsync(c => c.Id == chargeId && c.IsActive);

            if (charge == null)
                throw new KeyNotFoundException("التحميل غير موجود");

            if (charge.IsSettled || charge.ChargeStatus == ChargeStatus.Paid)
                throw new InvalidOperationException("تم سداد هذا التحميل بالكامل مسبقاً");

            // 👈 لا سداد قبل تأكيد التحميل (رفض المستأجر أو انتظار موافقته)
            if (charge.ChargeStatus == ChargeStatus.PendingApproval)
                throw new InvalidOperationException("هذا التحميل ما زال عرضاً معلقاً — لا يمكن التحصيل قبل موافقة المستأجر");

            if (charge.ChargeStatus == ChargeStatus.Rejected)
                throw new InvalidOperationException("هذا التحميل مرفوض من المستأجر — لا يمكن التحصيل");

            decimal remaining = charge.Amount - charge.SettledAmount;
            if (remaining <= 0)
                throw new InvalidOperationException("لا يوجد مبلغ متبقٍ للسداد");

            // البحث عن العقد الساري للمستأجر (مطلوب لإنشاء الدفعة)
            var activeContract = await _db.Contracts
                .FirstOrDefaultAsync(c => c.TenantId == charge.TenantId && c.Status == ContractStatus.Active && c.IsActive);

            if (activeContract == null)
                throw new InvalidOperationException("لا يوجد عقد ساري للمستأجر — لا يمكن تسجيل السداد بدون عقد");

            string receiptNumber = await _numberGen.GenerateAsync("receipt");

            var payment = new Payment
            {
                ReceiptNumber = receiptNumber,
                ContractId = activeContract.Id,
                TenantId = charge.TenantId,
                UnitId = charge.UnitId ?? activeContract.UnitId,
                PaymentType = PaymentType.Maintenance, // 👈 إيراد صيانة (غير الإيجار)
                Amount = remaining,
                PaymentMethod = dto.PaymentMethod,
                ReferenceNumber = dto.ReferenceNumber,
                PaymentDate = dto.PaymentDate,
                Notes = $"سداد تحميل رقم ({charge.ChargeNumber}): {charge.Description}",
                IsActive = true
            };

            _db.Payments.Add(payment);

            charge.SettledAmount = charge.Amount;
            charge.IsSettled = true;
            charge.ChargeStatus = ChargeStatus.Paid;
            charge.SettlementReceiptNumber = receiptNumber;
            charge.UpdatedAt = DateTimeHelper.LibyaNow;

            await _db.SaveChangesAsync();

            // 🔔 إشعار المستأجر بالسداد
            _ = _notification.SendToTenantAsync(
                charge.TenantId,
                "تم سداد المتعلقات المالية ✅",
                $"تم استلام مبلغ {remaining:N2} د.ل كسداد للتحميل رقم {charge.ChargeNumber} بموجب الإيصال {receiptNumber}.",
                NotificationType.PaymentReceived,
                "/portal/payments",
                charge.Id
            );

            // إعادة التحميل مع العلاقات للعرض
            var saved = await _db.TenantCharges
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .Include(c => c.MaintenanceRequest)
                .FirstAsync(c => c.Id == charge.Id);

            return MapChargeToDto(saved);
        }

        // =========================================================================
        // 👈 المنطق المشترك للتحميل (يدوي أو تلقائي عند الإكمال)
        // السيناريو: تكلفة 1000 → تحميل 1500 → إيراد 1500 / مصروف 1000 / ربح 500
        // =========================================================================
        private async Task ApplyChargeInternalAsync(MaintenanceRequest request, decimal billedAmount, bool recordCostExpense, string? notes, MaintenanceBillingType billingType)
        {
            // 👈 العروض: لا تُسجل التكلفة كمصروف الآن — تُسجل فقط عند موافقة المستأجر (لأن العمل قد لا يتم)
            bool isOffer = billingType == MaintenanceBillingType.Offer;
            if (isOffer) recordCostExpense = false;

            // 1. تسجيل تكلفة الصيانة الفعلية كمصروف (مرة واحدة فقط لكل طلب — للإجبارية فقط)
            if (recordCostExpense && request.Cost > 0)
            {
                bool costExpenseExists = await _db.Expenses
                    .AnyAsync(e => e.MaintenanceRequestId == request.Id && e.IsActive);

                if (!costExpenseExists)
                {
                    string expenseNumber = await _numberGen.GenerateAsync("expense");

                    var costExpense = new Expense
                    {
                        ExpenseNumber = expenseNumber,
                        UnitId = request.UnitId,
                        TenantId = request.TenantId,
                        IsChargedToTenant = false, // التكلفة مصروف على الإدارة — التحميل يُسجل منفصلاً في TenantCharges
                        ExpenseType = ExpenseType.Maintenance,
                        Amount = request.Cost,
                        ExpenseDate = DateTimeHelper.LibyaNow,
                        Description = $"تكلفة تنفيذ صيانة ({request.RequestNumber}): {request.Description}",
                        MaintenanceRequestId = request.Id,
                        IsActive = true
                    };

                    _db.Expenses.Add(costExpense);
                }
            }

            // 2. إنشاء التحميل (مستحق على المستأجر)
            string chargeNumber = await _numberGen.GenerateAsync("charge");

            // العقد الساري (لربط التحصيل لاحقاً)
            var activeContract = await _db.Contracts
                .FirstOrDefaultAsync(c => c.TenantId == request.TenantId && c.Status == ContractStatus.Active && c.IsActive);

            var charge = new TenantCharge
            {
                ChargeNumber = chargeNumber,
                TenantId = request.TenantId!.Value,
                UnitId = request.UnitId,
                ContractId = activeContract?.Id,
                MaintenanceRequestId = request.Id,
                Amount = billedAmount,
                SettledAmount = 0,
                Description = notes ?? $"تحميل تكلفة صيانة ({request.RequestNumber})",
                ChargeDate = DateTimeHelper.LibyaNow,
                // 👈 الإجبارية: مؤكدة مباشرة / العرض: بانتظار موافقة المستأجر
                ChargeStatus = isOffer ? ChargeStatus.PendingApproval : ChargeStatus.Approved,
                IsSettled = false,
                IsActive = true
            };
            request.BillingType = billingType;

            // 3. السداد التلقائي من رصيد المستأجر الدائن — للإجبارية فقط (العرض ينتظر الموافقة)
            var tenant = await _db.Tenants.FirstAsync(t => t.Id == request.TenantId.Value);
            if (!isOffer && tenant.CreditBalance > 0 && activeContract != null)
            {
                decimal amountToDeduct = Math.Min(tenant.CreditBalance, billedAmount);
                tenant.CreditBalance -= amountToDeduct;

                string receiptNo = await _numberGen.GenerateAsync("receipt");

                var settlementPayment = new Payment
                {
                    ContractId = activeContract.Id,
                    TenantId = tenant.Id,
                    UnitId = request.UnitId,
                    Amount = amountToDeduct,
                    PaymentDate = DateTimeHelper.LibyaNow,
                    PaymentType = PaymentType.Maintenance, // 👈 إيراد صيانة
                    PaymentMethod = PaymentMethod.FromBalance,
                    ReceiptNumber = receiptNo,
                    Notes = $"خصم تلقائي لتحميل صيانة رقم ({chargeNumber}): {charge.Description}",
                    IsActive = true
                };

                _db.Payments.Add(settlementPayment);

                charge.SettledAmount = amountToDeduct;
                charge.IsSettled = charge.SettledAmount >= charge.Amount;
                charge.SettlementReceiptNumber = receiptNo;
            }

            _db.TenantCharges.Add(charge);

            // 4. تحديث طلب الصيانة
            request.BilledToTenant = true;
            request.BilledAmount = billedAmount;
            request.UpdatedAt = DateTimeHelper.LibyaNow;

            await _db.SaveChangesAsync();

            if (isOffer)
            {
                // 🔔 إشعار المستأجر: عرض صيانة يحتاج موافقة (قبول/رفض)
                _ = _notification.SendToTenantAsync(
                    request.TenantId.Value,
                    "عرض صيانة يحتاج موافقتكم 🛠️",
                    $"تعرض الإدارة تنفيذ: {request.Description} بمبلغ {billedAmount:N2} د.ل. يرجى قبول العرض أو رفضه من بوابة الصيانة.",
                    NotificationType.MaintenanceChargeOffer,
                    "/portal/maintenance",
                    charge.Id
                );

                // 🔔 إشعار الإدارة بأن العرض أُرسل وينتظر الرد
                _ = _notification.SendToGroupAsync(
                    "Accountants",
                    "عرض صيانة مُرسل للمستأجر ⏳",
                    $"تم إرسال عرض صيانة بمبلغ {billedAmount:N2} د.ل للمستأجر {request.Tenant?.FullName} ({request.RequestNumber}) — بانتظار موافقته أو رفضه.",
                    NotificationType.MaintenanceChargeOffer,
                    $"/admin/maintenance/{request.Id}"
                );
            }
            else
            {
                // 🔔 إشعار المستأجر بتحميل إجباري
                _ = _notification.SendToTenantAsync(
                    request.TenantId.Value,
                    "مبلغ محمّل على حسابك 📋",
                    $"تم تحميل مبلغ {billedAmount:N2} د.ل على حسابكم — {charge.Description}.",
                    NotificationType.AutomaticDeduction,
                    "/portal/payments",
                    charge.Id
                );

                // 🔔 إشعار الإدارة/المحاسبين
                _ = _notification.SendToGroupAsync(
                    "Accountants",
                    "تحميل مالي جديد على مستأجر 💰",
                    $"تم تحميل مبلغ {billedAmount:N2} د.ل على المستأجر {request.Tenant?.FullName} بموجب طلب الصيانة {request.RequestNumber}.",
                    NotificationType.AutomaticDeduction,
                    $"/admin/maintenance/{request.Id}"
                );
            }
        }

        // =========================================================================
        // 👈 جديد: رد المستأجر على عرض الصيانة (قبول / رفض)
        // =========================================================================
        public async Task<TenantChargeDto> RespondToChargeAsync(int chargeId, int tenantId, RespondChargeDto dto)
        {
            var charge = await _db.TenantCharges
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .Include(c => c.MaintenanceRequest)
                .FirstOrDefaultAsync(c => c.Id == chargeId && c.IsActive);

            if (charge == null)
                throw new KeyNotFoundException("العرض غير موجود");

            // 🔒 حماية: العرض يجب أن يكون للمستأجر نفسه
            if (charge.TenantId != tenantId)
                throw new UnauthorizedAccessException("لا يمكنك الرد على عرض لا يخصك");

            if (charge.ChargeStatus != ChargeStatus.PendingApproval)
                throw new InvalidOperationException("تم الرد على هذا العرض مسبقاً");

            var request = charge.MaintenanceRequest;
            if (request == null)
                throw new InvalidOperationException("العرض غير مرتبط بطلب صيانة");

            charge.RespondedAt = DateTimeHelper.LibyaNow;

            if (dto.Accept)
            {
                // ✅ قبول العرض → يصبح مؤكداً ويدخل المديونية
                charge.ChargeStatus = ChargeStatus.Approved;
                request.BillingType = MaintenanceBillingType.Offer;

                // تسجيل تكلفة الصيانة الفعلية كمصروف الآن (العمل سينفذ)
                if (request.Cost > 0)
                {
                    bool costExpenseExists = await _db.Expenses
                        .AnyAsync(e => e.MaintenanceRequestId == request.Id && e.IsActive);

                    if (!costExpenseExists)
                    {
                        string expenseNumber = await _numberGen.GenerateAsync("expense");
                        _db.Expenses.Add(new Expense
                        {
                            ExpenseNumber = expenseNumber,
                            UnitId = request.UnitId,
                            TenantId = request.TenantId,
                            IsChargedToTenant = false,
                            ExpenseType = ExpenseType.Maintenance,
                            Amount = request.Cost,
                            ExpenseDate = DateTimeHelper.LibyaNow,
                            Description = $"تكلفة تنفيذ صيانة ({request.RequestNumber}): {request.Description}",
                            MaintenanceRequestId = request.Id,
                            IsActive = true
                        });
                    }
                }

                // خصم تلقائي من رصيد المستأجر الدائن إن وُجد
                var activeContract = await _db.Contracts
                    .FirstOrDefaultAsync(ct => ct.TenantId == tenantId && ct.Status == ContractStatus.Active && ct.IsActive);

                var tenant = await _db.Tenants.FirstAsync(t => t.Id == tenantId);
                if (tenant.CreditBalance > 0 && activeContract != null)
                {
                    decimal amountToDeduct = Math.Min(tenant.CreditBalance, charge.Amount);
                    tenant.CreditBalance -= amountToDeduct;

                    string receiptNo = await _numberGen.GenerateAsync("receipt");
                    _db.Payments.Add(new Payment
                    {
                        ContractId = activeContract.Id,
                        TenantId = tenantId,
                        UnitId = charge.UnitId ?? activeContract.UnitId,
                        Amount = amountToDeduct,
                        PaymentDate = DateTimeHelper.LibyaNow,
                        PaymentType = PaymentType.Maintenance,
                        PaymentMethod = PaymentMethod.FromBalance,
                        ReceiptNumber = receiptNo,
                        Notes = $"خصم تلقائي بعد الموافقة على عرض صيانة ({charge.ChargeNumber})",
                        IsActive = true
                    });

                    charge.SettledAmount = amountToDeduct;
                    charge.IsSettled = charge.SettledAmount >= charge.Amount;
                    if (charge.IsSettled) charge.ChargeStatus = ChargeStatus.Paid;
                    charge.SettlementReceiptNumber = receiptNo;
                }

                await _db.SaveChangesAsync();

                // 🔔 إشعار الإدارة بقبول المستأجر
                _ = _notification.SendToGroupAsync(
                    "Accountants",
                    "المستأجر وافق على عرض الصيانة ✅",
                    $"وافق المستأجر {charge.Tenant?.FullName} على عرض بمبلغ {charge.Amount:N2} د.ل ({charge.ChargeNumber} — {request.RequestNumber}).",
                    NotificationType.MaintenanceChargeApproved,
                    $"/admin/maintenance/{request.Id}"
                );

                // 🔔 تأكيد للمستأجر
                _ = _notification.SendToTenantAsync(
                    tenantId,
                    "تم تأكيد قبولك للعرض ✅",
                    $"تم تأكيد قبولك لعرض الصيانة بمبلغ {charge.Amount:N2} د.ل وسيدخل ضمن متعلقات حسابكم.",
                    NotificationType.MaintenanceChargeApproved,
                    "/portal/maintenance",
                    charge.Id
                );
            }
            else
            {
                // ❌ رفض العرض → لا مصروف ولا مديونية
                charge.ChargeStatus = ChargeStatus.Rejected;
                charge.RejectionReason = dto.Reason;

                await _db.SaveChangesAsync();

                // 🔔 إشعار الإدارة برفض المستأجر
                _ = _notification.SendToGroupAsync(
                    "Accountants",
                    "المستأجر رفض عرض الصيانة ❌",
                    $"رفض المستأجر {charge.Tenant?.FullName} العرض رقم {charge.ChargeNumber} ({request.RequestNumber}).{(string.IsNullOrWhiteSpace(dto.Reason) ? "" : " السبب: " + dto.Reason)}",
                    NotificationType.MaintenanceChargeRejected,
                    $"/admin/maintenance/{request.Id}"
                );
            }

            var saved = await _db.TenantCharges
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .Include(c => c.MaintenanceRequest)
                .FirstAsync(c => c.Id == charge.Id);

            return MapChargeToDto(saved);
        }

        private static string GetStatusLabel(MaintenanceStatus status) => status switch
        {
            MaintenanceStatus.New => "جديد",
            MaintenanceStatus.InProgress => "قيد التنفيذ",
            MaintenanceStatus.Completed => "مكتمل",
            MaintenanceStatus.Cancelled => "ملغي",
            _ => status.ToString()
        };

        // 👈 جلب التحميلات المرتبطة بمجموعة طلبات (بدون علاقة تنقل عكسية)
        private async Task<Dictionary<int, TenantCharge>> GetChargesMapAsync(List<MaintenanceRequest> requests)
        {
            var ids = requests.Select(r => r.Id).ToList();
            var charges = await _db.TenantCharges
                .Where(ch => ch.MaintenanceRequestId != null && ids.Contains(ch.MaintenanceRequestId.Value))
                .ToListAsync();

            return charges
                .GroupBy(ch => ch.MaintenanceRequestId!.Value)
                .ToDictionary(g => g.Key, g => g.First());
        }

        private static MaintenanceResponseDto MapToDto(MaintenanceRequest m, TenantCharge? charge)
        {
            return new MaintenanceResponseDto
            {
                Id = m.Id,
                RequestNumber = m.RequestNumber,
                UnitId = m.UnitId,
                UnitNumber = m.Unit?.UnitNumber ?? "",
                UnitName = m.Unit?.UnitNumber ?? "",
                TenantId = m.TenantId,
                TenantName = m.Tenant?.FullName,
                Type = m.Type.ToString(),
                Priority = m.Priority.ToString(),
                Status = m.Status.ToString(),
                Description = m.Description,
                Cost = m.Cost,
                RequestDate = m.RequestDate,
                CompletionDate = m.CompletionDate,
                Notes = m.Notes,

                // 👈 معلومات التحميل
                BilledToTenant = m.BilledToTenant,
                BilledAmount = m.BilledAmount,
                BillingType = m.BillingType,
                ChargeStatus = charge?.ChargeStatus ?? ChargeStatus.PendingApproval,
                ChargeSettledAmount = charge?.SettledAmount,
                ChargeIsSettled = charge?.IsSettled ?? false,
                ProfitAmount = m.BilledToTenant ? m.BilledAmount - m.Cost : null
            };
        }

        private static string GetChargeStatusLabel(ChargeStatus status) => status switch
        {
            ChargeStatus.PendingApproval => "بانتظار موافقة المستأجر",
            ChargeStatus.Approved => "مؤكد",
            ChargeStatus.Rejected => "مرفوض",
            ChargeStatus.Paid => "مسدد",
            _ => status.ToString()
        };

        private static TenantChargeDto MapChargeToDto(TenantCharge c)
        {
            return new TenantChargeDto
            {
                Id = c.Id,
                ChargeNumber = c.ChargeNumber,
                TenantId = c.TenantId,
                TenantName = c.Tenant?.FullName,
                UnitId = c.UnitId,
                UnitNumber = c.Unit?.UnitNumber,
                MaintenanceRequestId = c.MaintenanceRequestId,
                RequestNumber = c.MaintenanceRequest?.RequestNumber,
                Amount = c.Amount,
                SettledAmount = c.SettledAmount,
                IsSettled = c.IsSettled,
                ChargeStatusLabel = GetChargeStatusLabel(c.ChargeStatus),
                RespondedAt = c.RespondedAt,
                RejectionReason = c.RejectionReason,
                SettlementReceiptNumber = c.SettlementReceiptNumber,
                ChargeDate = c.ChargeDate,
                Description = c.Description,
                Notes = c.Notes
            };
        }
    }
}

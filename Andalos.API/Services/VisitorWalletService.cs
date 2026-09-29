using Andalos.API.Security;
using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.DTOs.Visitors;
using Andalos.API.Enums;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Andalos.API.Services
{
    public class VisitorWalletService : IVisitorWalletService
    {
        private readonly AppDbContext _db;
        private readonly ISettingService _settings;
        private readonly ITenantAccountService _tenantAccountService; // 👈 لخصم التسوية من الإيجار عند الرغبة
        private readonly INotificationService _notification; // 👈 حقن خدمة الإشعارات الفورية

        public VisitorWalletService(
            AppDbContext db,
            ISettingService settings,
            ITenantAccountService tenantAccountService,
            INotificationService notification) // 👈 إضافة الخدمة في الباني
        {
            _db = db;
            _settings = settings;
            _tenantAccountService = tenantAccountService;
            _notification = notification;
        }

        // =====================================================
        // 1. البوابة: إصدار تصريح مدفوع برصيد + تحديث شفت الحارس
        // =====================================================
        public Task<VisitorPassResponseDto> CreatePaidPassAsync(CreatePaidVisitorPassDto dto, int gatekeeperUserId) => _db.AtomicAsync(() => CreatePaidPassAsyncCore(dto, gatekeeperUserId));

        private async Task<VisitorPassResponseDto> CreatePaidPassAsyncCore(CreatePaidVisitorPassDto dto, int gatekeeperUserId)
        {
            FinancialOperationGuard.ValidateAmount(dto.Amount, 100_000m);
            var now = DateTimeHelper.LibyaNow;
            if (await _db.VisitorBlacklists.AnyAsync(b => b.IsActive && (b.IsPermanent || b.ExpiresAt == null || b.ExpiresAt > now) &&
                (b.Phone == dto.VisitorPhone || (dto.NationalId != null && b.NationalId == dto.NationalId)))) throw new ForbiddenOperationException();
            decimal passPrice = dto.Amount;
            string passCode = await GenerateUniquePaidPassCodeAsync();

            var pass = new VisitorPass
            {
                PassCode = passCode,
                VisitorName = dto.VisitorName,
                VisitorPhone = dto.VisitorPhone,
                NationalId = dto.NationalId,
                VisitorType = VisitorType.Customer,
                ValidDate = DateTimeHelper.LibyaToday,
                MaxEntries = 1,
                UsedCount = 0,
                Status = PassStatus.Active,
                IsPaidPass = true,
                InitialBalance = passPrice,
                RemainingBalance = passPrice, // الرصيد المتاح للشراء
                WalletStatus = WalletStatus.Active,
                IssuedByUserId = gatekeeperUserId,
                Purpose = dto.Purpose ?? "زيارة وتسوق بالمجمع",
                Notes = dto.Notes,
                CreatedBy = $"Gatekeeper_{gatekeeperUserId}"
            };

            _db.VisitorPasses.Add(pass);

            // 👈 تحديث/فتح شفت الحارس لتراكم الكاش
            var shift = await _db.GatekeeperShifts
                .FirstOrDefaultAsync(s => s.UserId == gatekeeperUserId && !s.IsHandedOver && s.IsActive);

            if (shift == null)
            {
                shift = new GatekeeperShift
                {
                    UserId = gatekeeperUserId,
                    StartTime = DateTimeHelper.LibyaNow,
                    TotalPassesIssued = 1,
                    TotalCashCollected = passPrice,
                    IsActive = true
                };
                _db.GatekeeperShifts.Add(shift);
            }
            else
            {
                shift.TotalPassesIssued++;
                shift.TotalCashCollected += passPrice;
                shift.UpdatedAt = DateTimeHelper.LibyaNow;
            }

            await _db.SaveChangesAsync();
            _db.GateCashReceipts.Add(new() { VisitorPassId = pass.Id, ShiftId = shift.Id, UserId = gatekeeperUserId,
                Amount = passPrice, Kind = "Issue", CreatedAt = DateTimeHelper.LibyaNow });
            await _db.SaveChangesAsync();

            return new VisitorPassResponseDto
            {
                Id = pass.Id,
                PassCode = pass.PassCode,
                VisitorName = pass.VisitorName,
                VisitorPhone = pass.VisitorPhone,
                NationalId = pass.NationalId,
                VisitorType = pass.VisitorType.ToString(),
                ValidDate = pass.ValidDate,
                MaxEntries = pass.MaxEntries,
                UsedCount = pass.UsedCount,
                Status = pass.Status.ToString(),
                Purpose = pass.Purpose,
                Notes = $"تصريح مدفوع برصيد {passPrice} د.ل",
                CreatedAt = pass.CreatedAt
            };
        }

        public Task<AddBalanceToPassResponseDto> AddBalanceToPassAsync(AddBalanceToPassDto dto, int userId) => _db.AtomicAsync(async () =>
        {
            FinancialOperationGuard.ValidateAmount(dto.Amount, 100_000m);
            var pass = await _db.VisitorPasses.SingleOrDefaultAsync(p => p.IsActive && p.PassCode == dto.PassCode);
            if (pass is null) throw new KeyNotFoundException();
            if (await _db.VisitorBlacklists.AnyAsync(b => b.IsActive && (b.IsPermanent || b.ExpiresAt == null || b.ExpiresAt > DateTimeHelper.LibyaNow) &&
                ((!string.IsNullOrEmpty(pass.VisitorPhone) && b.Phone == pass.VisitorPhone) ||
                 (!string.IsNullOrEmpty(pass.NationalId) && b.NationalId == pass.NationalId)))) throw new ForbiddenOperationException();
            if (!pass.IsPaidPass || pass.ValidDate.Date != DateTimeHelper.LibyaToday ||
                pass.Status is PassStatus.Revoked or PassStatus.Expired || pass.WalletStatus == WalletStatus.Expired || pass.RemainingBalance + dto.Amount > 100_000m)
                throw new ArgumentException("Pass cannot be topped up.");
            var shift = await _db.GatekeeperShifts.SingleOrDefaultAsync(s => s.UserId == userId && s.IsActive && !s.IsHandedOver);
            if (shift is null) { shift = new() { UserId = userId, StartTime = DateTimeHelper.LibyaNow }; _db.GatekeeperShifts.Add(shift); }
            shift.TotalCashCollected += dto.Amount; shift.UpdatedAt = DateTimeHelper.LibyaNow;
            pass.RemainingBalance += dto.Amount; pass.WalletStatus = WalletStatus.Active; pass.UpdatedAt = DateTimeHelper.LibyaNow;
            await _db.SaveChangesAsync();
            _db.GateCashReceipts.Add(new() { VisitorPassId = pass.Id, ShiftId = shift.Id, UserId = userId, Amount = dto.Amount,
                Kind = "TopUp", CreatedAt = DateTimeHelper.LibyaNow });
            await _db.SaveChangesAsync();
            return new AddBalanceToPassResponseDto { PassId = pass.Id, AddedAmount = dto.Amount, RemainingBalance = pass.RemainingBalance, ShiftId = shift.Id };
        });

        // =====================================================
        // 2. المحل: الخصم بالـ QR Code وحساب الفرق الكاش
        // =====================================================
        public Task<PassPurchaseResultDto> ProcessShopPurchaseAsync(ProcessPassPurchaseDto dto, int tenantId) => _db.AtomicAsync(() => ProcessShopPurchaseAsyncCore(dto, tenantId));

        private async Task<PassPurchaseResultDto> ProcessShopPurchaseAsyncCore(ProcessPassPurchaseDto dto, int tenantId)
        {
            FinancialOperationGuard.ValidateAmount(dto.PurchaseAmount, 100_000m);
            if (dto.TenantId.HasValue && dto.TenantId != tenantId) throw new ForbiddenOperationException();
            var units = await _db.Contracts.Where(c => c.TenantId == tenantId && c.IsActive && c.Status == ContractStatus.Active && c.Unit!.IsActive)
                .Select(c => c.UnitId).Distinct().ToListAsync();
            var unitId = dto.UnitId ?? (units.Count == 1 ? units[0] : 0);
            if (!units.Contains(unitId)) throw new ForbiddenOperationException();

            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);
            if (tenant == null)
                return new PassPurchaseResultDto { IsSuccess = false, Message = "❌ حساب المحل غير موجود بالنظام" };

            var pass = await _db.VisitorPasses
                .FirstOrDefaultAsync(p => p.PassCode == dto.PassCode && p.IsActive);

            if (pass == null)
                return new PassPurchaseResultDto { IsSuccess = false, Message = "❌ رمز الباركود غير صحيح أو غير موجود" };

            if (!pass.IsPaidPass)
                return new PassPurchaseResultDto { IsSuccess = false, Message = "❌ هذا التصريح مجاني ولا يحتوي على محفظة مالية" };

            if (pass.ValidDate.Date != DateTimeHelper.LibyaToday || pass.Status is PassStatus.Revoked or PassStatus.Expired)
                return new PassPurchaseResultDto { IsSuccess = false, Message = "❌ صلاحية كود الشراء انتهت (تاريخ اليوم فقط)" };

            if (pass.WalletStatus != WalletStatus.Active || pass.RemainingBalance <= 0)
                return new PassPurchaseResultDto { IsSuccess = false, Message = "❌ رصيد هذا التصريح مستنفد بالكامل (0 د.ل)" };

            // 👈 معادلة الخصم الحساسة:
            decimal chargedAmount = 0;
            decimal cashDifference = 0;

            if (pass.RemainingBalance >= dto.PurchaseAmount)
            {
                // الرصيد يكفي الفاتورة بالكامل
                chargedAmount = dto.PurchaseAmount;
                cashDifference = 0;
                pass.RemainingBalance -= dto.PurchaseAmount;
            }
            else
            {
                // الرصيد غير كافٍ: نخصم المتبقي بالكامل، والباقي يدفعه كاش للمحل
                chargedAmount = pass.RemainingBalance;
                cashDifference = dto.PurchaseAmount - pass.RemainingBalance;
                pass.RemainingBalance = 0;
            }

            if (pass.RemainingBalance == 0)
            {
                pass.WalletStatus = WalletStatus.Depleted;
            }

            pass.UpdatedAt = DateTimeHelper.LibyaNow;

            // تسجيل الحركة لصالح المحل
            var transaction = new PassTransaction
            {
                VisitorPassId = pass.Id,
                TenantId = tenantId,
                UnitId = unitId,
                Amount = chargedAmount,
                TransactionDate = DateTimeHelper.LibyaNow,
                IsSettled = false
            };

            _db.PassTransactions.Add(transaction);
            await _db.SaveChangesAsync();

            // 🔔 [إشعار فوري]: تنبيه المستأجر (صاحب المحل) بخصم ناجح من محفظة الزائر وتوضيح المتبقي كاش إن وجد
            await _notification.SendToTenantAsync(
                tenantId,
                "عملية بيع عبر محفظة زائر 🛒",
                $"تم خصم مبلغ {chargedAmount:N2} د.ل من محفظة الزائر ({pass.VisitorName}) لصالح محلكم بنجاح.{(cashDifference > 0 ? $" المتبقي كاش: {cashDifference:N2} د.ل." : "")}",
                NotificationType.PaymentReceived,
                "/portal/wallet-scanner",
                transaction.Id
            );

            string msg = cashDifference > 0
                ? $"✅ تم خصم {chargedAmount} د.ل من التصريح. يرجى تحصيل المتبقي ({cashDifference} د.ل) كاش من الزائر."
                : $"✅ تم خصم كامل قيمة الفاتورة ({chargedAmount} د.ل) بنجاح من التصريح.";

            return new PassPurchaseResultDto
            {
                IsSuccess = true,
                Message = msg,
                VisitorName = pass.VisitorName,
                TotalPurchaseAmount = dto.PurchaseAmount,
                ChargedFromPass = chargedAmount,
                CashDifferenceToPay = cashDifference,
                PassRemainingBalance = pass.RemainingBalance,
                TransactionTime = transaction.TransactionDate
            };
        }

        // =====================================================
        // 3. استعراض مبيعات المحل بانتظار التسديد
        // =====================================================
        public async Task<TenantPassBalanceDto> GetMyUnsettledBalanceAsync(int tenantId)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);
            if (tenant == null) throw new KeyNotFoundException("المستأجر غير موجود");

            var activeContract = await _db.Contracts
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Status == ContractStatus.Active && c.IsActive);

            var unsettledQuery = _db.PassTransactions
                .Where(t => t.TenantId == tenantId && !t.IsSettled && t.IsActive);

            var totalAmount = await unsettledQuery.SumAsync(t => t.Amount);
            var count = await unsettledQuery.CountAsync();
            var lastDate = await unsettledQuery.MaxAsync(t => (DateTime?)t.TransactionDate);

            return new TenantPassBalanceDto
            {
                TenantId = tenant.Id,
                TenantName = tenant.FullName,
                TradeName = activeContract?.TradeName,
                TotalUnsettledAmount = totalAmount,
                UnsettledTransactionsCount = count,
                LastTransactionDate = lastDate
            };
        }

        public async Task<List<TenantPassBalanceDto>> GetAllShopsUnsettledBalancesAsync()
        {
            return await _db.PassTransactions.Where(t => t.IsActive && !t.IsSettled && t.Tenant != null && t.Tenant.IsActive)
                .GroupBy(t => new { t.TenantId, t.Tenant!.FullName })
                .Select(g => new TenantPassBalanceDto { TenantId = g.Key.TenantId!.Value, TenantName = g.Key.FullName,
                    TotalUnsettledAmount = g.Sum(t => t.Amount), UnsettledTransactionsCount = g.Count(), LastTransactionDate = g.Max(t => t.TransactionDate) })
                .OrderByDescending(t => t.TotalUnsettledAmount).ToListAsync();
        }

        // =====================================================
        // 4. الإدارة: تسديد مستحقات المحل (كاش / تحويل / خصم من الإيجار!)
        // =====================================================
        public Task<SettlementResponseDto> SettleShopBalanceAsync(ProcessSettlementDto dto, int adminUserId) => _db.AtomicAsync(() => SettleShopBalanceAsyncCore(dto, adminUserId));

        private async Task<SettlementResponseDto> SettleShopBalanceAsyncCore(ProcessSettlementDto dto, int adminUserId)
        {
            if (!Enum.IsDefined(dto.SettlementMethod)) throw new ArgumentException("Unknown settlement method.");
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == dto.TenantId && t.IsActive);
            if (tenant == null)
                throw new KeyNotFoundException("المستأجر غير موجود");

            var unsettledTransactions = await _db.PassTransactions
                .Where(t => t.TenantId == dto.TenantId && !t.IsSettled && t.IsActive)
                .ToListAsync();

            if (!unsettledTransactions.Any())
                throw new InvalidOperationException("لا توجد مبيعات غير مسددة لهذا المحل لتسويتها");

            decimal totalAmount = unsettledTransactions.Sum(t => t.Amount);

            // إنشاء قيد التسوية
            var settlement = new TenantSettlement
            {
                TenantId = dto.TenantId,
                TotalAmount = totalAmount,
                SettlementDate = DateTimeHelper.LibyaNow,
                SettlementMethod = dto.SettlementMethod,
                ProcessedByUserId = adminUserId
            };

            _db.TenantSettlements.Add(settlement);
            await _db.SaveChangesAsync();

            // تحديث الحركات لتكون مسددة
            foreach (var trans in unsettledTransactions)
            {
                trans.IsSettled = true;
                trans.SettlementId = settlement.Id;
                trans.UpdatedAt = DateTimeHelper.LibyaNow;
            }

            // 💡 👈 التصحيح المحاسبي الجوهري:
            // عند التسوية بـ RentDeduction نمرر PaymentMethod.Transfer (أو Cash)
            // لكي تُحتسب كـ Credit دائن حقيقي يُخفض مديونية المستأجر في كشف الحساب ويُضاف لمحفظته!
            if (dto.SettlementMethod == SettlementMethod.RentDeduction)
            {
                await _tenantAccountService.DepositAdvancePaymentAsync(
                    dto.TenantId,
                    totalAmount,
                    PaymentMethod.Transfer, // 👈 تم التعديل من FromBalance إلى Transfer
                    $"تسوية مبيعات زوار الـ QR (سند تسوية رقم {settlement.Id}) - إضافة لرصيد الإيجار"
                );
            }

            await _db.SaveChangesAsync();

            // 🔔 [إشعار فوري]: تنبيه المستأجر بتسوية مستحقات مبيعات الزوار وتوضيح طريقة الدفع المتخذة
            await _notification.SendToTenantAsync(
                dto.TenantId,
                "تمت تسوية مستحقات مبيعات الزوار ✅",
                $"نعلمكم بأنه تمت تسوية مستحقات مبيعات زوار الـ QR الخاصة بمحلكم بقيمة {totalAmount:N2} د.ل بنجاح عبر طريقة ({GetSettlementMethodLabel(dto.SettlementMethod)}).",
                NotificationType.PaymentReceived,
                "/portal/wallet-scanner",
                settlement.Id
            );

            return new SettlementResponseDto
            {
                SettlementId = settlement.Id,
                TenantId = tenant.Id,
                TenantName = tenant.FullName,
                TotalSettledAmount = totalAmount,
                SettlementMethod = dto.SettlementMethod.ToString(),
                SettledTransactionsCount = unsettledTransactions.Count,
                SettlementDate = settlement.SettlementDate
            };
        }

        // =====================================================
        // 5. الحراس: ملخص العهدة وتسليم الكاش
        // =====================================================
        public async Task<GatekeeperShiftSummaryDto> GetCurrentShiftSummaryAsync(int gatekeeperUserId)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == gatekeeperUserId);
            var shift = await _db.GatekeeperShifts
                .FirstOrDefaultAsync(s => s.UserId == gatekeeperUserId && !s.IsHandedOver && s.IsActive);

            if (shift == null)
            {
                return new GatekeeperShiftSummaryDto
                {
                    UserId = gatekeeperUserId,
                    GatekeeperName = user?.FullName ?? "حارس البوابة",
                    StartTime = DateTimeHelper.LibyaNow,
                    TotalPassesIssued = 0,
                    TotalCashCollected = 0,
                    IsHandedOver = false
                };
            }

            return new GatekeeperShiftSummaryDto
            {
                ShiftId = shift.Id,
                UserId = gatekeeperUserId,
                GatekeeperName = user?.FullName ?? "حارس البوابة",
                StartTime = shift.StartTime,
                EndTime = shift.EndTime,
                TotalPassesIssued = shift.TotalPassesIssued,
                TotalCashCollected = shift.TotalCashCollected,
                IsHandedOver = shift.IsHandedOver,
                HandedOverAt = shift.HandedOverAt
            };
        }

        public Task<bool> HandoverShiftCashAsync(int shiftId, int adminUserId) => _db.AtomicAsync(() => HandoverShiftCashAsyncCore(shiftId, adminUserId));

        private async Task<bool> HandoverShiftCashAsyncCore(int shiftId, int adminUserId)
        {
            var shift = await _db.GatekeeperShifts.FirstOrDefaultAsync(s => s.Id == shiftId && s.IsActive);
            if (shift == null || shift.IsHandedOver) return false;

            if (shift.UserId == adminUserId) throw new ForbiddenOperationException(); // segregation of duties
            shift.HandedOverByUserId = adminUserId;
            shift.IsHandedOver = true;
            shift.HandedOverAt = DateTimeHelper.LibyaNow;
            shift.EndTime = DateTimeHelper.LibyaNow;
            shift.UpdatedAt = DateTimeHelper.LibyaNow;

            await _db.SaveChangesAsync();
            return true;
        }

        // =====================================================
        // 6. Scheduler: تصفير الأرصدة المنتهية بنهاية اليوم
        // =====================================================
        public Task<decimal> ExpireUnusedBalancesAsync() => _db.AtomicAsync(() => ExpireUnusedBalancesAsyncCore());

        private async Task<decimal> ExpireUnusedBalancesAsyncCore()
        {
            var today = DateTimeHelper.LibyaToday;

            // تصاريح الأيام السابقة التي لم تُصفر بعد
            var expiredPasses = await _db.VisitorPasses
                .Where(p => p.IsPaidPass && p.IsActive && p.ValidDate.Date < today && p.RemainingBalance > 0)
                .ToListAsync();

            decimal totalForfeited = 0;

            foreach (var pass in expiredPasses)
            {
                totalForfeited += pass.RemainingBalance;
                pass.RemainingBalance = 0;
                pass.WalletStatus = WalletStatus.Expired;
                pass.UpdatedAt = DateTimeHelper.LibyaNow;
            }

            await _db.SaveChangesAsync();
            return totalForfeited; // إرجاع إجمالي المبالغ المتبقية التي أصبحت أرباحاً صافية للإدارة
        }

        // =====================================================
        // تقرير حركات خصم الـ QR التفصيلي مع الفلاتر
        // =====================================================
        public async Task<List<PassTransactionDetailDto>> GetPassTransactionsReportAsync(
            int? tenantId,
            int? unitId,
            DateTime? fromDate,
            DateTime? toDate,
            bool? isSettled)
        {
            var query = _db.PassTransactions
                .Include(t => t.VisitorPass)
                .Include(t => t.Tenant)
                .Include(t => t.Unit)
                .Include(t => t.Settlement)
                .Where(t => t.IsActive);

            // فلترة بالمستأجر
            if (tenantId.HasValue)
                query = query.Where(t => t.TenantId == tenantId.Value);

            // فلترة بالمحل
            if (unitId.HasValue)
                query = query.Where(t => t.UnitId == unitId.Value);

            // فلترة من تاريخ
            if (fromDate.HasValue)
                query = query.Where(t => t.TransactionDate >= fromDate.Value.Date);

            // فلترة إلى تاريخ (يشمل كامل اليوم حتى 23:59:59)
            if (toDate.HasValue)
            {
                var actualToDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(t => t.TransactionDate <= actualToDate);
            }

            // فلترة بحالة التسوية
            if (isSettled.HasValue)
                query = query.Where(t => t.IsSettled == isSettled.Value);

            var list = await query
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            return list.Select(t => new PassTransactionDetailDto
            {
                TransactionId = t.Id,
                PassCode = t.VisitorPass?.PassCode ?? "",
                VisitorName = t.VisitorPass?.VisitorName ?? "",
                VisitorPhone = t.VisitorPass?.VisitorPhone ?? "",
                TenantId = t.TenantId,
                TenantName = t.Tenant?.FullName ?? "",
                UnitId = t.UnitId,
                UnitNumber = t.Unit?.UnitNumber ?? "",
                Amount = t.Amount,
                TransactionDate = t.TransactionDate,
                IsSettled = t.IsSettled,
                SettlementId = t.SettlementId,
                SettlementDate = t.Settlement?.SettlementDate
            }).ToList();
        }

        // =====================================================
        // تقرير المبالغ المستلمة في البوابة مع الفلترة
        // =====================================================
        public async Task<GateCashReportSummaryDto> GetGateCashReportAsync(
            int? gatekeeperUserId,
            DateTime? fromDate,
            DateTime? toDate,
            bool? isHandedOver)
        {
            var from = fromDate?.Date;
            var to = toDate.HasValue
                ? toDate.Value.Date.AddDays(1).AddTicks(-1)
                : (DateTime?)null;

            var receiptsQuery = from r in _db.GateCashReceipts
                join p in _db.VisitorPasses on r.VisitorPassId equals p.Id
                join u in _db.Users on r.UserId equals u.Id
                where (!isHandedOver.HasValue || _db.GatekeeperShifts.Any(s => s.Id == r.ShiftId && s.IsHandedOver == isHandedOver.Value)) &&
                    (!gatekeeperUserId.HasValue || r.UserId == gatekeeperUserId.Value) &&
                    (!from.HasValue || r.CreatedAt >= from.Value) && (!to.HasValue || r.CreatedAt <= to.Value)
                orderby r.CreatedAt descending
                select new GateCashReceiptDetailDto { ReceiptId = r.Id, ReceiptKind = r.Kind, PassId = p.Id, PassCode = p.PassCode,
                    VisitorName = p.VisitorName, VisitorPhone = p.VisitorPhone, AmountCollected = r.Amount, IssuedAt = r.CreatedAt,
                    ValidDate = p.ValidDate, IssuedByUserId = r.UserId, GatekeeperName = u.FullName, Purpose = p.Purpose,
                    WalletStatus = p.WalletStatus.ToString(), RemainingBalance = p.RemainingBalance };
            var receipts = await receiptsQuery.ToListAsync();

            // 2) ملخص الورديات
            var shiftsQuery = _db.GatekeeperShifts
                .Include(s => s.User)
                .Where(s => s.IsActive);

            if (gatekeeperUserId.HasValue)
                shiftsQuery = shiftsQuery.Where(s => s.UserId == gatekeeperUserId.Value);

            if (from.HasValue)
                shiftsQuery = shiftsQuery.Where(s => s.StartTime >= from.Value);

            if (to.HasValue)
                shiftsQuery = shiftsQuery.Where(s => s.StartTime <= to.Value);

            if (isHandedOver.HasValue)
                shiftsQuery = shiftsQuery.Where(s => s.IsHandedOver == isHandedOver.Value);

            var shifts = await shiftsQuery
                .OrderByDescending(s => s.StartTime)
                .ToListAsync();

            var shiftDtos = shifts.Select(s => new GateShiftCashSummaryDto
            {
                ShiftId = s.Id,
                UserId = s.UserId,
                GatekeeperName = s.User?.FullName ?? "حارس غير معروف",
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                TotalPassesIssued = s.TotalPassesIssued,
                TotalCashCollected = s.TotalCashCollected,
                IsHandedOver = s.IsHandedOver,
                HandedOverAt = s.HandedOverAt,
                HandedOverByUserId = s.HandedOverByUserId
            }).ToList();

            // 3) الإجماليات
            decimal totalCash = receipts.Sum(r => r.AmountCollected);
            decimal handedOverCash = shiftDtos.Where(s => s.IsHandedOver).Sum(s => s.TotalCashCollected);
            decimal pendingCash = shiftDtos.Where(s => !s.IsHandedOver).Sum(s => s.TotalCashCollected);

            return new GateCashReportSummaryDto
            {
                TotalReceiptsCount = receipts.Count,
                TotalCashCollected = totalCash,
                TotalHandedOverCash = handedOverCash,
                TotalPendingHandoverCash = pendingCash,
                TotalShiftsCount = shiftDtos.Count,
                OpenShiftsCount = shiftDtos.Count(s => !s.IsHandedOver),
                Receipts = receipts,
                Shifts = shiftDtos
            };
        }
        // =====================================================
        // جلب سجل مبيعات وحركات محفظة الـ QR الشامل للمستأجر
        // =====================================================
        public async Task<TenantWalletFullHistoryDto> GetTenantWalletHistoryAsync(
            int tenantId,
            DateTime? fromDate,
            DateTime? toDate,
            bool? isSettled)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);
            if (tenant == null)
                throw new KeyNotFoundException("المستأجر غير موجود");

            var activeContract = await _db.Contracts
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Status == ContractStatus.Active && c.IsActive);

            var query = _db.PassTransactions
                .Include(t => t.VisitorPass)
                .Include(t => t.Unit)
                .Include(t => t.Settlement)
                .Where(t => t.TenantId == tenantId && t.IsActive);

            // تطبيق فلاتر التاريخ
            if (fromDate.HasValue)
                query = query.Where(t => t.TransactionDate >= fromDate.Value.Date);

            if (toDate.HasValue)
            {
                var actualToDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(t => t.TransactionDate <= actualToDate);
            }

            // فلترة بحالة التسوية (حسب طلب المستأجر)
            if (isSettled.HasValue)
                query = query.Where(t => t.IsSettled == isSettled.Value);

            var transactions = await query
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            // حساب الإحصائيات
            decimal totalUnsettled = transactions.Where(t => !t.IsSettled).Sum(t => t.Amount);
            decimal totalSettled = transactions.Where(t => t.IsSettled).Sum(t => t.Amount);

            var transactionDtos = transactions.Select(t => new PassTransactionDetailDto
            {
                TransactionId = t.Id,
                PassCode = t.VisitorPass?.PassCode ?? "",
                VisitorName = t.VisitorPass?.VisitorName ?? "",
                VisitorPhone = t.VisitorPass?.VisitorPhone ?? "",
                TenantId = t.TenantId,
                TenantName = tenant.FullName,
                UnitId = t.UnitId,
                UnitNumber = t.Unit?.UnitNumber ?? "",
                Amount = t.Amount,
                TransactionDate = t.TransactionDate,
                IsSettled = t.IsSettled,
                SettlementId = t.SettlementId,
                SettlementDate = t.Settlement?.SettlementDate
            }).ToList();

            return new TenantWalletFullHistoryDto
            {
                TenantId = tenant.Id,
                TenantName = tenant.FullName,
                TradeName = activeContract?.TradeName,
                TotalUnsettledAmount = totalUnsettled,
                TotalSettledAmount = totalSettled,
                GrandTotalEarned = totalUnsettled + totalSettled,
                TotalTransactionsCount = transactions.Count,
                Transactions = transactionDtos
            };
        }

        // =====================================================
        // دوال داخلية مساعدة لخدمة المحفظة
        // =====================================================
        private async Task<string> GenerateUniquePaidPassCodeAsync()
        {
            string passCode;
            bool exists;
            string datePrefix = DateTimeHelper.LibyaNow.ToString("yyyyMMdd");

            do
            {
                string randomHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
                passCode = $"PASS-{datePrefix}-{randomHex}";

                exists = await _db.VisitorPasses.AnyAsync(p => p.PassCode == passCode);
            }
            while (exists);

            return passCode;
        }

        private static string GetSettlementMethodLabel(SettlementMethod method) => method switch
        {
            SettlementMethod.Cash => "دفع كاش نقدي 💵",
            SettlementMethod.BankTransfer => "حوالة بنكية صادرة 🏛️",
            SettlementMethod.RentDeduction => "خصم دائن من قيمة الإيجار ⚖️",
            _ => "طريقة تسوية عامة"
        };
    }
}
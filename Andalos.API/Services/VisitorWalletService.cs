using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.DTOs.Visitors;
using Andalos.API.Enums;
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

        public VisitorWalletService(AppDbContext db, ISettingService settings, ITenantAccountService tenantAccountService)
        {
            _db = db;
            _settings = settings;
            _tenantAccountService = tenantAccountService;
        }

        // =====================================================
        // 1. البوابة: إصدار تصريح مدفوع برصيد + تحديث شفت الحارس
        // =====================================================
        public async Task<VisitorPassResponseDto> CreatePaidPassAsync(CreatePaidVisitorPassDto dto, int gatekeeperUserId)
        {
            // قراءة السعر من الإعدادات إذا لم يُحدد
            decimal passPrice = dto.Amount > 0
                ? dto.Amount
                : await _settings.GetValueAsync<decimal>(SettingKeys.VisitorDefaultValidity, 50);

            string passCode = await GenerateUniquePaidPassCodeAsync();

            var pass = new VisitorPass
            {
                PassCode = passCode,
                VisitorName = dto.VisitorName,
                VisitorPhone = dto.VisitorPhone,
                NationalId = dto.NationalId,
                VisitorType = VisitorType.Customer,
                ValidDate = DateTime.Today,
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
                    StartTime = DateTime.UtcNow,
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
                shift.UpdatedAt = DateTime.UtcNow;
            }

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

        // =====================================================
        // 2. المحل: الخصم بالـ QR Code وحساب الفرق الكاش
        // =====================================================
        public async Task<PassPurchaseResultDto> ProcessShopPurchaseAsync(ProcessPassPurchaseDto dto, int tenantId)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);
            if (tenant == null)
                return new PassPurchaseResultDto { IsSuccess = false, Message = "❌ حساب المحل غير موجود بالنظام" };

            var pass = await _db.VisitorPasses
                .FirstOrDefaultAsync(p => p.PassCode == dto.PassCode && p.IsActive);

            if (pass == null)
                return new PassPurchaseResultDto { IsSuccess = false, Message = "❌ رمز الباركود غير صحيح أو غير موجود" };

            if (!pass.IsPaidPass)
                return new PassPurchaseResultDto { IsSuccess = false, Message = "❌ هذا التصريح مجاني ولا يحتوي على محفظة مالية" };

            if (pass.ValidDate.Date != DateTime.Today)
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

            pass.UpdatedAt = DateTime.UtcNow;

            // تسجيل الحركة لصالح المحل
            var transaction = new PassTransaction
            {
                VisitorPassId = pass.Id,
                TenantId = tenantId,
                UnitId = dto.UnitId,
                Amount = chargedAmount,
                TransactionDate = DateTime.Now,
                IsSettled = false
            };

            _db.PassTransactions.Add(transaction);
            await _db.SaveChangesAsync();

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
            var tenants = await _db.Tenants.Where(t => t.IsActive).ToListAsync();
            var result = new List<TenantPassBalanceDto>();

            foreach (var tenant in tenants)
            {
                var dto = await GetMyUnsettledBalanceAsync(tenant.Id);
                if (dto.TotalUnsettledAmount > 0)
                {
                    result.Add(dto);
                }
            }

            return result.OrderByDescending(r => r.TotalUnsettledAmount).ToList();
        }

        // =====================================================
        // 4. الإدارة: تسديد مستحقات المحل (كاش / تحويل / خصم من الإيجار!)
        // =====================================================
        public async Task<SettlementResponseDto> SettleShopBalanceAsync(ProcessSettlementDto dto, int adminUserId)
        {
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
                SettlementDate = DateTime.Now,
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
                trans.UpdatedAt = DateTime.UtcNow;
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
                    StartTime = DateTime.Now,
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

        public async Task<bool> HandoverShiftCashAsync(int shiftId, int adminUserId)
        {
            var shift = await _db.GatekeeperShifts.FirstOrDefaultAsync(s => s.Id == shiftId && s.IsActive);
            if (shift == null || shift.IsHandedOver) return false;

            shift.IsHandedOver = true;
            shift.HandedOverAt = DateTime.Now;
            shift.EndTime = DateTime.Now;
            shift.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return true;
        }

        // =====================================================
        // 6. Scheduler: تصفير الأرصدة المنتهية بنهاية اليوم
        // =====================================================
        public async Task<decimal> ExpireUnusedBalancesAsync()
        {
            var today = DateTime.Today;

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
                pass.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return totalForfeited; // إرجاع إجمالي المبالغ المتبقية التي أصبحت أرباحاً صافية للإدارة
        }

        private async Task<string> GenerateUniquePaidPassCodeAsync()
        {
            string passCode;
            bool exists;
            string datePrefix = DateTime.Now.ToString("yyyyMMdd");

            do
            {
                string randomHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
                passCode = $"PASS-{datePrefix}-{randomHex}";

                exists = await _db.VisitorPasses.AnyAsync(p => p.PassCode == passCode);
            }
            while (exists);

            return passCode;
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

            // 1) التصاريح المدفوعة (تفاصيل الكاش المستلم)
            var passesQuery = _db.VisitorPasses
                .Include(p => p.IssuedByUser)
                .Where(p => p.IsActive && p.IsPaidPass);

            if (gatekeeperUserId.HasValue)
                passesQuery = passesQuery.Where(p => p.IssuedByUserId == gatekeeperUserId.Value);

            if (from.HasValue)
                passesQuery = passesQuery.Where(p => p.CreatedAt >= from.Value);

            if (to.HasValue)
                passesQuery = passesQuery.Where(p => p.CreatedAt <= to.Value);

            var paidPasses = await passesQuery
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var receipts = paidPasses.Select(p => new GateCashReceiptDetailDto
            {
                PassId = p.Id,
                PassCode = p.PassCode,
                VisitorName = p.VisitorName,
                VisitorPhone = p.VisitorPhone,
                AmountCollected = p.InitialBalance,
                IssuedAt = p.CreatedAt,
                ValidDate = p.ValidDate,
                IssuedByUserId = p.IssuedByUserId,
                GatekeeperName = p.IssuedByUser?.FullName ?? "حارس غير معروف",
                Purpose = p.Purpose,
                WalletStatus = p.WalletStatus.ToString(),
                RemainingBalance = p.RemainingBalance
            }).ToList();

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
                HandedOverAt = s.HandedOverAt
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
    }
}
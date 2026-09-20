using Andalos.API.DTOs.Visitors;

namespace Andalos.API.Interfaces
{
    public interface IVisitorWalletService
    {
        // 1. البوابة: إصدار تصريح مدفوع بـ 50 دينار وتحديث شفت الحارس
        Task<VisitorPassResponseDto> CreatePaidPassAsync(CreatePaidVisitorPassDto dto, int gatekeeperUserId);

        // 2. المحل: خصم الشراء بالـ QR Code (يتدبر فرق الكاش إن كانت المشتريات أكثر)
        Task<PassPurchaseResultDto> ProcessShopPurchaseAsync(ProcessPassPurchaseDto dto, int tenantId);

        // 3. المحل: استعراض مبيعاتي بالـ QR بانتظار التسديد من الإدارة
        Task<TenantPassBalanceDto> GetMyUnsettledBalanceAsync(int tenantId);

        // 4. الإدارة: استعراض مستحقات جميع المحلات من مبيعات الزوار
        Task<List<TenantPassBalanceDto>> GetAllShopsUnsettledBalancesAsync();

        // 5. الإدارة: تسديد مستحقات المحل (نقداً / تحويل / خصم من الإيجار)
        Task<SettlementResponseDto> SettleShopBalanceAsync(ProcessSettlementDto dto, int adminUserId);

        // 6. الحراس: جلب ملخص الشفت الحالي وتسليم العهدة للإدارة
        Task<GatekeeperShiftSummaryDto> GetCurrentShiftSummaryAsync(int gatekeeperUserId);
        Task<bool> HandoverShiftCashAsync(int shiftId, int adminUserId);

        // 7. Scheduler: تصفير الأرصدة المنتهية بنهاية اليوم وتحويل المتبقي للإدارة
        Task<decimal> ExpireUnusedBalancesAsync();
        Task<GateCashReportSummaryDto> GetGateCashReportAsync(
    int? gatekeeperUserId,
    DateTime? fromDate,
    DateTime? toDate,
    bool? isHandedOver);
        // جلب سجل مبيعات وحركات المحفظة التفصيلي لمستأجر معين (مع الفلاتر)
        Task<TenantWalletFullHistoryDto> GetTenantWalletHistoryAsync(int tenantId, DateTime? fromDate, DateTime? toDate, bool? isSettled);
        Task<List<PassTransactionDetailDto>> GetPassTransactionsReportAsync(int? tenantId, int? unitId, DateTime? fromDate, DateTime? toDate, bool? isSettled);
    }
}
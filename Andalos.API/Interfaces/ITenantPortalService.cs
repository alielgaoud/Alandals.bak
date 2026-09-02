using Andalos.API.DTOs.Contracts;
using Andalos.API.DTOs.Maintenance;
using Andalos.API.DTOs.Payments;
using Andalos.API.DTOs.Portal;
using Andalos.API.DTOs.Visitors;

namespace Andalos.API.Interfaces
{
    public interface ITenantPortalService
    {
        Task<TenantAccountStatementDto> GetMyStatementAsync(int tenantId);
        Task<List<ContractResponseDto>> GetMyContractsAsync(int tenantId);
        Task<List<PaymentResponseDto>> GetMyPaymentsAsync(int tenantId);
        Task<MaintenanceResponseDto> RequestMaintenanceAsync(int tenantId, TenantCreateMaintenanceDto dto);
        Task<VisitorPassResponseDto> CreateVisitorPassAsync(int tenantId, TenantCreatePassDto dto, string createdBy);
        Task<List<VisitorPassResponseDto>> GetMyVisitorPassesAsync(int tenantId);
        Task<bool> CreateTenantUserAccountAsync(CreateTenantUserAccountDto dto);
        Task<List<MaintenanceResponseDto>> GetMyMaintenanceAsync(int tenantId);
        Task<MaintenanceResponseDto> GetMaintenanceByIdAsync(int tenantId, int requestId);
    }
}
using Andalos.API.DTOs.Contracts;
using Andalos.API.DTOs.Maintenance;
using Andalos.API.DTOs.Payments;
using Andalos.API.DTOs.Tenants;
using Andalos.API.DTOs.Units;
using Andalos.API.DTOs.Visitors;

namespace Andalos.API.Interfaces
{
    public interface ITenantService
    {
        // CRUD الأساسي
        Task<List<TenantResponseDto>> GetAllAsync();
        Task<TenantResponseDto?> GetByIdAsync(int id);
        Task<TenantResponseDto> CreateAsync(CreateTenantDto dto);
        Task<TenantResponseDto?> UpdateAsync(int id, UpdateTenantDto dto);
        Task<bool> DeleteAsync(int id);

        // 👈 7 Endpoints منفصلة لكل قسم
        Task<TenantResponseDto?> GetPersonalInfoAsync(int tenantId);
        Task<List<UnitResponseDto>> GetRentedUnitsAsync(int tenantId);
        Task<List<ContractResponseDto>> GetContractsAsync(int tenantId);
        Task<List<PaymentResponseDto>> GetPaymentsAsync(int tenantId);
        Task<List<MaintenanceResponseDto>> GetMaintenanceRequestsAsync(int tenantId);
        Task<List<VisitorPassResponseDto>> GetVisitorPassesAsync(int tenantId);
        Task<TenantFinancialSummaryDto?> GetFinancialSummaryAsync(int tenantId);
    }
}
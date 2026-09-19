using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Units
{
    public class CreateUnitDto
    {
        [Required(ErrorMessage = "رقم المحل مطلوب")]
        [MaxLength(20)]
        public string UnitNumber { get; set; } = string.Empty;

        public decimal Area { get; set; } = 0;

        public string? Floor { get; set; }

        public string? Building { get; set; }

        public string? Description { get; set; }

        public string? Notes { get; set; }

        public decimal? ElectricityMeterStart { get; set; }
    }

    public class UpdateUnitDto
    {
        public UnitStatus Status { get; set; }

        public decimal Area { get; set; }

        public string? Floor { get; set; }

        public string? Building { get; set; }

        public string? Description { get; set; }

        public string? Notes { get; set; }

        public decimal? ElectricityMeterStart { get; set; }
    }

    public class UnitResponseDto
    {
        public int Id { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Area { get; set; }
        public string? Floor { get; set; }
        public string? Building { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }
        public decimal? ElectricityMeterStart { get; set; }
        public DateTime CreatedAt { get; set; }
    }// DTO سجل المحل الكامل
    public class UnitHistoryDto
    {
        // 1. بيانات المحل الأساسية
        public UnitResponseDto UnitInfo { get; set; } = new();

        // 2. المستأجر والعقد الحالي (إن وجد)
        public UnitCurrentTenantDto? CurrentOccupant { get; set; }

        // 3. الملخص المالي الإجمالي للمحل
        public UnitFinancialStatsDto FinancialSummary { get; set; } = new();

        // 4. سجل العقود والمستأجرين السابقين والأنشطة
        public List<UnitContractHistoryDto> ContractHistory { get; set; } = new();

        // 5. سجل الصيانة والإصلاحات
        public List<UnitMaintenanceHistoryDto> MaintenanceHistory { get; set; } = new();

        // 6. سجل المصروفات المقيدة على المحل
        public List<UnitExpenseHistoryDto> ExpenseHistory { get; set; } = new();
    }

    public class UnitCurrentTenantDto
    {
        public int ContractId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string TenantPhone { get; set; } = string.Empty;
        public string? TradeName { get; set; } // الاسم التجاري
        public string ActivityType { get; set; } = string.Empty;
        public decimal MonthlyRent { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class UnitContractHistoryDto
    {
        public int ContractId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public string TenantPhone { get; set; } = string.Empty;
        public string? TradeName { get; set; }
        public string ActivityType { get; set; } = string.Empty;
        public decimal MonthlyRent { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? ParentContractId { get; set; }
    }

    public class UnitMaintenanceHistoryDto
    {
        public int Id { get; set; }
        public string RequestNumber { get; set; } = string.Empty;
        public string? TenantName { get; set; }
        public string Type { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime RequestDate { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class UnitExpenseHistoryDto
    {
        public int Id { get; set; }
        public string ExpenseNumber { get; set; } = string.Empty;
        public string? TenantName { get; set; }
        public bool IsChargedToTenant { get; set; }
        public string ExpenseType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class UnitFinancialStatsDto
    {
        public decimal TotalRevenueGenerated { get; set; } // إجمالي الإيرادات المقبوضة من هذا المحل
        public decimal TotalMaintenanceCost { get; set; }   // إجمالي تكاليف الصيانة
        public decimal TotalDirectExpenses { get; set; }    // إجمالي المصروفات المباشرة
        public decimal NetProfitGenerated { get; set; }     // صافي الأرباح (الإيرادات - المصروفات والصيانة)
        public int TotalContractsCount { get; set; }        // عدد العقود التي أُنشئت لهذا المحل
    }
}
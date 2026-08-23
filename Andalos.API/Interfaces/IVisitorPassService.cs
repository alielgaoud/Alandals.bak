using Andalos.API.DTOs.Visitors;

namespace Andalos.API.Interfaces
{
    public interface IVisitorPassService
    {
        Task<VisitorPassResponseDto> CreatePassAsync(CreateVisitorPassDto dto, string createdBy);
        Task<VisitorPassResponseDto?> GetByIdAsync(int id);
        Task<VisitorPassResponseDto?> GetByCodeAsync(string passCode);
        Task<List<VisitorPassResponseDto>> GetAllAsync(DateTime? date, int? unitId);
        Task<ScanResultDto> ScanAndValidatePassAsync(ScanPassDto dto, string scannedBy);
        Task<bool> RevokePassAsync(int id);
        Task<List<EntryLogResponseDto>> GetEntryLogsAsync(DateTime? date);
    }
}
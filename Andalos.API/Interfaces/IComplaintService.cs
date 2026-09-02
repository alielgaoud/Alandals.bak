using Andalos.API.DTOs.Complaints;

namespace Andalos.API.Interfaces
{
    public interface IComplaintService
    {
        // للمستأجر
        Task<TenantComplaintDto> SubmitComplaintAsync(int tenantId, int userId, CreateComplaintDto dto);
        Task<List<TenantComplaintDto>> GetTenantComplaintsAsync(int tenantId);

        // للإدارة
        Task<List<ComplaintResponseDto>> GetAllComplaintsAsync(int? tenantId = null, string? status = null);
        Task<ComplaintResponseDto?> GetComplaintByIdAsync(int id);
        Task<ComplaintReplyDto> ReplyToComplaintAsync(int complaintId, int adminUserId, CreateReplyDto dto);
        Task<bool> UpdateStatusAsync(int complaintId, string status);
    }
}
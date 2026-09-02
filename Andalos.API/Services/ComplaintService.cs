using Andalos.API.Data;
using Andalos.API.DTOs.Complaints;
using Andalos.API.Enums;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public class ComplaintService : IComplaintService
    {
        private readonly AppDbContext _db;

        public ComplaintService(AppDbContext db)
        {
            _db = db;
        }

        // ===== للمستأجر: إرسال شكوى =====
        public async Task<TenantComplaintDto> SubmitComplaintAsync(int tenantId, int userId, CreateComplaintDto dto)
        {
            var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == tenantId && t.IsActive);
            if (!tenantExists)
                throw new KeyNotFoundException("المستأجر غير موجود");

            var complaint = new Complaint
            {
                Subject = dto.Subject,
                Description = dto.Description,
                TenantId = tenantId,
                UserId = userId,
                Status = ComplaintStatus.New,
                SubmittedAt = DateTime.Now
            };

            _db.Complaints.Add(complaint);
            await _db.SaveChangesAsync();

            return new TenantComplaintDto
            {
                Id = complaint.Id,
                Subject = complaint.Subject,
                Description = complaint.Description,
                Status = complaint.Status.ToString(),
                SubmittedAt = complaint.SubmittedAt,
                Replies = new List<ComplaintReplyDto>()
            };
        }

        // ===== للمستأجر: عرض شكاويه =====
        public async Task<List<TenantComplaintDto>> GetTenantComplaintsAsync(int tenantId)
        {
            return await _db.Complaints
                .Include(c => c.Replies)
                    .ThenInclude(r => r.RepliedByUser)
                .Where(c => c.TenantId == tenantId && c.IsActive)
                .OrderByDescending(c => c.SubmittedAt)
                .Select(c => new TenantComplaintDto
                {
                    Id = c.Id,
                    Subject = c.Subject,
                    Description = c.Description,
                    Status = c.Status.ToString(),
                    SubmittedAt = c.SubmittedAt,
                    ResolvedAt = c.ResolvedAt,
                    Replies = c.Replies
                        .OrderBy(r => r.RepliedAt)
                        .Select(r => new ComplaintReplyDto
                        {
                            Id = r.Id,
                            RepliedByName = r.RepliedByUser != null ? r.RepliedByUser.FullName : "الإدارة",
                            ReplyText = r.ReplyText,
                            RepliedAt = r.RepliedAt
                        }).ToList()
                })
                .ToListAsync();
        }

        // ===== للإدارة: عرض كل الشكاوى =====
        public async Task<List<ComplaintResponseDto>> GetAllComplaintsAsync(int? tenantId = null, string? status = null)
        {
            var query = _db.Complaints
                .Include(c => c.Tenant)
                .Include(c => c.User)
                .Include(c => c.Replies)
                    .ThenInclude(r => r.RepliedByUser)
                .Where(c => c.IsActive);

            if (tenantId.HasValue)
                query = query.Where(c => c.TenantId == tenantId.Value);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ComplaintStatus>(status, true, out var parsedStatus))
                query = query.Where(c => c.Status == parsedStatus);

            return await query
                .OrderByDescending(c => c.SubmittedAt)
                .Select(c => new ComplaintResponseDto
                {
                    Id = c.Id,
                    Subject = c.Subject,
                    Description = c.Description,
                    TenantId = c.TenantId,
                    TenantName = c.Tenant != null ? c.Tenant.FullName : "",
                    UserId = c.UserId,
                    UserName = c.User != null ? c.User.UserName : "",
                    Status = c.Status.ToString(),
                    SubmittedAt = c.SubmittedAt,
                    ResolvedAt = c.ResolvedAt,
                    RepliesCount = c.Replies.Count,
                    Replies = c.Replies
                        .OrderBy(r => r.RepliedAt)
                        .Select(r => new ComplaintReplyDto
                        {
                            Id = r.Id,
                            RepliedByName = r.RepliedByUser != null ? r.RepliedByUser.FullName : "الإدارة",
                            ReplyText = r.ReplyText,
                            RepliedAt = r.RepliedAt
                        }).ToList()
                })
                .ToListAsync();
        }

        // ===== للإدارة: عرض شكوى واحدة =====
        public async Task<ComplaintResponseDto?> GetComplaintByIdAsync(int id)
        {
            var c = await _db.Complaints
                .Include(x => x.Tenant)
                .Include(x => x.User)
                .Include(x => x.Replies)
                    .ThenInclude(r => r.RepliedByUser)
                .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);

            if (c == null) return null;

            return new ComplaintResponseDto
            {
                Id = c.Id,
                Subject = c.Subject,
                Description = c.Description,
                TenantId = c.TenantId,
                TenantName = c.Tenant?.FullName ?? "",
                UserId = c.UserId,
                UserName = c.User?.UserName ?? "",
                Status = c.Status.ToString(),
                SubmittedAt = c.SubmittedAt,
                ResolvedAt = c.ResolvedAt,
                RepliesCount = c.Replies.Count,
                Replies = c.Replies
                    .OrderBy(r => r.RepliedAt)
                    .Select(r => new ComplaintReplyDto
                    {
                        Id = r.Id,
                        RepliedByName = r.RepliedByUser != null ? r.RepliedByUser.FullName : "الإدارة",
                        ReplyText = r.ReplyText,
                        RepliedAt = r.RepliedAt
                    }).ToList()
            };
        }

        // ===== للإدارة: الرد على شكوى =====
        public async Task<ComplaintReplyDto> ReplyToComplaintAsync(int complaintId, int adminUserId, CreateReplyDto dto)
        {
            var complaint = await _db.Complaints.FirstOrDefaultAsync(c => c.Id == complaintId && c.IsActive);
            if (complaint == null)
                throw new KeyNotFoundException("الشكوى غير موجودة");

            var adminUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == adminUserId);

            var reply = new ComplaintReply
            {
                ComplaintId = complaintId,
                RepliedByUserId = adminUserId,
                ReplyText = dto.ReplyText,
                RepliedAt = DateTime.Now
            };

            _db.ComplaintReplies.Add(reply);

            if (dto.MarkAsResolved)
            {
                complaint.Status = ComplaintStatus.Resolved;
                complaint.ResolvedAt = DateTime.Now;
            }
            else if (complaint.Status == ComplaintStatus.New)
            {
                complaint.Status = ComplaintStatus.InProgress;
            }

            complaint.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return new ComplaintReplyDto
            {
                Id = reply.Id,
                RepliedByName = adminUser?.FullName ?? "الإدارة",
                ReplyText = reply.ReplyText,
                RepliedAt = reply.RepliedAt
            };
        }

        // ===== للإدارة: تغيير حالة الشكوى =====
        public async Task<bool> UpdateStatusAsync(int complaintId, string status)
        {
            var complaint = await _db.Complaints.FirstOrDefaultAsync(c => c.Id == complaintId && c.IsActive);
            if (complaint == null) return false;

            if (!Enum.TryParse<ComplaintStatus>(status, true, out var parsedStatus))
                return false;

            complaint.Status = parsedStatus;
            if (parsedStatus == ComplaintStatus.Resolved)
                complaint.ResolvedAt = DateTime.Now;

            complaint.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
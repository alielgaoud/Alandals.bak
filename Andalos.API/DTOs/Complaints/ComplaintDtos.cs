using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Complaints
{
    // ===== إنشاء شكوى =====
    public class CreateComplaintDto
    {
        [Required(ErrorMessage = "عنوان الشكوى مطلوب")]
        [MaxLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "تفاصيل الشكوى مطلوبة")]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;
    }

    // ===== عرض الشكوى للإدارة =====
    public class ComplaintResponseDto
    {
        public int Id { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public int RepliesCount { get; set; }
        public List<ComplaintReplyDto> Replies { get; set; } = new();
    }

    // ===== عرض الشكوى للمستأجر =====
    public class TenantComplaintDto
    {
        public int Id { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public List<ComplaintReplyDto> Replies { get; set; } = new();
    }

    // ===== الرد =====
    public class ComplaintReplyDto
    {
        public int Id { get; set; }
        public string RepliedByName { get; set; } = string.Empty;
        public string ReplyText { get; set; } = string.Empty;
        public DateTime RepliedAt { get; set; }
    }

    public class CreateReplyDto
    {
        [Required(ErrorMessage = "نص الرد مطلوب")]
        [MaxLength(2000)]
        public string ReplyText { get; set; } = string.Empty;

        public bool MarkAsResolved { get; set; } = false;
    }
}
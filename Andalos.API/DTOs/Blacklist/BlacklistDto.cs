using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Blacklist
{
    // 1. كائن إضافة زائر للقائمة السوداء
    public class CreateBlacklistDto
    {
        [Required(ErrorMessage = "اسم الزائر مطلوب")]
        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? NationalId { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "سبب الحظر مطلوب")]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public DateTime IncidentDate { get; set; } = DateTime.Now;

        public bool IsPermanent { get; set; } = true;

        public DateTime? ExpiresAt { get; set; }

        public string? Notes { get; set; }

        public IFormFile? Attachment { get; set; } // ملف صورة الهوية
    }

    // 2. كائن استرجاع بيانات القائمة السوداء
    public class BlacklistResponseDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? NationalId { get; set; }
        public string? Phone { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime IncidentDate { get; set; }
        public bool IsPermanent { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsCurrentlyBlocked { get; set; } // هل الحظر فعال حالياً؟
        public string? Notes { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? AttachmentUrl { get; set; }
    }

    // 3. نتيجة فحص الزائر السريع
    public class CheckBlacklistResultDto
    {
        public bool IsBlacklisted { get; set; }
        public string? Reason { get; set; }
        public DateTime? BlockedSince { get; set; }
        public string? MatchType { get; set; } // تطابق تام أم تقريبي بالاسم مع الصورة
    }
}
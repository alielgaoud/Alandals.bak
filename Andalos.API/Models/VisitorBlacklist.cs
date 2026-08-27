using Andalos.API.Common;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class VisitorBlacklist : BaseEntity
    {
        [Required]
        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        // 👈 تم جعله اختيارياً (Nullable)
        [MaxLength(50)]
        public string? NationalId { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "سبب الحظر مطلوب لتوثيق المخالفة")]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public DateTime IncidentDate { get; set; } = DateTime.Now;

        public bool IsPermanent { get; set; } = true;

        public DateTime? ExpiresAt { get; set; }

        [MaxLength(300)]
        public string? Notes { get; set; }

        [MaxLength(500)]
        public string? AttachmentUrl { get; set; }
    }
}
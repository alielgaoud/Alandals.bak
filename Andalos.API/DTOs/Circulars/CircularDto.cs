using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Circulars
{
    public class CreateCircularDto
    {
        [Required(ErrorMessage = "عنوان التعميم مطلوب")]
        [MaxLength(250)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "محتوى التعميم مطلوب")]
        [MaxLength(5000)]
        public string Content { get; set; } = string.Empty;

        public CircularPriority Priority { get; set; } = CircularPriority.Normal;
        public bool IsPinned { get; set; } = false;

        // اختياري: نشر مجدول. إن كان null → ينشر فوراً
        public DateTime? PublishAt { get; set; }

        // اختياري: تاريخ انتهاء
        public DateTime? ExpiresAt { get; set; }
    }
    public class UpdateCircularDto
    {
        [Required]
        [MaxLength(250)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(5000)]
        public string Content { get; set; } = string.Empty;

        public CircularPriority Priority { get; set; } = CircularPriority.Normal;
        public bool IsPinned { get; set; } = false;
        public DateTime? ExpiresAt { get; set; }
    }
    public class CircularResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public bool IsPinned { get; set; }
        public DateTime? PublishAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsPublished { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    public class TenantCircularDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public bool IsPinned { get; set; }
        public DateTime PublishedAt { get; set; }
    }
}
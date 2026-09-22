using Andalos.API.Common;
using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class Circular : BaseEntity
    {
        [Required]
        [MaxLength(250)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(5000)]
        public string Content { get; set; } = string.Empty;

        public CircularPriority Priority { get; set; } = CircularPriority.Normal;

        public bool IsPinned { get; set; } = false;

        // الجدولة (اختيارية): إن لم تُحدد → منشور فوراً
        public DateTime? PublishAt { get; set; }

        // تاريخ الانتهاء (اختياري): يختفي بعده من بوابة المستأجر
        public DateTime? ExpiresAt { get; set; }

        // هل تم نشر التعميم وإرسال إشعاراته؟ (لمنع التكرار في الجدولة)
        public bool IsPublished { get; set; } = false;
        public DateTime? PublishedAt { get; set; }
    }
}
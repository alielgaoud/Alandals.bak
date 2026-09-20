using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        public int? UserId { get; set; } // من قام بالعملية
        public User? User { get; set; }

        [Required]
        [MaxLength(50)]
        public string AuditType { get; set; } = string.Empty; // Create, Update, Delete

        [Required]
        [MaxLength(100)]
        public string TableName { get; set; } = string.Empty; // اسم الجدول (مثال: Contracts)

        [Required]
        [MaxLength(50)]
        public string PrimaryKey { get; set; } = string.Empty; // رقم الـ ID للسجل المعدل

        public string? OldValues { get; set; } // القيم القديمة بصيغة JSON
        public string? NewValues { get; set; } // القيم الجديدة بصيغة JSON
        public string? AffectedColumns { get; set; } // الأعمدة التي تم تغييرها بصيغة JSON

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
using Andalos.API.Common;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class PushSubscription : BaseEntity
    {
        public int? UserId { get; set; }
        public User? User { get; set; }

        public int? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        [Required]
        [MaxLength(500)]
        public string Endpoint { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string P256dh { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Auth { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? DeviceInfo { get; set; }

        [MaxLength(50)]
        public string? BrowserType { get; set; } // Chrome, Firefox, Safari

        public DateTime? LastUsedAt { get; set; }
    }
}
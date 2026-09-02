using Andalos.API.Common;
using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class Complaint : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public int TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        [Required]
        public int UserId { get; set; }
        public User? User { get; set; }

        public ComplaintStatus Status { get; set; } = ComplaintStatus.New;

        public DateTime SubmittedAt { get; set; } = DateTime.Now;

        public DateTime? ResolvedAt { get; set; }

        public ICollection<ComplaintReply> Replies { get; set; } = new List<ComplaintReply>();
    }
}
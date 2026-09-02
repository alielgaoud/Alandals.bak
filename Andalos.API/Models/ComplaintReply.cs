using Andalos.API.Common;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class ComplaintReply : BaseEntity
    {
        [Required]
        public int ComplaintId { get; set; }
        public Complaint? Complaint { get; set; }

        [Required]
        public int RepliedByUserId { get; set; }
        public User? RepliedByUser { get; set; }

        [Required]
        [MaxLength(2000)]
        public string ReplyText { get; set; } = string.Empty;

        public DateTime RepliedAt { get; set; } = DateTime.Now;
    }
}
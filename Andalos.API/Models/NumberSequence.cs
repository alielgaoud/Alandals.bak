using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class NumberSequence
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string SequenceKey { get; set; } = string.Empty;
        // مثال: "Contract", "Receipt", "Maintenance", "Expense"

        public int CurrentYear { get; set; }

        public int LastNumber { get; set; } = 0;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
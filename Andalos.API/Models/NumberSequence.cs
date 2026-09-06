using Andalos.API.Common;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class NumberSequence : BaseEntity
    {

        [Required]
        [MaxLength(100)]
        public string SequenceKey { get; set; } = string.Empty;
        // مثال: "Contract", "Receipt", "Maintenance", "Expense"
        public int LastYear { get; set; }
        public int CurrentYear { get; set; }

        public int LastNumber { get; set; } = 0;

    }
}
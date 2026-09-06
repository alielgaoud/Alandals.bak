using Andalos.API.Common;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class UserPermission : BaseEntity
    {
        [Required]
        public int UserId { get; set; }
        public User? User { get; set; }

        [Required]
        [MaxLength(100)]
        public string PermissionKey { get; set; } = string.Empty; // مثال: "Contracts.Create"
    }
}
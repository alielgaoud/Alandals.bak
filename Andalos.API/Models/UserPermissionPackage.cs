using Andalos.API.Common;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class UserPermissionPackage : BaseEntity
    {
        [Required]
        public int UserId { get; set; }
        public User? User { get; set; }

        [Required]
        public int PackageId { get; set; }
        public PermissionPackage? Package { get; set; }
    }
}
using Andalos.API.Common;
using System.ComponentModel.DataAnnotations;
namespace Andalos.API.Models;
// UserPermissions is a READ-ONLY legacy quarantine. Only this table contains verified direct allows.
public class DirectUserPermission : BaseEntity
{
    public int UserId { get; set; }
    public User? User { get; set; }
    [Required, MaxLength(100)] public string PermissionKey { get; set; } = string.Empty;
}

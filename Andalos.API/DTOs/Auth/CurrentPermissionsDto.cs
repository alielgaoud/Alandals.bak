using System.ComponentModel.DataAnnotations;
namespace Andalos.API.DTOs.Auth;
public sealed class CurrentPermissionsDto
{
    public string Role { get; set; } = "";
    public List<string> Permissions { get; set; } = new();
    public List<string> Modules { get; set; } = new();
    public long PermissionsVersion { get; set; }
    public bool ReconciliationRequired { get; set; }
}
public sealed class ChangeOwnPasswordDto
{
    [Required, MaxLength(128)] public string CurrentPassword { get; set; } = "";
    [Required, MinLength(12), MaxLength(128)] public string NewPassword { get; set; } = "";
}

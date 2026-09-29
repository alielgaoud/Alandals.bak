using System.ComponentModel.DataAnnotations;
namespace Andalos.API.DTOs.Users;
public sealed class ReconcilePermissionsDto
{
    [Required] public long? ExpectedVersion { get; set; }
    [Required] public List<string> DirectPermissions { get; set; } = new();
    [Required] public List<int> PackageIds { get; set; } = new();
    [Required] public List<LegacyPermissionDecision> LegacyDecisions { get; set; } = new();
    [Required, MinLength(10), MaxLength(500)] public string ReviewNote { get; set; } = "";
}
public sealed class LegacyPermissionDecision
{
    public string PermissionKey { get; set; } = "";
    public string Source { get; set; } = ""; // Direct, Package, Remove (no hidden Deny)
}

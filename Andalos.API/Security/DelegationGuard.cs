using Andalos.API.Enums;
using Andalos.API.Models;
namespace Andalos.API.Security;
// Deliberately NOT a role-default grant or an Admin delegation policy.
public sealed class DelegationGuard(CurrentUser current)
{
    public int ActorId => current.UserId;
    public void RequireSuperAdmin(int? targetUserId = null)
    {
        var actor = current.Required;
        if (!actor.IsSuperAdmin || !actor.IsActive || actor.IsLocked || actor.RequiresPasswordChange ||
            targetUserId == actor.Id) throw new ForbiddenOperationException();
    }
    public static void RequireEmployeeTarget(User user)
    {
        if (user.Role is not (UserRole.SuperAdmin or UserRole.Admin or UserRole.Accountant or UserRole.GateKeeper))
            throw new ArgumentException("Employee permissions cannot be assigned to portal accounts.");
    }
    public static void CheckVersion(User user, long? expected)
    {
        if (expected.HasValue && expected.Value != user.PermissionsVersion) throw new ConcurrencyConflictException();
    }
}

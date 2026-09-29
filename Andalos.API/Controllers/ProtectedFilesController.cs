using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
namespace Andalos.API.Controllers;
[ApiController, Route("uploads")]
public sealed class ProtectedFilesController(AppDbContext db, CurrentUser current, EffectivePermissions permissions,
    IWebHostEnvironment env) : ControllerBase
{
    [HttpGet("{**path}")]
    public async Task<IActionResult> Download(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\\') || path.Split('/').Any(p => p is ".." or "." or "")) throw new ForbiddenOperationException();
        var relative = "/uploads/" + path;
        string? key = null; int? owner = null; string? capability = null;
        var receipt = await db.BankTransferRequests.AsNoTracking().Where(r => r.IsActive && r.ReceiptFilePath == relative).Select(r => new { r.TenantId }).FirstOrDefaultAsync();
        if (receipt is not null) { key = Permissions.BankTransfers.View; owner = receipt.TenantId; capability = "payments"; }
        var doc = await db.ProtectedDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.Path == relative);
        if (doc is not null) { key = doc.PermissionKey; owner = doc.TenantId; capability = "statement"; }
        if (key is null)
        {
            var contractDoc = await db.ContractDocuments.AsNoTracking().Where(d => d.IsActive && d.FilePath == relative && d.Contract!.IsActive)
                .Select(d => new { d.Contract!.TenantId }).FirstOrDefaultAsync();
            if (contractDoc is not null) { key = Permissions.Contracts.ExportPdf; owner = contractDoc.TenantId; capability = "contracts"; }
        }
        if (key is null && await db.Expenses.AnyAsync(e => e.IsActive && e.AttachmentUrl == relative)) key = Permissions.Expenses.View;
        if (key is null && await db.VisitorBlacklists.AnyAsync(b => b.IsActive && b.AttachmentUrl == relative)) key = Permissions.Visitors.ManageBlacklist;
        var user = current.Required;
        var allowed = key is not null && user.IsStaff && await permissions.HasAsync(user, key);
        if (!allowed && user.IsTenant && owner == user.TenantId)
            allowed = user.Role == Andalos.API.Enums.UserRole.Tenant || (capability is not null && await db.TenantStaffPermissions.AnyAsync(p => p.UserId == user.Id && p.Capability == capability));
        if (!allowed) throw new ForbiddenOperationException();
        var root = Path.GetFullPath(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"));
        var full = Path.GetFullPath(Path.Combine(root, "uploads", path));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new ForbiddenOperationException();
        // Do not let an uploaded path or deployment symlink escape the storage boundary.
        var probe = root;
        foreach (var segment in ("uploads/" + path).Split('/'))
        {
            probe = Path.Combine(probe, segment);
            if (new FileInfo(probe).LinkTarget is not null || new DirectoryInfo(probe).LinkTarget is not null) throw new ForbiddenOperationException();
        }
        if (!System.IO.File.Exists(full)) return NotFound();
        var provider = new FileExtensionContentTypeProvider();
        provider.TryGetContentType(full, out var type);
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(full, type ?? "application/octet-stream", Path.GetFileName(full));
    }
}

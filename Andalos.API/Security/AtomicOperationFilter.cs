using Andalos.API.Data;
using Andalos.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Andalos.API.Security;
public sealed class IdempotencyRequestMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var rule = context.GetEndpoint()?.Metadata.GetMetadata<EndpointSecurityRule>();
        if (rule?.IsIdempotent == true)
        {
            var key = context.Request.Headers["Idempotency-Key"].ToString();
            if (key.Length is < 8 or > 128 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.')))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { success = false, code = "idempotency_key_required", message = "Idempotency-Key (8-128 ASCII characters) is required." });
                return;
            }
            // Risky commands are small JSON payloads. Never buffer unbounded uploads.
            context.Request.EnableBuffering(64 * 1024, 1024 * 1024);
            using var memory = new MemoryStream();
            try { await context.Request.Body.CopyToAsync(memory, context.RequestAborted); }
            catch (IOException) { context.Response.StatusCode = 413; return; }
            context.Request.Body.Position = 0;
            var prefix = Encoding.UTF8.GetBytes($"{context.Request.Method}\n{context.Request.Path.Value?.ToLowerInvariant()}\n{context.Request.QueryString}\n{context.Request.ContentType}\n");
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(prefix); hash.AppendData(memory.ToArray());
            context.Items["idempotency.key_hash"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
            context.Items["idempotency.request_hash"] = Convert.ToHexString(hash.GetHashAndReset());
        }
        await next(context);
    }
}

// Entire action + all nested SaveChanges + audit + replay receipt share one SQL transaction.
// No transparent write retries: a failed transaction returns 409/503 and the caller retries the SAME key.
public sealed class AtomicOperationFilter(AppDbContext db, CurrentUser current, UserSnapshotReader snapshots,
    IOptions<JsonOptions> json, IUrlHelperFactory urls) : IAsyncActionFilter, IOrderedFilter
{
    public int Order => -1000;
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var rule = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EndpointSecurityRule>();
        var action = (ControllerActionDescriptor)context.ActionDescriptor;
        if (rule is null || rule.IsPublic || HttpMethods.IsGet(context.HttpContext.Request.Method) ||
            HttpMethods.IsHead(context.HttpContext.Request.Method) || action.ControllerName == "Auth" ||
            (action.ControllerName == "Settings" && action.ActionName == "ResetDatabase"))
        { await next(); return; }
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, context.HttpContext.RequestAborted);
        var ct = context.HttpContext.RequestAborted;
        // Close the authorize-to-financial-write race. An in-flight identity change cannot commit a command.
        var live = await snapshots.ReadAsync(current.UserId, ct);
        if (live is null || !live.IsActive || live.IsLocked || live.SecurityStamp != current.Required.SecurityStamp ||
            live.PermissionsVersion != current.Required.PermissionsVersion) throw new ForbiddenOperationException();
        var operation = EndpointSecurityCatalog.Key(action);
        var keyHash = context.HttpContext.Items["idempotency.key_hash"] as string;
        var requestHash = context.HttpContext.Items["idempotency.request_hash"] as string;
        if (rule.IsIdempotent)
        {
            if (keyHash is null || requestHash is null) throw new ArgumentException("Idempotency-Key is required.");
            var receipt = await db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(r => r.UserId == live.Id && r.Operation == operation && r.KeyHash == keyHash, ct);
            if (receipt is not null)
            {
                if (receipt.RequestHash != requestHash) throw new ConcurrencyConflictException("Idempotency key was reused with a different request.");
                context.HttpContext.Response.Headers["Idempotency-Replayed"] = "true";
                if (receipt.Location is not null) context.HttpContext.Response.Headers.Location = receipt.Location;
                context.Result = new ContentResult { Content = receipt.ResponseJson, ContentType = "application/json; charset=utf-8", StatusCode = receipt.StatusCode };
                await tx.CommitAsync(ct); return;
            }
        }
        var executed = await next();
        if (executed.Exception is not null && !executed.ExceptionHandled) return; // dispose rolls back
        var status = executed.Result switch { ObjectResult o => o.StatusCode ?? 200, StatusCodeResult s => s.StatusCode, JsonResult j => j.StatusCode ?? 200, _ => 200 };
        if (status >= 400) return; // even controller-caught errors must roll back partial saves
        if (rule.IsIdempotent)
        {
            object? value = executed.Result switch { ObjectResult o => o.Value, JsonResult j => j.Value, StatusCodeResult => null, _ => throw new InvalidOperationException("Idempotent commands must return JSON.") };
            var location = executed.Result is CreatedAtActionResult created ? urls.GetUrlHelper(context).Action(new UrlActionContext
                { Action = created.ActionName, Controller = created.ControllerName, Values = created.RouteValues, Protocol = context.HttpContext.Request.Scheme }) : null;
            db.IdempotencyRecords.Add(new() { UserId = live.Id, Operation = operation, KeyHash = keyHash!, RequestHash = requestHash!,
                StatusCode = status, ResponseJson = JsonSerializer.Serialize(value, json.Value.JsonSerializerOptions), Location = location });
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}

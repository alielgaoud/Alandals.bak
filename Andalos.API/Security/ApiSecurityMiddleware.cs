using Andalos.API.Data;
using Andalos.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace Andalos.API.Security;
public sealed class ApiSecurityMiddleware(RequestDelegate next, ILogger<ApiSecurityMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var (status, code) = ex switch
            {
                ForbiddenOperationException => (403, "access_denied"),
                UnauthorizedAccessException => (401, "authentication_required"),
                ConcurrencyConflictException or DbUpdateConcurrencyException => (409, "concurrency_conflict"),
                DbUpdateException { InnerException: SqlException sql } when sql.Number is 1205 or 2601 or 2627 => (409, "concurrency_conflict"),
                SqlException sql when sql.Number is 1205 or 2601 or 2627 => (409, "concurrency_conflict"),
                DbException or DbUpdateException => (503, "storage_unavailable"),
                ArgumentException => (400, "invalid_input"),
                KeyNotFoundException => (404, "not_found"),
                InvalidOperationException => (400, "operation_not_allowed"),
                _ => (500, "internal_error")
            };
            if (status >= 500) logger.LogError(ex, "API operation failed ({Code}), trace {Trace}", code, context.TraceIdentifier);
            else logger.LogWarning("API operation rejected ({Code}), trace {Trace}", code, context.TraceIdentifier);
            context.Response.Clear(); context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new { success = false, code, message = status == 400 && ex is ArgumentException ? ex.Message : "تعذر تنفيذ الطلب." });
            if (status is 403 or 409) await context.RequestServices.GetRequiredService<SecurityAuditWriter>().WriteAsync(context, code);
        }
    }
}
public sealed class ClassifiedEndpointMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var ep = context.GetEndpoint();
        if (ep?.Metadata.GetMetadata<ControllerActionDescriptor>() is not null)
        {
            var rule = ep.Metadata.GetMetadata<EndpointSecurityRule>();
            if (rule is null || rule.Scope == IdentityPolicies.Denied ||
                (!rule.IsPublic && ep.Metadata.GetMetadata<IAllowAnonymous>() is not null))
            {
                context.Response.StatusCode = context.User.Identity?.IsAuthenticated == true ? 403 : 401;
                await context.Response.WriteAsJsonAsync(new { success = false, code = "access_denied", message = "غير مصرح." });
                return;
            }
        }
        await next(context);
    }
}
public sealed class SecurityAuditWriter(IServiceScopeFactory scopes, ILogger<SecurityAuditWriter> logger)
{
    public async Task WriteAsync(HttpContext context, string outcome)
    {
        var user = context.RequestServices.GetRequiredService<CurrentUser>().Snapshot;
        var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        logger.LogWarning("Security outcome {Outcome} actor {Actor} endpoint {Endpoint} target {Target} trace {Trace}",
            outcome, user?.Id, action?.DisplayName, context.Request.RouteValues.GetValueOrDefault("id"), context.TraceIdentifier);
        if (user is null || action is null) return;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var target = context.Request.RouteValues.GetValueOrDefault("id")?.ToString() ?? context.Request.RouteValues.GetValueOrDefault("tenantId")?.ToString() ?? "endpoint";
            db.AuditLogs.Add(new AuditLog { UserId = user.Id, TableName = $"{action.ControllerName}.{action.ActionName}"[..Math.Min(100, $"{action.ControllerName}.{action.ActionName}".Length)],
                PrimaryKey = target[..Math.Min(50, target.Length)], AuditType = "Security", Outcome = outcome[..Math.Min(20, outcome.Length)],
                CorrelationId = context.TraceIdentifier, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { logger.LogError(ex, "Could not persist security audit, trace {Trace}; central structured log remains available.", context.TraceIdentifier); }
    }
}
public sealed class ApiAuthorizationResultHandler(SecurityAuditWriter audit) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();
    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult result)
    {
        if (context.Items["authorization.start"] is long start)
            SecurityMetrics.DecisionDuration.Record(System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        if (result.Forbidden)
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { success = false, code = "access_denied", message = "غير مصرح." });
            await audit.WriteAsync(context, "Denied");
            return;
        }
        await _default.HandleAsync(next, context, policy, result);
    }
}

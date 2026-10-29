using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using YeaFlow.Application.Tenancy;
using YeaFlow.Domain.Identity;
using YeaFlow.Infrastructure.Context;
using YeaFlow.Infrastructure.Persistence;

namespace YeaFlow.Api.Infrastructure;

/// <summary>
/// Turns the authenticated principal into the request's execution context. Tenant comes from the token's
/// <c>tenant_id</c> claim; the user must exist in that tenant, be active, and their roles and company access come from
/// the app database, never from the token alone. Operators (Yeasoft staff) carry the <c>yeaflow_operator</c> role.
/// </summary>
public sealed class ExecutionContextMiddleware : IMiddleware
{
    public const string TenantClaim = "tenant_id";
    public const string OperatorRole = "yeaflow_operator";

    private readonly ScopedExecutionContext _context;
    private readonly YeaFlowDbContext _db;

    public ExecutionContextMiddleware(ScopedExecutionContext context, YeaFlowDbContext db)
    {
        _context = context;
        _db = db;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        var http = context;
        ArgumentNullException.ThrowIfNull(next);
        var principal = http.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            await next(http);
            return;
        }

        var correlationId = http.TraceIdentifier;
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        if (subject is null)
        {
            await DenyAsync(http, "The token has no subject.");
            return;
        }

        if (principal.IsInRole(OperatorRole))
        {
            // Operators manage tenants; they get no tenant business data (scope section 8, support access).
            _context.InitialiseForOperator(DeterministicId(subject), correlationId, new HashSet<string> { TenantOnboardingService.OperatorPermission });
            await next(http);
            return;
        }

        if (!Guid.TryParse(principal.FindFirstValue(TenantClaim), out var tenantId))
        {
            await DenyAsync(http, "The token has no tenant.");
            return;
        }

        _context.InitialiseForTenant(tenantId, correlationId);
        var user = await _db.Users
            .Include(u => u.CompanyAccess)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.ExternalSubject == subject, http.RequestAborted);
        if (user is null || !user.IsActive)
        {
            await DenyAsync(http, "No active YeaFlow user for this sign-in.");
            return;
        }

        if (user.RequiresMfa && !HasMfa(principal))
        {
            await DenyAsync(http, "Multi-factor authentication is required for this role.");
            return;
        }

        _context.AttachUser(user.Id, RolePermissions.ForRoles(user.Roles), user.CompanyAccess.Select(a => a.CompanyId).ToHashSet());
        await next(http);
    }

    private static bool HasMfa(ClaimsPrincipal principal) =>
        principal.FindAll("amr").Any(c => c.Value is "mfa" or "otp" or "hwk" or "swk");

    private static Guid DeterministicId(string subject) =>
        new(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(subject)).AsSpan(0, 16));

    private static Task DenyAsync(HttpContext http, string detail) =>
        Results.Problem(title: "Forbidden", detail: detail, statusCode: StatusCodes.Status403Forbidden).ExecuteAsync(http);
}

using YeaFlow.Application.Common;
using YeaFlow.Application.Tenancy;
using YeaFlow.Application.Users;
using YeaFlow.Domain.Tenancy;

namespace YeaFlow.Api.Endpoints;

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/me", (IExecutionContext context) => Results.Ok(new
        {
            context.TenantId,
            context.UserId,
            Permissions = context.Permissions.Order(StringComparer.Ordinal),
            context.CompanyIds,
        })).WithTags("Account");

        var admin = api.MapGroup("/admin").WithTags("Administration");
        admin.MapGet("/seats", async (SeatService seats, CancellationToken ct) => Results.Ok(await seats.GetUsageAsync(ct)));
        admin.MapPost("/users/{id:guid}/activate", async (Guid id, SeatService seats, CancellationToken ct) =>
        {
            await seats.ActivateAsync(id, ct);
            return Results.NoContent();
        });
        admin.MapPost("/users/{id:guid}/disable", async (Guid id, SeatService seats, CancellationToken ct) =>
        {
            await seats.DisableAsync(id, ct);
            return Results.NoContent();
        });

        var platform = api.MapGroup("/platform").WithTags("Operator console");
        platform.MapPost("/tenants", async (CreateTenantRequest request, TenantOnboardingService onboarding, CancellationToken ct) =>
        {
            var tenant = await onboarding.CreateAsync(new CreateTenantCommand(request.Name, request.Slug, request.Package), ct);
            return Results.Created($"/api/v1/platform/tenants/{tenant.Id}", new { tenant.Id, tenant.Name, tenant.Slug, Package = tenant.Package.ToString(), Status = tenant.Status.ToString() });
        });

        return api;
    }

    public sealed record CreateTenantRequest(string Name, string Slug, Package Package);
}

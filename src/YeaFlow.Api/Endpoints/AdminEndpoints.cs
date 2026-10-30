using YeaFlow.Application.Common;
using YeaFlow.Application.Connectors;
using YeaFlow.Application.Tenancy;
using YeaFlow.Application.Users;
using YeaFlow.Domain.Tenancy;
using YeaFlow.Infrastructure.Context;

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

        // Connectors and companies are tenant data: the operator's permission is checked here, then the work runs in the
        // target tenant's scope so row-level security and audit apply to that tenant.
        platform.MapPost("/tenants/{tenantId:guid}/connectors", async (Guid tenantId, RegisterConnectorRequest request, IExecutionContext context, TenantScope scopes, CancellationToken ct) =>
        {
            Authorize.Require(context, TenantOnboardingService.OperatorPermission);
            var registration = await scopes.RunAsync(tenantId, context.CorrelationId, sp =>
                sp.GetRequiredService<ConnectorAdminService>().RegisterConnectorAsync(request.Name, OperatorName(context), ct));
            return Results.Created($"/api/v1/platform/tenants/{tenantId}/connectors/{registration.ConnectorId}", registration);
        });

        platform.MapGet("/tenants/{tenantId:guid}/connectors", async (Guid tenantId, IExecutionContext context, TenantScope scopes, CancellationToken ct) =>
        {
            Authorize.Require(context, TenantOnboardingService.OperatorPermission);
            return Results.Ok(await scopes.RunAsync(tenantId, context.CorrelationId, sp =>
                sp.GetRequiredService<ConnectorAdminService>().ListConnectorsAsync(ct)));
        });

        platform.MapPost("/tenants/{tenantId:guid}/companies", async (Guid tenantId, AddCompanyRequest request, IExecutionContext context, TenantScope scopes, CancellationToken ct) =>
        {
            Authorize.Require(context, TenantOnboardingService.OperatorPermission);
            var company = await scopes.RunAsync(tenantId, context.CorrelationId, sp =>
                sp.GetRequiredService<ConnectorAdminService>().AddCompanyAsync(
                    new AddCompanyCommand(request.Name, request.SapCompanyDb, request.DatabaseType, request.LocalCurrency ?? "KES", request.ConnectorId),
                    OperatorName(context),
                    ct));
            return Results.Created($"/api/v1/platform/tenants/{tenantId}/companies/{company.Id}", new { company.Id, company.Name, company.SapCompanyDb, company.ConnectorId });
        });

        return api;
    }

    private static string OperatorName(IExecutionContext context) => $"operator {context.UserId}";

    public sealed record CreateTenantRequest(string Name, string Slug, Package Package);

    public sealed record RegisterConnectorRequest(string Name);

    public sealed record AddCompanyRequest(string Name, string SapCompanyDb, SapDatabaseType DatabaseType, string? LocalCurrency, Guid ConnectorId);
}

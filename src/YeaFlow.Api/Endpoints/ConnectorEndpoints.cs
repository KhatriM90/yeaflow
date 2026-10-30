using System.Security.Claims;
using YeaFlow.Api.Infrastructure;
using YeaFlow.Infrastructure.Connectors;
using YeaFlow.Sap.Contracts.Connector;

namespace YeaFlow.Api.Endpoints;

/// <summary>Endpoints the on-premises connector calls (D-007: connector → API only, long-poll for work).</summary>
public static class ConnectorEndpoints
{
    public const string Policy = "Connector";

    public static IEndpointRouteBuilder MapConnectorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(RelayRoutes.Prefix).RequireAuthorization(Policy).WithTags("Connector relay");

        group.MapPost("/commands/lease", async (LeaseRequest request, ClaimsPrincipal user, ConnectorRelayHub hub, CancellationToken ct) =>
            Results.Ok(await hub.LeaseAsync(ConnectorId(user), request, ct)));

        group.MapPost("/commands/{id:guid}/result", async (Guid id, CommandResult result, ClaimsPrincipal user, ConnectorRelayHub hub, CancellationToken ct) =>
        {
            await hub.CompleteAsync(ConnectorId(user), id, result, ct);
            return Results.NoContent();
        });

        group.MapPost("/heartbeat", async (ConnectorHeartbeat heartbeat, ClaimsPrincipal user, ConnectorRelayHub hub, CancellationToken ct) =>
        {
            await hub.HeartbeatAsync(ConnectorId(user), heartbeat, ct);
            return Results.NoContent();
        });

        return app;
    }

    private static Guid ConnectorId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ConnectorAuthenticationHandler.ConnectorIdClaim)!);
}

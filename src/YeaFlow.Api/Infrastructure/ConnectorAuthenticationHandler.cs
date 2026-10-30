using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using YeaFlow.Application.Connectors;
using YeaFlow.Infrastructure.Context;
using YeaFlow.Infrastructure.Persistence;
using YeaFlow.Sap.Contracts.Connector;

namespace YeaFlow.Api.Infrastructure;

/// <summary>
/// Authenticates connectors (<c>Authorization: Connector {tenantId}.{connectorId}.{secret}</c>). The lookup runs in the
/// token's tenant scope so row-level security applies; the secret is compared against its stored hash in constant time.
/// </summary>
public sealed class ConnectorAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string ConnectorRole = "yeaflow_connector";
    public const string ConnectorIdClaim = "connector_id";

    private readonly TenantScope _scopes;

    public ConnectorAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, TenantScope scopes)
        : base(options, logger, encoder)
    {
        _scopes = scopes;
    }

    public static bool HasConnectorHeader(HttpContext context) =>
        context.Request.Headers.Authorization.ToString().StartsWith(RelayRoutes.AuthScheme + " ", StringComparison.Ordinal);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!HasConnectorHeader(Context))
        {
            return AuthenticateResult.NoResult();
        }

        var token = Context.Request.Headers.Authorization.ToString()[(RelayRoutes.AuthScheme.Length + 1)..].Trim();
        if (!ConnectorToken.TryParse(token, out var tenantId, out var connectorId, out var secret))
        {
            return AuthenticateResult.Fail("Malformed connector token.");
        }

        var valid = await _scopes.RunAsync(tenantId, "connector-auth", async sp =>
        {
            var connector = await sp.GetRequiredService<YeaFlowDbContext>().Connectors.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == connectorId, Context.RequestAborted);
            return connector?.Verify(secret) == true;
        });

        if (!valid)
        {
            return AuthenticateResult.Fail("Invalid connector credentials.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim("sub", $"connector:{connectorId:N}"),
                new Claim(ExecutionContextMiddleware.TenantClaim, tenantId.ToString()),
                new Claim(ConnectorIdClaim, connectorId.ToString()),
                new Claim("roles", ConnectorRole),
            ],
            Scheme.Name,
            "sub",
            "roles");
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}

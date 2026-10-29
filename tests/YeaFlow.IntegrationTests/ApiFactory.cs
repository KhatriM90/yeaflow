using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace YeaFlow.IntegrationTests;

/// <summary>Hosts the real API against the fixture database, with a header-based test sign-in instead of OIDC.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public ApiFactory(string connectionString) => _connectionString = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:YeaFlow", _connectionString);
        builder.UseSetting("Hangfire:Enabled", "false");
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                o.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}

/// <summary>Reads <c>X-Test-User: subject|tenantId|roles|amr</c>. Test-only; never registered by the API itself.</summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string Header = "X-Test-User";

    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Header, out var value))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var parts = value.ToString().Split('|');
        var claims = new List<Claim> { new("sub", parts[0]) };
        if (parts.Length > 1 && parts[1].Length > 0)
        {
            claims.Add(new Claim("tenant_id", parts[1]));
        }

        if (parts.Length > 2)
        {
            claims.AddRange(parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(r => new Claim("roles", r)));
        }

        if (parts.Length > 3 && parts[3].Length > 0)
        {
            claims.Add(new Claim("amr", parts[3]));
        }

        var identity = new ClaimsIdentity(claims, SchemeName, "sub", "roles");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

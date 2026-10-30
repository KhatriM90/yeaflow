using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using YeaFlow.Api.Demo;
using YeaFlow.Sap.Contracts.Connector;

namespace YeaFlow.IntegrationTests;

/// <summary>Local demo mode: seeded tenant, pick-a-person sign-in and the fixed demo connector. Development only.</summary>
public sealed class DemoModeTests : IClassFixture<PostgresFixture>, IDisposable
{
    private const string Secret = "integration-demo-connector-secret";
    private readonly PostgresFixture _pg;
    private readonly DemoApiFactory? _factory;

    public DemoModeTests(PostgresFixture pg)
    {
        _pg = pg;
        if (pg.Available)
        {
            _factory = new DemoApiFactory(pg.ConnectionString!, "Development");
        }
    }

    public void Dispose() => _factory?.Dispose();

    private sealed class DemoApiFactory(string connectionString, string environment) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("ConnectionStrings:YeaFlow", connectionString);
            builder.UseSetting("Hangfire:Enabled", "false");
            builder.UseSetting("DemoMode:Enabled", "true");
            builder.UseSetting("DemoMode:ConnectorSecret", Secret);
            builder.UseSetting("Relay:CommandTimeout", "00:00:03");
            builder.UseSetting("Relay:PollInterval", "00:00:00.100");
        }
    }

    private async Task<HttpClient> SignedInAsync(string persona)
    {
        var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var response = await client.PostAsJsonAsync(new Uri("/api/demo/sign-in", UriKind.Relative), new { persona });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return client;
    }

    [Fact]
    public async Task SalesPerson_SignsIn_AndSeesSeededCompany()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var me = await (await SignedInAsync("sales")).GetFromJsonAsync<JsonElement>(new Uri("/api/v1/me", UriKind.Relative));

        Assert.Equal("Wanjiru Kamau", me.GetProperty("displayName").GetString());
        var company = Assert.Single(me.GetProperty("companies").EnumerateArray());
        Assert.Equal(DemoMode.CompanyId, company.GetProperty("id").GetGuid());
        Assert.Contains("Sales.Order.Create", me.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));
    }

    [Fact]
    public async Task SeededConnector_CanTakeAndAnswer_ADemoPosting()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var user = await SignedInAsync("sales");
        var connector = _factory!.CreateClient();
        connector.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            RelayRoutes.AuthScheme, $"{DemoMode.TenantId:N}.{DemoMode.ConnectorId:N}.{Secret}");

        var posting = user.PostAsJsonAsync(new Uri("/api/v1/postings", UriKind.Relative), new
        {
            companyId = DemoMode.CompanyId,
            transactionId = $"YF-{Guid.NewGuid()}",
            sapObject = "Orders",
            payload = new { CardCode = "C001" },
            submitNow = true,
        });
        var lease = await connector.PostAsJsonAsync(new Uri(RelayRoutes.Lease, UriKind.Relative), new LeaseRequest(1, 5));
        var command = Assert.Single((await lease.Content.ReadFromJsonAsync<LeaseResponse>())!.Commands);
        Assert.Equal(DemoMode.SapCompanyDb, command.SapCompanyDb);
        await connector.PostAsJsonAsync(new Uri(RelayRoutes.Result(command.Id), UriKind.Relative), new CommandResult(RelayOutcomes.Posted, 1, 100001));

        var result = await (await posting).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Posted", result.GetProperty("state").GetString());
    }

    [Fact]
    public async Task UnknownPersona_IsRejected_AndNoCookieMeansSignedOut()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var client = _factory!.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(new Uri("/api/demo/sign-in", UriKind.Relative), new { persona = "root" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task SignOut_EndsTheSession()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var client = await SignedInAsync("admin");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative))).StatusCode);
        await client.PostAsync(new Uri("/api/demo/sign-out", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative))).StatusCode);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void DemoMode_RefusesToStart_OutsideDevelopment(string environment)
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        using var factory = new DemoApiFactory(_pg.ConnectionString!, environment);
        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("DemoMode is for local development only", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DemoEndpoints_DoNotExist_WhenDemoModeIsOff()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        using var normal = new ApiFactory(_pg.ConnectionString!);
        var response = await normal.CreateClient().PostAsJsonAsync(new Uri("/api/demo/sign-in", UriKind.Relative), new { persona = "sales" });
        Assert.NotEqual(HttpStatusCode.NoContent, response.StatusCode);
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using YeaFlow.Application.Connectors;
using YeaFlow.Domain.Identity;
using YeaFlow.Domain.Tenancy;
using YeaFlow.Sap.Contracts.Connector;

namespace YeaFlow.IntegrationTests;

// D-007: postings reach SAP through a connector that only makes outbound calls to the API.
public sealed class ConnectorRelayTests : IClassFixture<PostgresFixture>, IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);
    private static readonly Uri Postings = new("/api/v1/postings", UriKind.Relative);
    private static readonly Uri Lease = new(RelayRoutes.Lease, UriKind.Relative);
    private readonly PostgresFixture _pg;
    private readonly ApiFactory? _factory;

    public ConnectorRelayTests(PostgresFixture pg)
    {
        _pg = pg;
        if (pg.Available)
        {
            _factory = new ApiFactory(pg.ConnectionString!);
        }
    }

    public void Dispose() => _factory?.Dispose();

    private sealed record Setup(Guid Tenant, Guid Company, HttpClient User, HttpClient Connector, string Token);

    /// <summary>Seeds a tenant with a sales user, then registers a connector through the operator API and assigns it.</summary>
    private async Task<Setup> SetupAsync(bool assignConnector = true)
    {
        var tenant = new Tenant(Guid.NewGuid(), "Acme", $"acme-{Guid.NewGuid():N}"[..20], Package.Starter, Now);
        var company = new Company(Guid.NewGuid(), tenant.Id, "Acme Kenya", $"SBO_{Guid.NewGuid():N}"[..20], SapDatabaseType.SqlServer);
        var subject = $"oidc-{Guid.NewGuid():N}";
        var user = new AppUser(Guid.NewGuid(), tenant.Id, $"{Guid.NewGuid():N}@acme.co.ke", "Wanjiru");
        user.AssignRole(Role.Sales);
        user.LinkExternalSubject(subject);
        user.GrantCompany(company.Id);
        user.Activate(Package.Starter, 0);

        await using (var db = _pg.CreateContext(PostgresFixture.SystemFor(tenant.Id)))
        {
            db.Tenants.Add(tenant);
            db.Companies.Add(company);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var registered = await Operator().PostAsJsonAsync(new Uri($"/api/v1/platform/tenants/{tenant.Id}/connectors", UriKind.Relative), new { name = "Head office" });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var registration = (await registered.Content.ReadFromJsonAsync<ConnectorRegistration>())!;

        if (assignConnector)
        {
            await using var db = _pg.CreateContext(PostgresFixture.SystemFor(tenant.Id));
            var tracked = await db.Companies.FirstAsync(c => c.Id == company.Id);
            tracked.AssignConnector(registration.ConnectorId);
            await db.SaveChangesAsync();
        }

        var userClient = _factory!.CreateClient();
        userClient.DefaultRequestHeaders.Add(TestAuthHandler.Header, $"{subject}|{tenant.Id}||");
        return new Setup(tenant.Id, company.Id, userClient, ConnectorClient(registration.Token), registration.Token);
    }

    private HttpClient Operator()
    {
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.Header, "yeasoft-staff-1||yeaflow_operator|mfa");
        return client;
    }

    private HttpClient ConnectorClient(string token)
    {
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(RelayRoutes.AuthScheme, token);
        return client;
    }

    private static object Order(Setup s, string txn) => new
    {
        companyId = s.Company,
        transactionId = txn,
        sapObject = "Orders",
        payload = new { CardCode = "C001", DocumentLines = new[] { new { ItemCode = "A1", Quantity = 2 } } },
        submitNow = true,
    };

    private static async Task<LeasedCommand> LeaseOneAsync(HttpClient connector)
    {
        var response = await connector.PostAsJsonAsync(Lease, new LeaseRequest(MaxCommands: 1, WaitSeconds: 5));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.Single((await response.Content.ReadFromJsonAsync<LeaseResponse>())!.Commands);
    }

    [Fact]
    public async Task Posting_TravelsThroughConnector_AndComesBackPosted()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var s = await SetupAsync();

        var posting = s.User.PostAsJsonAsync(Postings, Order(s, "TX-RELAY-1"));
        var command = await LeaseOneAsync(s.Connector);
        Assert.Equal(RelayCommandKinds.PostDocument, command.Kind);
        Assert.Equal("TX-RELAY-1", command.TransactionId);
        Assert.Equal("Orders", command.SapObject);
        Assert.Contains("C001", command.Payload, StringComparison.Ordinal);

        var reported = await s.Connector.PostAsJsonAsync(new Uri(RelayRoutes.Result(command.Id), UriKind.Relative), new CommandResult(RelayOutcomes.Posted, 42, 1042));
        Assert.Equal(HttpStatusCode.NoContent, reported.StatusCode);

        // Reporting the same result again (connector retry after a dropped response) is harmless.
        var again = await s.Connector.PostAsJsonAsync(new Uri(RelayRoutes.Result(command.Id), UriKind.Relative), new CommandResult(RelayOutcomes.Posted, 42, 1042));
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);

        var result = await (await posting).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Posted", result.GetProperty("state").GetString());
        Assert.Equal(42, result.GetProperty("sapDocEntry").GetInt32());
        Assert.Equal(1042, result.GetProperty("sapDocNum").GetInt32());
    }

    [Fact]
    public async Task SapRejection_IsShownWithSapError()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var s = await SetupAsync();

        var posting = s.User.PostAsJsonAsync(Postings, Order(s, "TX-RELAY-REJ"));
        var command = await LeaseOneAsync(s.Connector);
        await s.Connector.PostAsJsonAsync(new Uri(RelayRoutes.Result(command.Id), UriKind.Relative),
            new CommandResult(RelayOutcomes.Rejected, ErrorCode: "-5002", ErrorMessage: "Credit limit exceeded"));

        var result = await (await posting).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Failed", result.GetProperty("state").GetString());
        Assert.Contains("Credit limit", result.GetProperty("errorMessage").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConnectorThatNeverPicksUp_LeavesPostingQueued_AndCommandWithdrawn()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var s = await SetupAsync();

        var result = await (await s.User.PostAsJsonAsync(Postings, Order(s, "TX-RELAY-OFFLINE"))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Queued", result.GetProperty("state").GetString());
        Assert.Equal("sap.unavailable", result.GetProperty("errorCode").GetString());

        // The withdrawn command is never handed out later, so it cannot be posted twice.
        var lease = await s.Connector.PostAsJsonAsync(Lease, new LeaseRequest(WaitSeconds: 0));
        Assert.Empty((await lease.Content.ReadFromJsonAsync<LeaseResponse>())!.Commands);
    }

    [Fact]
    public async Task LeasedButUnanswered_IsOutcomeUnknown()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var s = await SetupAsync();

        var posting = s.User.PostAsJsonAsync(Postings, Order(s, "TX-RELAY-LOST"));
        _ = await LeaseOneAsync(s.Connector);

        var result = await (await posting).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("OutcomeUnknown", result.GetProperty("state").GetString());
    }

    [Fact]
    public async Task CompanyWithoutConnector_StaysQueued()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var s = await SetupAsync(assignConnector: false);
        var result = await (await s.User.PostAsJsonAsync(Postings, Order(s, "TX-RELAY-NONE"))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Queued", result.GetProperty("state").GetString());
    }

    [Fact]
    public async Task ConnectorToken_CannotCallUserApi_AndUserCannotCallRelay()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var s = await SetupAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await s.Connector.GetAsync(new Uri("/api/v1/me", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.User.PostAsJsonAsync(Lease, new LeaseRequest(WaitSeconds: 0))).StatusCode);
    }

    [Fact]
    public async Task BadOrForeignToken_IsUnauthorized()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var s = await SetupAsync();
        Assert.True(ConnectorToken.TryParse(s.Token, out _, out var connectorId, out var secret));

        var wrongSecret = ConnectorClient(ConnectorToken.Format(s.Tenant, connectorId, new string('x', 43)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await wrongSecret.PostAsJsonAsync(Lease, new LeaseRequest(WaitSeconds: 0))).StatusCode);

        // The same connector claimed under another tenant is invisible there (row-level security).
        var other = await SetupAsync();
        var foreign = ConnectorClient(ConnectorToken.Format(other.Tenant, connectorId, secret));
        Assert.Equal(HttpStatusCode.Unauthorized, (await foreign.PostAsJsonAsync(Lease, new LeaseRequest(WaitSeconds: 0))).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await ConnectorClient("garbage").PostAsJsonAsync(Lease, new LeaseRequest(WaitSeconds: 0))).StatusCode);
    }

    [Fact]
    public async Task Connector_CannotReportOnAnotherConnectorsCommand()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var s = await SetupAsync();
        var intruder = await SetupAsync();

        var posting = s.User.PostAsJsonAsync(Postings, Order(s, "TX-RELAY-X"));
        var command = await LeaseOneAsync(s.Connector);
        var hijack = await intruder.Connector.PostAsJsonAsync(new Uri(RelayRoutes.Result(command.Id), UriKind.Relative), new CommandResult(RelayOutcomes.Posted, 1, 1));
        Assert.Equal(HttpStatusCode.NotFound, hijack.StatusCode);

        await s.Connector.PostAsJsonAsync(new Uri(RelayRoutes.Result(command.Id), UriKind.Relative), new CommandResult(RelayOutcomes.Posted, 5, 5));
        Assert.Equal("Posted", (await (await posting).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("state").GetString());
    }

    [Fact]
    public async Task Heartbeat_MarksConnectorOnline_ForOperator()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var s = await SetupAsync();

        var beat = await s.Connector.PostAsJsonAsync(new Uri(RelayRoutes.Heartbeat, UriKind.Relative),
            new ConnectorHeartbeat(Guid.Empty, "1.2.3", DateTimeOffset.UtcNow, []));
        Assert.Equal(HttpStatusCode.NoContent, beat.StatusCode);

        var list = await Operator().GetFromJsonAsync<JsonElement>(new Uri($"/api/v1/platform/tenants/{s.Tenant}/connectors", UriKind.Relative));
        var connector = list.EnumerateArray().Single();
        Assert.True(connector.GetProperty("online").GetBoolean());
        Assert.Equal("1.2.3", connector.GetProperty("version").GetString());
    }

    [Fact]
    public async Task Operator_AddsCompany_OnRegisteredConnector()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var s = await SetupAsync(assignConnector: false);
        Assert.True(ConnectorToken.TryParse(s.Token, out _, out var connectorId, out _));
        var db = $"SBO_{Guid.NewGuid():N}"[..20];
        var uri = new Uri($"/api/v1/platform/tenants/{s.Tenant}/companies", UriKind.Relative);
        var body = new { name = "Acme Uganda", sapCompanyDb = db, databaseType = "SqlServer", connectorId };

        Assert.Equal(HttpStatusCode.Created, (await Operator().PostAsJsonAsync(uri, body)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Operator().PostAsJsonAsync(uri, body)).StatusCode);
        var unknownConnector = new { name = "Acme Rwanda", sapCompanyDb = $"SBO_{Guid.NewGuid():N}"[..20], databaseType = "SqlServer", connectorId = Guid.NewGuid() };
        Assert.Equal(HttpStatusCode.NotFound, (await Operator().PostAsJsonAsync(uri, unknownConnector)).StatusCode);
    }
}

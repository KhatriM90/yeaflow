using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using YeaFlow.Domain.Identity;
using YeaFlow.Domain.Tenancy;

namespace YeaFlow.IntegrationTests;

public sealed class ApiTests : IClassFixture<PostgresFixture>, IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 8, 0, 0, TimeSpan.Zero);
    private readonly PostgresFixture _pg;
    private readonly ApiFactory? _factory;

    public ApiTests(PostgresFixture pg)
    {
        _pg = pg;
        if (pg.Available)
        {
            _factory = new ApiFactory(pg.ConnectionString!);
        }
    }

    public void Dispose() => _factory?.Dispose();

    private sealed record Seeded(Guid Tenant, Guid Company, string Subject);

    private async Task<Seeded> SeedUserAsync(Package package = Package.Starter, params string[] roles)
    {
        var tenant = new Tenant(Guid.NewGuid(), "Acme", $"acme-{Guid.NewGuid():N}"[..20], package, Now);
        var company = new Company(Guid.NewGuid(), tenant.Id, "Acme Kenya", "SBO_ACME", SapDatabaseType.SqlServer);
        var subject = $"oidc-{Guid.NewGuid():N}";
        var user = new AppUser(Guid.NewGuid(), tenant.Id, $"{Guid.NewGuid():N}@acme.co.ke", "Wanjiru");
        foreach (var role in roles)
        {
            user.AssignRole(role);
        }

        user.LinkExternalSubject(subject);
        user.GrantCompany(company.Id);
        user.Activate(package, 0);

        await using var db = _pg.CreateContext(PostgresFixture.SystemFor(tenant.Id));
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return new Seeded(tenant.Id, company.Id, subject);
    }

    private HttpClient ClientFor(Seeded who, string roles = "", string amr = "")
    {
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.Header, $"{who.Subject}|{who.Tenant}|{roles}|{amr}");
        return client;
    }

    [Fact]
    public async Task Health_IsAnonymous()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var client = _factory!.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/health/live", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/health/ready", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Api_RequiresSignIn()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var response = await _factory!.CreateClient().GetAsync(new Uri("/api/v1/me", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnknownUser_IsForbidden()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var stranger = new Seeded(Guid.NewGuid(), Guid.NewGuid(), "nobody");
        var response = await ClientFor(stranger).GetAsync(new Uri("/api/v1/me", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsPermissionsFromAppDatabase()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var who = await SeedUserAsync(Package.Starter, Role.Sales);
        var me = await ClientFor(who).GetFromJsonAsync<JsonElement>(new Uri("/api/v1/me", UriKind.Relative));
        var permissions = me.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ToList();
        Assert.Contains("Sales.Order.Create", permissions);
        Assert.Equal(who.Company, me.GetProperty("companyIds")[0].GetGuid());
    }

    [Fact]
    public async Task PrivilegedRole_WithoutMfa_IsForbidden()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var who = await SeedUserAsync(Package.Starter, Role.AppAdministrator);
        Assert.Equal(HttpStatusCode.Forbidden, (await ClientFor(who).GetAsync(new Uri("/api/v1/me", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ClientFor(who, amr: "mfa").GetAsync(new Uri("/api/v1/me", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task CreatePosting_DuplicateClick_ReturnsSameIntent_AndStaysPendingWithoutConnector()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var who = await SeedUserAsync(Package.Starter, Role.Sales);
        var client = ClientFor(who);
        var body = new
        {
            companyId = who.Company,
            transactionId = "TX-API-1",
            sapObject = "Orders",
            payload = new { CardCode = "C001", DocumentLines = new[] { new { ItemCode = "A1", Quantity = 2 } } },
            submitNow = true,
        };

        var first = await client.PostAsJsonAsync(new Uri("/api/v1/postings", UriKind.Relative), body);
        var second = await client.PostAsJsonAsync(new Uri("/api/v1/postings", UriKind.Relative), body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var a = await first.Content.ReadFromJsonAsync<JsonElement>();
        var b = await second.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(a.GetProperty("id").GetGuid(), b.GetProperty("id").GetGuid());

        // No connector yet: never shown as posted; queued for a bounded retry with the reason visible.
        Assert.Equal("Queued", a.GetProperty("state").GetString());
        Assert.Equal("sap.unavailable", a.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task CreatePosting_WithoutPermission_IsProblem403()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var who = await SeedUserAsync(Package.Starter, Role.Warehouse);
        var response = await ClientFor(who).PostAsJsonAsync(new Uri("/api/v1/postings", UriKind.Relative), new
        {
            companyId = who.Company, transactionId = "TX-2", sapObject = "Orders", payload = new { CardCode = "C1" }, submitNow = true,
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task OtherTenantsPosting_IsNotFound()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var owner = await SeedUserAsync(Package.Starter, Role.Sales);
        var created = await ClientFor(owner).PostAsJsonAsync(new Uri("/api/v1/postings", UriKind.Relative), new
        {
            companyId = owner.Company, transactionId = "TX-3", sapObject = "Orders", payload = new { CardCode = "C1" }, submitNow = false,
        });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var other = await SeedUserAsync(Package.Starter, Role.Sales);
        var response = await ClientFor(other).GetAsync(new Uri($"/api/v1/postings/{id}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SeatLimit_RejectsActivationAboveStarterLimit()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var admin = await SeedUserAsync(Package.Starter, Role.AppAdministrator);

        var pending = new List<Guid>();
        await using (var db = _pg.CreateContext(PostgresFixture.SystemFor(admin.Tenant)))
        {
            for (var i = 0; i < 5; i++)
            {
                var user = new AppUser(Guid.NewGuid(), admin.Tenant, $"u{i}-{Guid.NewGuid():N}@acme.co.ke", $"User {i}");
                db.Users.Add(user);
                pending.Add(user.Id);
            }

            await db.SaveChangesAsync();
        }

        var client = ClientFor(admin, amr: "mfa");
        for (var i = 0; i < 4; i++)
        {
            var ok = await client.PostAsync(new Uri($"/api/v1/admin/users/{pending[i]}/activate", UriKind.Relative), null);
            Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        }

        var rejected = await client.PostAsync(new Uri($"/api/v1/admin/users/{pending[4]}/activate", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("seats.limit_reached", problem.GetProperty("code").GetString());

        var usage = await client.GetFromJsonAsync<JsonElement>(new Uri("/api/v1/admin/seats", UriKind.Relative));
        Assert.Equal(5, usage.GetProperty("active").GetInt32());
        Assert.Equal(5, usage.GetProperty("limit").GetInt32());
    }

    [Fact]
    public async Task Operator_CanCreateTenant_ButTenantAdminCannot()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var operatorClient = _factory!.CreateClient();
        operatorClient.DefaultRequestHeaders.Add(TestAuthHandler.Header, "yeasoft-staff-1||yeaflow_operator|mfa");
        var slug = $"new-{Guid.NewGuid():N}"[..20];
        var created = await operatorClient.PostAsJsonAsync(new Uri("/api/v1/platform/tenants", UriKind.Relative), new { name = "New Co", slug, package = "Business" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var admin = await SeedUserAsync(Package.Starter, Role.AppAdministrator);
        var denied = await ClientFor(admin, amr: "mfa").PostAsJsonAsync(new Uri("/api/v1/platform/tenants", UriKind.Relative), new { name = "X", slug = "x-co", package = "Starter" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}

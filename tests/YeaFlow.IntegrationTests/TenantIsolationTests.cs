using Microsoft.EntityFrameworkCore;
using YeaFlow.Application.Common;
using YeaFlow.Domain.Audit;
using YeaFlow.Domain.Posting;
using YeaFlow.Domain.Tenancy;
using YeaFlow.Infrastructure.Context;
using YeaFlow.Infrastructure.Persistence;

namespace YeaFlow.IntegrationTests;

/// <summary>Scope section 9 acceptance: cross-tenant reads are denied; duplicates and stale updates are caught by the database.</summary>
public sealed class TenantIsolationTests : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 8, 0, 0, TimeSpan.Zero);
    private readonly PostgresFixture _pg;

    public TenantIsolationTests(PostgresFixture pg) => _pg = pg;

    private async Task<(Guid Tenant, Guid Company)> SeedTenantAsync()
    {
        var tenant = new Tenant(Guid.NewGuid(), "Tenant", $"t-{Guid.NewGuid():N}"[..20], Package.Starter, Now);
        var company = new Company(Guid.NewGuid(), tenant.Id, "Main", "SBO_MAIN", SapDatabaseType.SqlServer);
        await using var db = _pg.CreateContext(PostgresFixture.SystemFor(tenant.Id));
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return (tenant.Id, company.Id);
    }

    private async Task<PostingIntent> SeedIntentAsync(Guid tenant, Guid company, string txn = "TX-1")
    {
        var intent = PostingIntent.Create(tenant, company, Guid.NewGuid(), txn, "Orders", """{"CardCode":"C1"}""", Now);
        intent.Submit(Now);
        await using var db = _pg.CreateContext(PostgresFixture.SystemFor(tenant));
        db.PostingIntents.Add(intent);
        await db.SaveChangesAsync();
        return intent;
    }

    [Fact]
    public async Task OtherTenant_CannotSeeRows_EvenWithoutEfFilters()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var (tenantA, companyA) = await SeedTenantAsync();
        var (tenantB, _) = await SeedTenantAsync();
        await SeedIntentAsync(tenantA, companyA);

        await using var asB = _pg.CreateContext(PostgresFixture.SystemFor(tenantB));
        Assert.Empty(await asB.PostingIntents.ToListAsync());

        // Row-level security still hides tenant A when the EF query filter is bypassed.
        Assert.Empty(await asB.PostingIntents.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await asB.Companies.IgnoreQueryFilters().Where(c => c.TenantId == tenantA).ToListAsync());

        await using var asA = _pg.CreateContext(PostgresFixture.SystemFor(tenantA));
        Assert.Single(await asA.PostingIntents.ToListAsync());
    }

    [Fact]
    public async Task NoTenantSet_SeesNothing()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var (tenant, company) = await SeedTenantAsync();
        await SeedIntentAsync(tenant, company);

        await using var anonymous = _pg.CreateContext(new ScopedExecutionContext());
        Assert.Empty(await anonymous.PostingIntents.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task CrossTenantWrite_IsBlocked()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var (tenantA, companyA) = await SeedTenantAsync();
        var (tenantB, _) = await SeedTenantAsync();

        await using var asB = _pg.CreateContext(PostgresFixture.SystemFor(tenantB));
        asB.PostingIntents.Add(PostingIntent.Create(tenantA, companyA, Guid.NewGuid(), "TX-X", "Orders", "{}", Now));
        await Assert.ThrowsAsync<InvalidOperationException>(() => asB.SaveChangesAsync());

        // And PostgreSQL rejects it even if the application guard were bypassed.
        await Assert.ThrowsAnyAsync<Exception>(() => asB.Database.ExecuteSqlAsync(
            $"INSERT INTO companies (\"Id\", \"TenantId\", \"Name\", \"SapCompanyDb\", \"DatabaseType\", \"LocalCurrency\", \"IsActive\") VALUES ({Guid.NewGuid()}, {tenantA}, 'x', 'y', 'SqlServer', 'KES', true)"));
    }

    [Fact]
    public async Task DuplicateTransactionId_IsRejectedByDatabase()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var (tenant, company) = await SeedTenantAsync();
        await SeedIntentAsync(tenant, company, "TX-DUP");

        await using var db = _pg.CreateContext(PostgresFixture.SystemFor(tenant));
        db.PostingIntents.Add(PostingIntent.Create(tenant, company, Guid.NewGuid(), "TX-DUP", "Orders", "{}", Now));
        var ex = await Assert.ThrowsAsync<ConflictException>(() => ((IUnitOfWork)db).SaveChangesAsync(CancellationToken.None));
        Assert.Equal("concurrency.duplicate", ex.Code);
    }

    [Fact]
    public async Task TwoWorkers_CannotBothClaimTheSameIntent()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var (tenant, company) = await SeedTenantAsync();
        var seeded = await SeedIntentAsync(tenant, company);

        await using var worker1 = _pg.CreateContext(PostgresFixture.SystemFor(tenant));
        await using var worker2 = _pg.CreateContext(PostgresFixture.SystemFor(tenant));
        var claim1 = await worker1.PostingIntents.SingleAsync(p => p.Id == seeded.Id);
        var claim2 = await worker2.PostingIntents.SingleAsync(p => p.Id == seeded.Id);

        claim1.BeginSubmission(Now);
        claim2.BeginSubmission(Now);
        await ((IUnitOfWork)worker1).SaveChangesAsync(CancellationToken.None);
        await Assert.ThrowsAsync<ConflictException>(() => ((IUnitOfWork)worker2).SaveChangesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task AuditTrail_IsAppendOnly()
    {
        Assert.SkipUnless(_pg.Available, "Set YEAFLOW_PG to run PostgreSQL integration tests.");
        var (tenant, _) = await SeedTenantAsync();
        await using var db = _pg.CreateContext(PostgresFixture.SystemFor(tenant));
        db.AuditEvents.Add(new AuditEvent(tenant, null, null, "test.action", "ok", "corr", Now));
        await db.SaveChangesAsync();

        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("UPDATE audit_events SET \"Result\" = 'tampered'"));
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM audit_events"));
    }
}

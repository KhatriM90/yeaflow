using Microsoft.EntityFrameworkCore;
using Npgsql;
using YeaFlow.Infrastructure.Context;
using YeaFlow.Infrastructure.Persistence;

namespace YeaFlow.IntegrationTests;

/// <summary>
/// Creates a throwaway PostgreSQL database from <c>YEAFLOW_PG</c> (a role allowed to CREATE DATABASE), applies all
/// migrations, and drops it afterwards. Tests skip when the variable is not set.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string? _adminConnection = Environment.GetEnvironmentVariable("YEAFLOW_PG");
    private string? _database;

    public string? ConnectionString { get; private set; }

    public bool Available => ConnectionString is not null;

    public async ValueTask InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_adminConnection))
        {
            return;
        }

        _database = $"yeaflow_test_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(_adminConnection))
        {
            await admin.OpenAsync();

            // Superusers and BYPASSRLS roles skip row-level security, so isolation tests would test nothing.
            await using var check = new NpgsqlCommand("SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = current_user", admin);
            if (await check.ExecuteScalarAsync() is true)
            {
                throw new InvalidOperationException("YEAFLOW_PG must use a role without SUPERUSER or BYPASSRLS (it needs CREATEDB).");
            }

            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        ConnectionString = new NpgsqlConnectionStringBuilder(_adminConnection) { Database = _database }.ConnectionString;
        await using var db = CreateContext(new ScopedExecutionContext());
        await db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_database is null)
        {
            return;
        }

        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    public YeaFlowDbContext CreateContext(ScopedExecutionContext context)
    {
        var options = new DbContextOptionsBuilder<YeaFlowDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(context))
            .Options;
        return new YeaFlowDbContext(options, context);
    }

    public static ScopedExecutionContext SystemFor(Guid tenantId)
    {
        var context = new ScopedExecutionContext();
        context.InitialiseForSystem(tenantId, "test");
        return context;
    }
}

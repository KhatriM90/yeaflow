using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using YeaFlow.Infrastructure.Context;

namespace YeaFlow.Infrastructure.Persistence;

/// <summary>
/// Sets <c>app.tenant_id</c> on every opened connection so PostgreSQL row-level security policies apply.
/// An empty value matches no tenant rows.
/// </summary>
public sealed class TenantConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ScopedExecutionContext _context;

    public TenantConnectionInterceptor(ScopedExecutionContext context) => _context = context;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await using var command = CreateCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.tenant_id', @tenant, false)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenant";
        parameter.Value = _context.HasTenant ? _context.TenantId.ToString() : string.Empty;
        command.Parameters.Add(parameter);
        return command;
    }
}

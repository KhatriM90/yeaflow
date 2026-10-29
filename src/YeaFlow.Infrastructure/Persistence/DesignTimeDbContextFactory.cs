using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace YeaFlow.Infrastructure.Persistence;

/// <summary>Used by <c>dotnet ef</c> only. Connection string from <c>YEAFLOW_PG</c>; no secrets in source.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<YeaFlowDbContext>
{
    public YeaFlowDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("YEAFLOW_PG") ?? "Host=localhost;Database=yeaflow;Username=yeaflow";
        var options = new DbContextOptionsBuilder<YeaFlowDbContext>().UseNpgsql(connection).Options;
        return new YeaFlowDbContext(options);
    }
}

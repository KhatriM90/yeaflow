using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YeaFlow.Application.Postings;
using YeaFlow.Application.Tenancy;
using YeaFlow.Infrastructure.Context;

namespace YeaFlow.Infrastructure.Jobs;

/// <summary>
/// Hangfire entry points. Each tenant runs in its own DI scope with its own execution context, so queries, row-level
/// security and audit records are always scoped to exactly one tenant.
/// </summary>
public sealed class TenantJobRunner
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<TenantJobRunner> _logger;

    public TenantJobRunner(IServiceScopeFactory scopes, ILogger<TenantJobRunner> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public Task SubmitDuePostingsAsync(CancellationToken cancellationToken) =>
        ForEachTenantAsync("postings.submit", (jobs, ct) => jobs.SubmitDueAsync(ct), cancellationToken);

    public Task ReconcilePostingsAsync(CancellationToken cancellationToken) =>
        ForEachTenantAsync("postings.reconcile", (jobs, ct) => jobs.ReconcileUnknownAsync(ct), cancellationToken);

    private async Task ForEachTenantAsync(string jobName, Func<PostingJobs, CancellationToken, Task<int>> work, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> tenantIds;
        await using (var scope = _scopes.CreateAsyncScope())
        {
            tenantIds = await scope.ServiceProvider.GetRequiredService<ITenantRepository>().ListActiveTenantIdsAsync(cancellationToken);
        }

        foreach (var tenantId in tenantIds)
        {
            await using var scope = _scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ScopedExecutionContext>().InitialiseForSystem(tenantId, $"{jobName}:{Guid.NewGuid():N}");
            try
            {
                var count = await work(scope.ServiceProvider.GetRequiredService<PostingJobs>(), cancellationToken);
                if (count > 0)
                {
                    _logger.LogInformation("{Job} processed {Count} postings for tenant {TenantId}", jobName, count, tenantId);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One tenant's failure must not stop the others.
                _logger.LogError(ex, "{Job} failed for tenant {TenantId}", jobName, tenantId);
            }
        }
    }
}

using Microsoft.Extensions.DependencyInjection;

namespace YeaFlow.Infrastructure.Context;

/// <summary>
/// Runs work in a fresh DI scope bound to one tenant as the system actor: its own DbContext, row-level security setting and
/// audit context. Used by background work and by operator actions that target a tenant from outside it.
/// </summary>
public sealed class TenantScope
{
    private readonly IServiceScopeFactory _scopes;

    public TenantScope(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task<T> RunAsync<T>(Guid tenantId, string correlationId, Func<IServiceProvider, Task<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        await using var scope = _scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ScopedExecutionContext>().InitialiseForSystem(tenantId, correlationId);
        return await work(scope.ServiceProvider);
    }
}

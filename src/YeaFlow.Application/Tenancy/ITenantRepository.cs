using YeaFlow.Domain.Tenancy;

namespace YeaFlow.Application.Tenancy;

public interface ITenantRepository
{
    Task<Tenant?> GetAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Loads the tenant and locks its row until the unit of work commits, serialising seat changes.</summary>
    Task<Tenant?> GetForUpdateAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);

    void Add(Tenant tenant);

    Task<IReadOnlyList<Guid>> ListActiveTenantIdsAsync(CancellationToken cancellationToken);
}

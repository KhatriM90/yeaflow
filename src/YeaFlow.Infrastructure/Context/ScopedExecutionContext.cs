using YeaFlow.Application.Common;

namespace YeaFlow.Infrastructure.Context;

/// <summary>
/// Per-scope execution context. The API initialises it from the authenticated principal; background jobs initialise it
/// for one tenant as the system actor. Reading it before initialisation throws, so nothing runs without a tenant.
/// </summary>
public sealed class ScopedExecutionContext : IExecutionContext
{
    private Guid? _tenantId;

    public Guid TenantId => _tenantId ?? throw new InvalidOperationException("No tenant has been set for this scope.");

    public bool HasTenant => _tenantId.HasValue;

    public Guid? UserId { get; private set; }

    public string CorrelationId { get; private set; } = string.Empty;

    public IReadOnlySet<string> Permissions { get; private set; } = new HashSet<string>();

    public IReadOnlySet<Guid> CompanyIds { get; private set; } = new HashSet<Guid>();

    public bool IsSystem { get; private set; }

    /// <summary>
    /// First step for a signed-in user: fixes the tenant (from the token) so the user record can be loaded under
    /// row-level security. Until <see cref="AttachUser"/> runs the scope has no permissions.
    /// </summary>
    public void InitialiseForTenant(Guid tenantId, string correlationId)
    {
        EnsureNotInitialised();
        _tenantId = tenantId;
        CorrelationId = correlationId;
    }

    public void AttachUser(Guid userId, IReadOnlySet<string> permissions, IReadOnlySet<Guid> companyIds)
    {
        if (!_tenantId.HasValue || UserId.HasValue || IsSystem)
        {
            throw new InvalidOperationException("Attach a user only once, after the tenant is set.");
        }

        UserId = userId;
        Permissions = permissions;
        CompanyIds = companyIds;
    }

    /// <summary>Platform operator (Yeasoft staff) acting outside any tenant's business data.</summary>
    public void InitialiseForOperator(Guid userId, string correlationId, IReadOnlySet<string> permissions)
    {
        EnsureNotInitialised();
        UserId = userId;
        CorrelationId = correlationId;
        Permissions = permissions;
    }

    public void InitialiseForSystem(Guid tenantId, string correlationId)
    {
        EnsureNotInitialised();
        _tenantId = tenantId;
        CorrelationId = correlationId;
        IsSystem = true;
    }

    private void EnsureNotInitialised()
    {
        if (_tenantId.HasValue || UserId.HasValue || IsSystem)
        {
            throw new InvalidOperationException("The execution context is already initialised for this scope.");
        }
    }
}

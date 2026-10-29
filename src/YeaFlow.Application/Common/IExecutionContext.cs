namespace YeaFlow.Application.Common;

/// <summary>
/// Who is acting and for which tenant. Resolved from the authenticated user in the API and set explicitly by
/// background jobs, which always run for one tenant at a time. Everything tenant-scoped reads its tenant from here.
/// </summary>
public interface IExecutionContext
{
    Guid TenantId { get; }

    /// <summary>Null for system work such as reconciliation jobs.</summary>
    Guid? UserId { get; }

    string CorrelationId { get; }

    IReadOnlySet<string> Permissions { get; }

    IReadOnlySet<Guid> CompanyIds { get; }

    bool IsSystem { get; }
}

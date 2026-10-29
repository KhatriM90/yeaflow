using YeaFlow.Domain.Common;

namespace YeaFlow.Domain.Audit;

/// <summary>
/// Append-only audit record (scope section 4): actor, tenant, company, time, action, result, correlation ID and SAP reference.
/// The database rejects UPDATE and DELETE on this table.
/// </summary>
public sealed class AuditEvent : ITenantScoped
{
    private AuditEvent()
    {
        Action = string.Empty;
        Result = string.Empty;
        CorrelationId = string.Empty;
    }

    public AuditEvent(Guid tenantId, Guid? companyId, Guid? actorUserId, string action, string result, string correlationId, DateTimeOffset occurredAt, string? sapReference = null, string? detail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(result);
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CompanyId = companyId;
        ActorUserId = actorUserId;
        Action = action;
        Result = result;
        CorrelationId = correlationId;
        OccurredAt = occurredAt;
        SapReference = sapReference;
        Detail = detail;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid? CompanyId { get; private set; }

    /// <summary>Null for system actions (background jobs).</summary>
    public Guid? ActorUserId { get; private set; }

    public string Action { get; private set; }

    public string Result { get; private set; }

    public string CorrelationId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>e.g. <c>Orders/1234</c> (SAP object and DocEntry).</summary>
    public string? SapReference { get; private set; }

    public string? Detail { get; private set; }
}

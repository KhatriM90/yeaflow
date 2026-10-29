namespace YeaFlow.Application.Common;

/// <summary>Appends to the audit trail as part of the current unit of work.</summary>
public interface IAuditLog
{
    void Record(string action, string result, Guid? companyId = null, string? sapReference = null, string? detail = null);
}

public interface IUnitOfWork
{
    /// <summary>Saves pending changes. Throws <see cref="ConflictException"/> when a concurrency token no longer matches.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

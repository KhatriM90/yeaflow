using YeaFlow.Application.Common;
using YeaFlow.Domain.Audit;

namespace YeaFlow.Infrastructure.Persistence;

/// <summary>Adds audit events to the current unit of work so they commit (or roll back) with the change they describe.</summary>
internal sealed class AuditLog : IAuditLog
{
    private readonly YeaFlowDbContext _db;
    private readonly IExecutionContext _context;
    private readonly TimeProvider _time;

    public AuditLog(YeaFlowDbContext db, IExecutionContext context, TimeProvider time)
    {
        _db = db;
        _context = context;
        _time = time;
    }

    public void Record(string action, string result, Guid? companyId = null, string? sapReference = null, string? detail = null) =>
        _db.AuditEvents.Add(new AuditEvent(_context.TenantId, companyId, _context.UserId, action, result, _context.CorrelationId, _time.GetUtcNow(), sapReference, detail));
}

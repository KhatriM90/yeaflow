using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using YeaFlow.Application.Common;
using YeaFlow.Domain.Connectors;
using YeaFlow.Infrastructure.Persistence;
using YeaFlow.Sap.Contracts.Connector;

namespace YeaFlow.Infrastructure.Connectors;

/// <summary>
/// Connector side of the relay (D-007): long-poll lease of pending commands, result reporting and heartbeats.
/// Runs in the connector's tenant scope, so row-level security limits it to that tenant's commands.
/// </summary>
public sealed class ConnectorRelayHub
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(500);

    private readonly YeaFlowDbContext _db;
    private readonly IAuditLog _audit;
    private readonly TimeProvider _time;
    private readonly RelayOptions _options;

    public ConnectorRelayHub(YeaFlowDbContext db, IAuditLog audit, TimeProvider time, IOptions<RelayOptions> options)
    {
        _db = db;
        _audit = audit;
        _time = time;
        _options = options.Value;
    }

    public async Task<LeaseResponse> LeaseAsync(Guid connectorId, LeaseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var max = Math.Clamp(request.MaxCommands, 1, 20);
        var deadline = _time.GetUtcNow() + TimeSpan.FromSeconds(Math.Clamp(request.WaitSeconds, 0, (int)_options.MaxLeaseWait.TotalSeconds));

        while (true)
        {
            var leased = await TryLeaseAsync(connectorId, max, cancellationToken);
            if (leased.Count > 0 || _time.GetUtcNow() >= deadline)
            {
                return new LeaseResponse(leased);
            }

            await Task.Delay(IdleDelay, _time, cancellationToken);
        }
    }

    public async Task CompleteAsync(Guid connectorId, Guid commandId, CommandResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        var command = await _db.ConnectorCommands.FirstOrDefaultAsync(c => c.Id == commandId && c.ConnectorId == connectorId, cancellationToken)
            ?? throw new NotFoundException("Command not found.");
        if (!Enum.TryParse<ConnectorOutcome>(result.Outcome, ignoreCase: false, out var outcome) || !Enum.IsDefined(outcome))
        {
            throw new ConflictException("connector.invalid_outcome", $"Unknown outcome '{result.Outcome}'.");
        }

        if (command.State == ConnectorCommandState.Completed && command.Outcome == outcome)
        {
            // The connector retried a result we already stored.
            return;
        }

        command.Complete(outcome, result.DocEntry, result.DocNum, result.ErrorCode, result.ErrorMessage, _time.GetUtcNow());
        _audit.Record("connector.command_completed", outcome.ToString(), command.CompanyId, command.SapDocEntry is { } e ? $"{command.SapObject}/{e}" : null, command.TransactionId);
        await ((IUnitOfWork)_db).SaveChangesAsync(cancellationToken);
    }

    public async Task HeartbeatAsync(Guid connectorId, ConnectorHeartbeat heartbeat, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);
        var connector = await _db.Connectors.FirstOrDefaultAsync(c => c.Id == connectorId, cancellationToken)
            ?? throw new NotFoundException("Connector not found.");
        connector.RecordContact(heartbeat.Version, _time.GetUtcNow());
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<LeasedCommand>> TryLeaseAsync(Guid connectorId, int max, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        // SKIP LOCKED lets several connector instances share a queue without handing the same command out twice.
        var commands = await _db.ConnectorCommands
            .FromSql($"SELECT *, xmin FROM connector_commands WHERE \"ConnectorId\" = {connectorId} AND \"State\" = 'Pending' ORDER BY \"CreatedAt\" LIMIT {max} FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);

        var now = _time.GetUtcNow();
        foreach (var command in commands)
        {
            command.Lease(now);
        }

        var connector = await _db.Connectors.FirstAsync(c => c.Id == connectorId, cancellationToken);
        connector.RecordContact(connector.Version, now);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _db.ChangeTracker.Clear();

        return commands
            .Select(c => new LeasedCommand(c.Id, c.Kind.ToString(), c.SapCompanyDb, c.SapObject, c.TransactionId, c.Payload))
            .ToList();
    }
}

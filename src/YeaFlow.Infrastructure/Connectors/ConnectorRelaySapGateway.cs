using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YeaFlow.Application.Common;
using YeaFlow.Application.Postings;
using YeaFlow.Domain.Connectors;
using YeaFlow.Infrastructure.Context;
using YeaFlow.Infrastructure.Persistence;
using YeaFlow.Sap.Contracts.Documents;

namespace YeaFlow.Infrastructure.Connectors;

/// <summary>
/// <see cref="ISapGateway"/> over the connector relay (D-007). Queues a command for the company's connector and waits for
/// its result. On timeout the command is withdrawn if no connector has taken it (provably not sent); a command that was
/// taken but not answered is reported as outcome unknown, so reconciliation checks SAP before anything is retried.
/// </summary>
internal sealed class ConnectorRelaySapGateway : ISapGateway
{
    private readonly TenantScope _scopes;
    private readonly TimeProvider _time;
    private readonly RelayOptions _options;
    private readonly ILogger<ConnectorRelaySapGateway> _logger;

    public ConnectorRelaySapGateway(TenantScope scopes, TimeProvider time, IOptions<RelayOptions> options, ILogger<ConnectorRelaySapGateway> logger)
    {
        _scopes = scopes;
        _time = time;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SapPostOutcome> PostAsync(Guid tenantId, Guid companyId, string sapObject, string transactionId, string payload, CancellationToken cancellationToken)
    {
        var result = await RelayAsync(tenantId, companyId, ConnectorCommandKind.PostDocument, sapObject, transactionId, payload, cancellationToken);
        return result switch
        {
            RelayResult.NoConnector r => new SapPostOutcome.TransientNotSent(r.Message),
            RelayResult.NotPickedUp => new SapPostOutcome.TransientNotSent("The SAP connector did not pick up the request in time; nothing was sent to SAP."),
            RelayResult.NoAnswer => new SapPostOutcome.Unknown("The SAP connector took the request but did not report a result in time."),
            RelayResult.Done { Command: var c } => c.Outcome switch
            {
                ConnectorOutcome.Posted => new SapPostOutcome.Posted(new SapDocumentRef(sapObject, c.SapDocEntry!.Value, c.SapDocNum!.Value)),
                ConnectorOutcome.DraftForApproval => new SapPostOutcome.DraftForApproval(c.SapDocEntry!.Value),
                ConnectorOutcome.Rejected => new SapPostOutcome.Rejected(c.ErrorCode ?? "sap.rejected", c.ErrorMessage ?? "SAP rejected the document."),
                ConnectorOutcome.TransientNotSent => new SapPostOutcome.TransientNotSent(c.ErrorMessage ?? "SAP was not reachable from the connector."),
                _ => new SapPostOutcome.Unknown(c.ErrorMessage ?? "The connector could not confirm the result."),
            },
            _ => throw new InvalidOperationException("Unhandled relay result."),
        };
    }

    public async Task<SapLookupOutcome> FindByTransactionIdAsync(Guid tenantId, Guid companyId, string sapObject, string transactionId, CancellationToken cancellationToken)
    {
        var result = await RelayAsync(tenantId, companyId, ConnectorCommandKind.FindByTransactionId, sapObject, transactionId, null, cancellationToken);
        return result switch
        {
            RelayResult.Done { Command: { Outcome: ConnectorOutcome.Found } c } => new SapLookupOutcome.Found(new SapDocumentRef(sapObject, c.SapDocEntry!.Value, c.SapDocNum!.Value)),
            RelayResult.Done { Command.Outcome: ConnectorOutcome.Absent } => new SapLookupOutcome.Absent(),
            RelayResult.Done { Command: var c } => new SapLookupOutcome.Unavailable(c.ErrorMessage ?? "SAP lookup failed."),
            RelayResult.NoConnector r => new SapLookupOutcome.Unavailable(r.Message),
            _ => new SapLookupOutcome.Unavailable("The SAP connector did not answer in time."),
        };
    }

    private Task<RelayResult> RelayAsync(Guid tenantId, Guid companyId, ConnectorCommandKind kind, string sapObject, string transactionId, string? payload, CancellationToken cancellationToken) =>
        _scopes.RunAsync(tenantId, $"relay:{transactionId}", async sp =>
        {
            var db = sp.GetRequiredService<YeaFlowDbContext>();
            var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);
            if (company?.ConnectorId is not { } connectorId)
            {
                return new RelayResult.NoConnector("No SAP connector is assigned to this company yet.");
            }

            var command = new ConnectorCommand(tenantId, companyId, connectorId, company.SapCompanyDb, kind, sapObject, transactionId, payload, _time.GetUtcNow());
            db.ConnectorCommands.Add(command);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();

            var deadline = _time.GetUtcNow() + _options.CommandTimeout;
            while (_time.GetUtcNow() < deadline)
            {
                await Task.Delay(_options.PollInterval, _time, cancellationToken);
                var current = await db.ConnectorCommands.AsNoTracking().FirstAsync(c => c.Id == command.Id, cancellationToken);
                if (current.State == ConnectorCommandState.Completed)
                {
                    return new RelayResult.Done(current);
                }
            }

            return await GiveUpAsync(db, command.Id, cancellationToken);
        });

    private async Task<RelayResult> GiveUpAsync(YeaFlowDbContext db, Guid commandId, CancellationToken cancellationToken)
    {
        var command = await db.ConnectorCommands.FirstAsync(c => c.Id == commandId, cancellationToken);
        switch (command.State)
        {
            case ConnectorCommandState.Completed:
                return new RelayResult.Done(command);
            case ConnectorCommandState.Pending:
                command.Withdraw(_time.GetUtcNow());
                try
                {
                    await ((IUnitOfWork)db).SaveChangesAsync(cancellationToken);
                    _logger.LogWarning("Connector did not pick up command {CommandId}; withdrawn", commandId);
                    return new RelayResult.NotPickedUp();
                }
                catch (ConflictException)
                {
                    // Leased in the same instant: we can no longer prove it was not sent.
                    db.ChangeTracker.Clear();
                    var latest = await db.ConnectorCommands.AsNoTracking().FirstAsync(c => c.Id == commandId, cancellationToken);
                    return latest.State == ConnectorCommandState.Completed ? new RelayResult.Done(latest) : new RelayResult.NoAnswer();
                }

            default:
                return new RelayResult.NoAnswer();
        }
    }

    private abstract record RelayResult
    {
        public sealed record Done(ConnectorCommand Command) : RelayResult;

        public sealed record NoConnector(string Message) : RelayResult;

        public sealed record NotPickedUp : RelayResult;

        public sealed record NoAnswer : RelayResult;
    }
}

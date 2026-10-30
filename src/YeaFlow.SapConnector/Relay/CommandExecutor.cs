using YeaFlow.Sap.Contracts.Connector;
using YeaFlow.SapConnector.ServiceLayer;

namespace YeaFlow.SapConnector.Relay;

/// <summary>
/// Runs one relayed command against Service Layer and turns the result into a <see cref="CommandResult"/> the API's
/// posting state machine understands. The classification is conservative: anything that might have reached SAP is Unknown.
/// </summary>
public sealed class CommandExecutor
{
    private readonly Func<string, DocumentPoster> _posterFor;
    private readonly ILogger<CommandExecutor> _logger;

    public CommandExecutor(ServiceLayerClientFactory clients, ILogger<CommandExecutor> logger)
        : this(companyDb => new DocumentPoster(clients.For(companyDb)), logger)
    {
    }

    internal CommandExecutor(Func<string, DocumentPoster> posterFor, ILogger<CommandExecutor> logger)
    {
        _posterFor = posterFor;
        _logger = logger;
    }

    public async Task<CommandResult> ExecuteAsync(LeasedCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        DocumentPoster poster;
        try
        {
            poster = _posterFor(command.SapCompanyDb);
        }
        catch (InvalidOperationException ex)
        {
            // Company not configured on this connector: nothing was sent.
            return new CommandResult(RelayOutcomes.TransientNotSent, ErrorCode: "connector.company_not_configured", ErrorMessage: ex.Message);
        }

        return command.Kind switch
        {
            RelayCommandKinds.PostDocument => await PostAsync(poster, command, cancellationToken),
            RelayCommandKinds.FindByTransactionId => await FindAsync(poster, command, cancellationToken),
            _ => new CommandResult(RelayOutcomes.TransientNotSent, ErrorCode: "connector.unsupported_command", ErrorMessage: $"This connector version does not support '{command.Kind}'."),
        };
    }

    private async Task<CommandResult> PostAsync(DocumentPoster poster, LeasedCommand command, CancellationToken cancellationToken)
    {
        if (command.Payload is null)
        {
            return new CommandResult(RelayOutcomes.Rejected, ErrorCode: "connector.no_payload", ErrorMessage: "The command has no document payload.");
        }

        try
        {
            var created = await poster.PostAsync(command.SapObject, command.TransactionId, command.Payload, cancellationToken);
            return new CommandResult(RelayOutcomes.Posted, created.DocEntry, created.DocNum);
        }
        catch (ServiceLayerException ex)
        {
            var code = ex.SapError?.Code is { Length: > 0 } c ? c : $"sl.{ex.Kind}";
            return ex.Kind switch
            {
                ServiceLayerFailureKind.Rejected or ServiceLayerFailureKind.NotFound or ServiceLayerFailureKind.ConcurrencyConflict
                    => new CommandResult(RelayOutcomes.Rejected, ErrorCode: code, ErrorMessage: ex.Message),
                ServiceLayerFailureKind.TransientNotSent or ServiceLayerFailureKind.AuthenticationFailed
                    => new CommandResult(RelayOutcomes.TransientNotSent, ErrorCode: code, ErrorMessage: ex.Message),
                _ => new CommandResult(RelayOutcomes.Unknown, ErrorCode: code, ErrorMessage: ex.Message),
            };
        }
        catch (ArgumentException ex)
        {
            // Bad entity set or payload JSON: validated before any call to SAP.
            return new CommandResult(RelayOutcomes.Rejected, ErrorCode: "connector.invalid_command", ErrorMessage: ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected failure posting {TransactionId}", command.TransactionId);
            return new CommandResult(RelayOutcomes.Unknown, ErrorCode: "connector.unexpected", ErrorMessage: "The connector hit an unexpected error; checking SAP before any retry.");
        }
    }

    private static async Task<CommandResult> FindAsync(DocumentPoster poster, LeasedCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var found = await poster.FindByTransactionIdAsync(command.SapObject, command.TransactionId, cancellationToken);
            return found is null
                ? new CommandResult(RelayOutcomes.Absent)
                : new CommandResult(RelayOutcomes.Found, found.DocEntry, found.DocNum);
        }
        catch (Exception ex) when (ex is ServiceLayerException or InvalidOperationException or ArgumentException)
        {
            // Lookup failures prove nothing either way; the API keeps the posting as outcome unknown.
            return new CommandResult(RelayOutcomes.TransientNotSent, ErrorCode: "connector.lookup_failed", ErrorMessage: ex.Message);
        }
    }
}

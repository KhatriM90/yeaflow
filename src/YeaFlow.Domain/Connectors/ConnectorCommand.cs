using YeaFlow.Domain.Common;

namespace YeaFlow.Domain.Connectors;

public enum ConnectorCommandKind
{
    /// <summary>POST a document payload to a Service Layer entity set.</summary>
    PostDocument = 1,

    /// <summary>Find a document by its YeaFlow transaction ID UDF (outcome-unknown reconciliation).</summary>
    FindByTransactionId = 2,
}

public enum ConnectorCommandState
{
    /// <summary>Waiting for the connector to pick it up. Nothing has been sent to SAP.</summary>
    Pending = 0,

    /// <summary>The connector has taken it; SAP may or may not have received it.</summary>
    Leased = 1,

    Completed = 2,

    /// <summary>Withdrawn by the API before any connector leased it, so it was provably never sent.</summary>
    Withdrawn = 3,
}

public enum ConnectorOutcome
{
    Posted = 1,
    DraftForApproval = 2,
    Rejected = 3,
    TransientNotSent = 4,
    Unknown = 5,
    Found = 6,
    Absent = 7,
}

/// <summary>
/// One unit of work relayed to a connector over its outbound long-poll (D-007). The API never pushes to the customer site;
/// the connector leases commands, runs them through Service Layer, and reports the outcome.
/// </summary>
public sealed class ConnectorCommand : ICompanyScoped
{
    private ConnectorCommand()
    {
        SapCompanyDb = string.Empty;
        SapObject = string.Empty;
        TransactionId = string.Empty;
    }

    public ConnectorCommand(Guid tenantId, Guid companyId, Guid connectorId, string sapCompanyDb, ConnectorCommandKind kind, string sapObject, string transactionId, string? payload, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CompanyId = companyId;
        ConnectorId = connectorId;
        SapCompanyDb = sapCompanyDb;
        Kind = kind;
        SapObject = sapObject;
        TransactionId = transactionId;
        Payload = payload;
        State = ConnectorCommandState.Pending;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid ConnectorId { get; private set; }

    public string SapCompanyDb { get; private set; }

    public ConnectorCommandKind Kind { get; private set; }

    public string SapObject { get; private set; }

    public string TransactionId { get; private set; }

    public string? Payload { get; private set; }

    public ConnectorCommandState State { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LeasedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public ConnectorOutcome? Outcome { get; private set; }

    public int? SapDocEntry { get; private set; }

    public int? SapDocNum { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    public uint Version { get; private set; }

    public void Lease(DateTimeOffset now)
    {
        Require(ConnectorCommandState.Pending);
        State = ConnectorCommandState.Leased;
        LeasedAt = now;
    }

    /// <summary>Only a command nobody has leased can be withdrawn; that is what makes "not sent" provable.</summary>
    public void Withdraw(DateTimeOffset now)
    {
        Require(ConnectorCommandState.Pending);
        State = ConnectorCommandState.Withdrawn;
        CompletedAt = now;
    }

    public void Complete(ConnectorOutcome outcome, int? docEntry, int? docNum, string? errorCode, string? errorMessage, DateTimeOffset now)
    {
        Require(ConnectorCommandState.Leased);
        if (!IsValidOutcome(outcome))
        {
            throw new DomainException("connector.invalid_outcome", $"{outcome} is not a valid outcome for {Kind}.");
        }

        if (outcome is ConnectorOutcome.Posted or ConnectorOutcome.Found && (docEntry is null || docNum is null))
        {
            throw new DomainException("connector.missing_document", "A posted or found outcome must carry DocEntry and DocNum.");
        }

        if (outcome is ConnectorOutcome.DraftForApproval && docEntry is null)
        {
            throw new DomainException("connector.missing_document", "A draft outcome must carry the draft DocEntry.");
        }

        Outcome = outcome;
        SapDocEntry = docEntry;
        SapDocNum = docNum;
        ErrorCode = Truncate(errorCode, 100);
        ErrorMessage = Truncate(errorMessage, 1000);
        State = ConnectorCommandState.Completed;
        CompletedAt = now;
    }

    private bool IsValidOutcome(ConnectorOutcome outcome) => Kind switch
    {
        ConnectorCommandKind.PostDocument => outcome is ConnectorOutcome.Posted or ConnectorOutcome.DraftForApproval or ConnectorOutcome.Rejected or ConnectorOutcome.TransientNotSent or ConnectorOutcome.Unknown,
        ConnectorCommandKind.FindByTransactionId => outcome is ConnectorOutcome.Found or ConnectorOutcome.Absent or ConnectorOutcome.TransientNotSent,
        _ => false,
    };

    private void Require(ConnectorCommandState state)
    {
        if (State != state)
        {
            throw new DomainException("connector.invalid_command_state", $"Command {Id} is {State}; expected {state}.");
        }
    }

    private static string? Truncate(string? value, int max) => value is { } v && v.Length > max ? v[..max] : value;
}

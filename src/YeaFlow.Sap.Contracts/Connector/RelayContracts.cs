namespace YeaFlow.Sap.Contracts.Connector;

/// <summary>
/// Wire contract between the YeaFlow API and a connector (decision D-007). The connector calls the API; the API never
/// calls into the customer network. Authentication header: <c>Authorization: Connector {token}</c>.
/// </summary>
public static class RelayRoutes
{
    public const string Prefix = "/connector/v1";
    public const string Lease = Prefix + "/commands/lease";
    public const string Heartbeat = Prefix + "/heartbeat";
    public const string AuthScheme = "Connector";

    public static string Result(Guid commandId) => $"{Prefix}/commands/{commandId}/result";
}

public static class RelayCommandKinds
{
    public const string PostDocument = "PostDocument";
    public const string FindByTransactionId = "FindByTransactionId";
}

public static class RelayOutcomes
{
    public const string Posted = "Posted";
    public const string DraftForApproval = "DraftForApproval";
    public const string Rejected = "Rejected";
    public const string TransientNotSent = "TransientNotSent";
    public const string Unknown = "Unknown";
    public const string Found = "Found";
    public const string Absent = "Absent";
}

public sealed record LeaseRequest(int MaxCommands = 5, int WaitSeconds = 20);

public sealed record LeasedCommand(Guid Id, string Kind, string SapCompanyDb, string SapObject, string TransactionId, string? Payload);

public sealed record LeaseResponse(IReadOnlyList<LeasedCommand> Commands);

public sealed record CommandResult(string Outcome, int? DocEntry = null, int? DocNum = null, string? ErrorCode = null, string? ErrorMessage = null);

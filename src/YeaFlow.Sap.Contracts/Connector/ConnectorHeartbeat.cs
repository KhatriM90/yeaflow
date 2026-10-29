namespace YeaFlow.Sap.Contracts.Connector;

/// <summary>Sent by each connector so the operator console can show version, health and last contact (scope section 8).</summary>
public sealed record ConnectorHeartbeat(
    Guid ConnectorId,
    string Version,
    DateTimeOffset SentAt,
    IReadOnlyList<CompanyHealth> Companies);

public sealed record CompanyHealth(string CompanyDb, bool ServiceLayerReachable, int ActiveSessions, string? LastError);

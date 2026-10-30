namespace YeaFlow.Infrastructure.Connectors;

public sealed class RelayOptions
{
    public const string SectionName = "Relay";

    /// <summary>How long a request waits for the connector before treating the call as not picked up / outcome unknown.</summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Upper bound for a connector long-poll.</summary>
    public TimeSpan MaxLeaseWait { get; set; } = TimeSpan.FromSeconds(25);
}

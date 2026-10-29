namespace YeaFlow.Domain.Posting;

/// <summary>Bounded retry and reconciliation settings for postings.</summary>
public sealed record RetryPolicy
{
    public static readonly RetryPolicy Default = new();

    public int MaxSubmitAttempts { get; init; } = 3;

    public int MaxReconcileAttempts { get; init; } = 10;

    public TimeSpan BaseBackoff { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan MaxBackoff { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long after the last submission an absent SAP document is accepted as proof that it was not posted.
    /// Must exceed the longest Service Layer request time so an in-flight post is not duplicated.
    /// </summary>
    public TimeSpan SettleWindow { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan BackoffFor(int attempt)
    {
        var factor = Math.Pow(2, Math.Max(0, attempt - 1));
        var ticks = Math.Min(BaseBackoff.Ticks * factor, MaxBackoff.Ticks);
        return TimeSpan.FromTicks((long)ticks);
    }
}

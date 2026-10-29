namespace YeaFlow.Domain.Posting;

/// <summary>Posting states from scope section 8. Only <see cref="Posted"/> may be shown as posted to users.</summary>
public enum PostingState
{
    Draft = 0,
    AwaitingApproval = 1,
    Queued = 2,
    Submitting = 3,
    Posted = 4,
    Failed = 5,
    OutcomeUnknown = 6,
}

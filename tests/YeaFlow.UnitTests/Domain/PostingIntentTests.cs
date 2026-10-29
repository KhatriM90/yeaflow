using YeaFlow.Domain.Common;
using YeaFlow.Domain.Posting;

namespace YeaFlow.UnitTests.Domain;

public class PostingIntentTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 29, 8, 0, 0, TimeSpan.Zero);
    private static readonly RetryPolicy Policy = new() { MaxSubmitAttempts = 3, SettleWindow = TimeSpan.FromMinutes(5), MaxReconcileAttempts = 3 };

    private static PostingIntent NewIntent() =>
        PostingIntent.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "TX-1", "Orders", """{"CardCode":"C001"}""", T0);

    private static PostingIntent Submitting()
    {
        var intent = NewIntent();
        intent.Submit(T0);
        intent.BeginSubmission(T0);
        return intent;
    }

    [Fact]
    public void Create_StartsAsDraft_WithPayloadHash()
    {
        var intent = NewIntent();
        Assert.Equal(PostingState.Draft, intent.State);
        Assert.True(intent.HasSamePayload("""{"CardCode":"C001"}"""));
        Assert.False(intent.HasSamePayload("""{"CardCode":"C002"}"""));
        Assert.Equal(64, intent.PayloadHash.Length);
    }

    [Fact]
    public void HappyPath_DraftQueuedSubmittingPosted()
    {
        var intent = Submitting();
        intent.MarkPosted(101, 5001, T0.AddSeconds(2));

        Assert.Equal(PostingState.Posted, intent.State);
        Assert.Equal(101, intent.SapDocEntry);
        Assert.Equal(5001, intent.SapDocNum);
        Assert.Equal(1, intent.SubmitAttempts);
        Assert.True(intent.IsTerminal);
    }

    [Fact]
    public void CannotPostWithoutSubmitting()
    {
        var intent = NewIntent();
        var ex = Assert.Throws<InvalidPostingTransitionException>(() => intent.MarkPosted(1, 1, T0));
        Assert.Equal(PostingState.Draft, ex.Current);
    }

    [Fact]
    public void PostedIsTerminal()
    {
        var intent = Submitting();
        intent.MarkPosted(1, 1, T0);
        Assert.Throws<InvalidPostingTransitionException>(() => intent.BeginSubmission(T0));
        Assert.Throws<InvalidPostingTransitionException>(() => intent.MarkFailed("x", "y", T0));
    }

    [Fact]
    public void Rejected_IsFailed_AndNotRetried()
    {
        var intent = Submitting();
        intent.MarkFailed("-5002", "Credit limit exceeded", T0);
        Assert.Equal(PostingState.Failed, intent.State);
        Assert.Equal("-5002", intent.LastErrorCode);
        Assert.Throws<InvalidPostingTransitionException>(() => intent.BeginSubmission(T0));
    }

    [Fact]
    public void TransientFailure_RetriesWithBackoff_ThenFails()
    {
        var intent = Submitting();
        intent.RetryAfterTransientFailure("sap.unavailable", "down", T0, Policy);
        Assert.Equal(PostingState.Queued, intent.State);
        Assert.Equal(T0 + Policy.BaseBackoff, intent.NextAttemptAt);

        intent.BeginSubmission(T0.AddMinutes(1));
        intent.RetryAfterTransientFailure("sap.unavailable", "down", T0.AddMinutes(1), Policy);
        Assert.Equal(T0.AddMinutes(1) + (Policy.BaseBackoff * 2), intent.NextAttemptAt);

        intent.BeginSubmission(T0.AddMinutes(2));
        intent.RetryAfterTransientFailure("sap.unavailable", "down", T0.AddMinutes(2), Policy);
        Assert.Equal(PostingState.Failed, intent.State);
        Assert.Equal(3, intent.SubmitAttempts);
    }

    [Fact]
    public void ApprovalFlow_DraftThenApprovedThenPosted()
    {
        var intent = Submitting();
        intent.MarkAwaitingApproval(77, T0);
        Assert.Equal(PostingState.AwaitingApproval, intent.State);
        Assert.Equal(77, intent.SapDraftEntry);

        intent.ApprovalGranted(T0.AddHours(1));
        intent.BeginSubmission(T0.AddHours(1));
        intent.MarkPosted(200, 6000, T0.AddHours(1));
        Assert.Equal(PostingState.Posted, intent.State);
    }

    [Fact]
    public void ApprovalRejected_Fails()
    {
        var intent = Submitting();
        intent.MarkAwaitingApproval(77, T0);
        intent.ApprovalRejected("Price too low", T0);
        Assert.Equal(PostingState.Failed, intent.State);
        Assert.Equal("sap.approval_rejected", intent.LastErrorCode);
    }

    [Fact]
    public void OutcomeUnknown_FoundInSap_IsPosted()
    {
        var intent = Submitting();
        intent.MarkOutcomeUnknown("timeout", T0);
        intent.ReconciledAsPosted(9, 900, T0.AddMinutes(1));
        Assert.Equal(PostingState.Posted, intent.State);
        Assert.Equal(1, intent.ReconcileAttempts);
    }

    [Fact]
    public void OutcomeUnknown_AbsentBeforeSettleWindow_DoesNotRetry()
    {
        var intent = Submitting();
        intent.MarkOutcomeUnknown("timeout", T0);
        intent.ReconciledAsAbsent(T0.AddMinutes(1), Policy);
        Assert.Equal(PostingState.OutcomeUnknown, intent.State);
        Assert.False(intent.RequiresReview);
    }

    [Fact]
    public void OutcomeUnknown_AbsentAfterSettleWindow_Requeues()
    {
        var intent = Submitting();
        intent.MarkOutcomeUnknown("timeout", T0);
        intent.ReconciledAsAbsent(T0.AddMinutes(6), Policy);
        Assert.Equal(PostingState.Queued, intent.State);
    }

    [Fact]
    public void OutcomeUnknown_AbsentWithNoRetriesLeft_RequiresReview()
    {
        var intent = Submitting();
        intent.RetryAfterTransientFailure("x", "x", T0, Policy);
        intent.BeginSubmission(T0);
        intent.RetryAfterTransientFailure("x", "x", T0, Policy);
        intent.BeginSubmission(T0);
        intent.MarkOutcomeUnknown("timeout", T0);

        intent.ReconciledAsAbsent(T0.AddMinutes(10), Policy);
        Assert.Equal(PostingState.OutcomeUnknown, intent.State);
        Assert.True(intent.RequiresReview);

        intent.ResolveAsFailedByReview("Checked SAP: not posted", T0.AddMinutes(11));
        Assert.Equal(PostingState.Failed, intent.State);
        Assert.False(intent.RequiresReview);
    }

    [Fact]
    public void StuckSubmission_BecomesOutcomeUnknown_AfterSettleWindow()
    {
        var intent = Submitting();
        Assert.False(intent.TryMarkStuckSubmissionUnknown(T0.AddMinutes(1), Policy));
        Assert.True(intent.TryMarkStuckSubmissionUnknown(T0.AddMinutes(5), Policy));
        Assert.Equal(PostingState.OutcomeUnknown, intent.State);
    }

    [Fact]
    public void TransactionId_LongerThanUdf_IsRejected()
    {
        var ex = Assert.Throws<DomainException>(() =>
            PostingIntent.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('x', 51), "Orders", "{}", T0));
        Assert.Equal("posting.transaction_id_too_long", ex.Code);
    }

    [Fact]
    public void Backoff_IsCapped()
    {
        var policy = new RetryPolicy { BaseBackoff = TimeSpan.FromSeconds(10), MaxBackoff = TimeSpan.FromSeconds(30) };
        Assert.Equal(TimeSpan.FromSeconds(10), policy.BackoffFor(1));
        Assert.Equal(TimeSpan.FromSeconds(20), policy.BackoffFor(2));
        Assert.Equal(TimeSpan.FromSeconds(30), policy.BackoffFor(5));
    }
}

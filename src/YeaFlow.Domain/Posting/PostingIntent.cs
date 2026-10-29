using System.Security.Cryptography;
using System.Text;
using YeaFlow.Domain.Common;

namespace YeaFlow.Domain.Posting;

/// <summary>
/// A write to SAP Business One, persisted before Service Layer is called (scope section 8, "Posting and recovery").
/// The <see cref="TransactionId"/> is unique per tenant, is written to the SAP document's <c>U_YF_TxnId</c> UDF,
/// and is what lets an <see cref="PostingState.OutcomeUnknown"/> result be reconciled without double posting.
/// </summary>
public sealed class PostingIntent : ICompanyScoped
{
    private PostingIntent()
    {
        TransactionId = string.Empty;
        SapObject = string.Empty;
        Payload = string.Empty;
        PayloadHash = string.Empty;
    }

    private PostingIntent(Guid id, Guid tenantId, Guid companyId, Guid actorUserId, string transactionId, string sapObject, string payload, DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        CompanyId = companyId;
        ActorUserId = actorUserId;
        TransactionId = transactionId;
        SapObject = sapObject;
        Payload = payload;
        PayloadHash = ComputePayloadHash(payload);
        State = PostingState.Draft;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid ActorUserId { get; private set; }

    /// <summary>App transaction ID; unique per tenant. Duplicate clicks reuse it and get the same intent back.</summary>
    public string TransactionId { get; private set; }

    /// <summary>Service Layer entity set the payload is posted to, e.g. <c>Orders</c> or <c>Invoices</c>.</summary>
    public string SapObject { get; private set; }

    /// <summary>JSON body sent to Service Layer. Sensitive values must not be stored here.</summary>
    public string Payload { get; private set; }

    /// <summary>SHA-256 of <see cref="Payload"/>; a reused transaction ID with a different hash is rejected.</summary>
    public string PayloadHash { get; private set; }

    public PostingState State { get; private set; }

    public int SubmitAttempts { get; private set; }

    public int ReconcileAttempts { get; private set; }

    /// <summary>Set when automatic recovery has given up and an authorised user must decide.</summary>
    public bool RequiresReview { get; private set; }

    public DateTimeOffset? NextAttemptAt { get; private set; }

    public DateTimeOffset? LastSubmittedAt { get; private set; }

    public int? SapDocEntry { get; private set; }

    public int? SapDocNum { get; private set; }

    /// <summary>DocEntry of the SAP draft created when an SAP approval procedure applies.</summary>
    public int? SapDraftEntry { get; private set; }

    public string? LastErrorCode { get; private set; }

    public string? LastErrorMessage { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Optimistic concurrency token (PostgreSQL xmin) so two workers cannot both submit.</summary>
    public uint Version { get; private set; }

    public bool IsTerminal => State is PostingState.Posted or PostingState.Failed;

    public static PostingIntent Create(Guid tenantId, Guid companyId, Guid actorUserId, string transactionId, string sapObject, string payload, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sapObject);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        if (transactionId.Length > 50)
        {
            // Must fit the alphanumeric UDF on the SAP document.
            throw new DomainException("posting.transaction_id_too_long", "Transaction IDs are at most 50 characters.");
        }

        return new PostingIntent(Guid.NewGuid(), tenantId, companyId, actorUserId, transactionId, sapObject, payload, now);
    }

    public static string ComputePayloadHash(string payload) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

    public bool HasSamePayload(string payload) =>
        string.Equals(PayloadHash, ComputePayloadHash(payload), StringComparison.Ordinal);

    /// <summary>User submits the draft for posting.</summary>
    public void Submit(DateTimeOffset now)
    {
        Require(PostingState.Draft);
        MoveTo(PostingState.Queued, now);
        NextAttemptAt = now;
    }

    /// <summary>A worker claims the intent and is about to call Service Layer.</summary>
    public void BeginSubmission(DateTimeOffset now)
    {
        Require(PostingState.Queued);
        SubmitAttempts++;
        LastSubmittedAt = now;
        NextAttemptAt = null;
        MoveTo(PostingState.Submitting, now);
    }

    public void MarkPosted(int docEntry, int docNum, DateTimeOffset now)
    {
        Require(PostingState.Submitting, PostingState.OutcomeUnknown);
        SapDocEntry = docEntry;
        SapDocNum = docNum;
        LastErrorCode = null;
        LastErrorMessage = null;
        RequiresReview = false;
        MoveTo(PostingState.Posted, now);
    }

    /// <summary>SAP saved a draft because an approval procedure applies; the app must not bypass it.</summary>
    public void MarkAwaitingApproval(int sapDraftEntry, DateTimeOffset now)
    {
        Require(PostingState.Submitting);
        SapDraftEntry = sapDraftEntry;
        MoveTo(PostingState.AwaitingApproval, now);
    }

    /// <summary>SAP approved the draft; the next submission converts the draft into the final document.</summary>
    public void ApprovalGranted(DateTimeOffset now)
    {
        Require(PostingState.AwaitingApproval);
        MoveTo(PostingState.Queued, now);
        NextAttemptAt = now;
    }

    public void ApprovalRejected(string reason, DateTimeOffset now)
    {
        Require(PostingState.AwaitingApproval);
        SetError("sap.approval_rejected", reason);
        MoveTo(PostingState.Failed, now);
    }

    /// <summary>SAP rejected the document (validation, credit limit, period, TransactionNotification…). Never retried blindly.</summary>
    public void MarkFailed(string errorCode, string errorMessage, DateTimeOffset now)
    {
        Require(PostingState.Submitting);
        SetError(errorCode, errorMessage);
        MoveTo(PostingState.Failed, now);
    }

    /// <summary>
    /// A transient failure where the request provably did not reach SAP (connection refused, session expired before send).
    /// Retries with bounded exponential backoff, then fails.
    /// </summary>
    public void RetryAfterTransientFailure(string errorCode, string errorMessage, DateTimeOffset now, RetryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        Require(PostingState.Submitting);
        SetError(errorCode, errorMessage);
        if (SubmitAttempts >= policy.MaxSubmitAttempts)
        {
            MoveTo(PostingState.Failed, now);
            return;
        }

        MoveTo(PostingState.Queued, now);
        NextAttemptAt = now + policy.BackoffFor(SubmitAttempts);
    }

    /// <summary>The request may have reached SAP but no answer came back (timeout after send).</summary>
    public void MarkOutcomeUnknown(string reason, DateTimeOffset now)
    {
        Require(PostingState.Submitting);
        SetError("posting.outcome_unknown", reason);
        MoveTo(PostingState.OutcomeUnknown, now);
    }

    /// <summary>
    /// A worker died mid-submission (process crash, deployment). After the settle window the intent is treated as
    /// outcome unknown so reconciliation decides, instead of staying in Submitting forever.
    /// </summary>
    public bool TryMarkStuckSubmissionUnknown(DateTimeOffset now, RetryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (State != PostingState.Submitting || LastSubmittedAt is not { } submitted || now - submitted < policy.SettleWindow)
        {
            return false;
        }

        MarkOutcomeUnknown("The submission did not complete; checking SAP before any retry.", now);
        return true;
    }

    /// <summary>Reconciliation found the SAP document carrying our transaction ID.</summary>
    public void ReconciledAsPosted(int docEntry, int docNum, DateTimeOffset now)
    {
        Require(PostingState.OutcomeUnknown);
        ReconcileAttempts++;
        MarkPosted(docEntry, docNum, now);
    }

    /// <summary>
    /// Reconciliation looked SAP up by transaction ID and found nothing. Only once the settle window has passed is absence
    /// treated as proof the post did not happen, and then only while retries remain; otherwise the intent goes to review.
    /// </summary>
    public void ReconciledAsAbsent(DateTimeOffset now, RetryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        Require(PostingState.OutcomeUnknown);
        ReconcileAttempts++;
        UpdatedAt = now;

        var settled = LastSubmittedAt is { } submitted && now - submitted >= policy.SettleWindow;
        if (!settled)
        {
            if (ReconcileAttempts >= policy.MaxReconcileAttempts)
            {
                RequiresReview = true;
            }

            return;
        }

        if (SubmitAttempts >= policy.MaxSubmitAttempts)
        {
            RequiresReview = true;
            return;
        }

        MoveTo(PostingState.Queued, now);
        NextAttemptAt = now;
    }

    /// <summary>An authorised user, after checking SAP, declares an unresolved posting as not posted.</summary>
    public void ResolveAsFailedByReview(string reason, DateTimeOffset now)
    {
        Require(PostingState.OutcomeUnknown);
        SetError("posting.resolved_by_review", reason);
        RequiresReview = false;
        MoveTo(PostingState.Failed, now);
    }

    private void SetError(string code, string message)
    {
        LastErrorCode = code;
        LastErrorMessage = message.Length > 1000 ? message[..1000] : message;
    }

    private void Require(params PostingState[] allowed)
    {
        if (Array.IndexOf(allowed, State) < 0)
        {
            throw new InvalidPostingTransitionException(Id, State, allowed);
        }
    }

    private void MoveTo(PostingState next, DateTimeOffset now)
    {
        State = next;
        UpdatedAt = now;
    }
}

public sealed class InvalidPostingTransitionException : DomainException
{
    public InvalidPostingTransitionException(Guid intentId, PostingState current, PostingState[] allowed)
        : base("posting.invalid_transition", $"Posting {intentId} is {current}; this action needs {string.Join(" or ", allowed)}.")
    {
        Current = current;
    }

    public PostingState Current { get; }
}

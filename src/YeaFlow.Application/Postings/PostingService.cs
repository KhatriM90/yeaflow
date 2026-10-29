using FluentValidation;
using Microsoft.Extensions.Logging;
using YeaFlow.Application.Common;
using YeaFlow.Domain.Identity;
using YeaFlow.Domain.Posting;

namespace YeaFlow.Application.Postings;

/// <summary>
/// Posting lifecycle (scope section 8): persist the intent, submit through the connector, classify the result,
/// and reconcile uncertain outcomes by transaction ID before any retry.
/// </summary>
public sealed class PostingService
{
    private readonly IPostingIntentRepository _intents;
    private readonly ISapGateway _sap;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLog _audit;
    private readonly IExecutionContext _context;
    private readonly TimeProvider _time;
    private readonly RetryPolicy _policy;
    private readonly ILogger<PostingService> _logger;

    public PostingService(
        IPostingIntentRepository intents,
        ISapGateway sap,
        IUnitOfWork unitOfWork,
        IAuditLog audit,
        IExecutionContext context,
        TimeProvider time,
        RetryPolicy policy,
        ILogger<PostingService> logger)
    {
        _intents = intents;
        _sap = sap;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _context = context;
        _time = time;
        _policy = policy;
        _logger = logger;
    }

    /// <summary>
    /// Persists the intent before anything is sent to SAP. Re-sending the same transaction ID with the same payload
    /// returns the existing intent (duplicate click); a different payload is a conflict.
    /// </summary>
    public async Task<PostingIntent> CreateAsync(CreatePostingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await new CreatePostingCommandValidator().ValidateAndThrowAsync(command, cancellationToken);
        Authorize.Require(_context, SapObjectPermissions.CreatePermissionFor(command.SapObject)!);
        Authorize.RequireCompany(_context, command.CompanyId);
        var actor = _context.UserId ?? throw new ForbiddenException("Postings must be created by a user.");

        var existing = await _intents.FindByTransactionIdAsync(command.TransactionId, cancellationToken);
        if (existing is not null)
        {
            if (existing.CompanyId != command.CompanyId || existing.SapObject != command.SapObject || !existing.HasSamePayload(command.Payload))
            {
                throw new ConflictException("posting.transaction_id_reused", "This transaction ID was already used for a different document.");
            }

            return existing;
        }

        var now = _time.GetUtcNow();
        var intent = PostingIntent.Create(_context.TenantId, command.CompanyId, actor, command.TransactionId, command.SapObject, command.Payload, now);
        if (command.SubmitNow)
        {
            intent.Submit(now);
        }

        _intents.Add(intent);
        _audit.Record("posting.created", intent.State.ToString(), intent.CompanyId, detail: $"{intent.SapObject} {intent.TransactionId}");

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictException)
        {
            // Two identical requests raced past the lookup; the unique index kept one. Return that one.
            var winner = await _intents.FindByTransactionIdAsync(command.TransactionId, cancellationToken);
            if (winner is not null && winner.HasSamePayload(command.Payload))
            {
                return winner;
            }

            throw;
        }

        return intent;
    }

    public async Task<PostingIntent> GetAsync(Guid intentId, CancellationToken cancellationToken)
    {
        var intent = await LoadAsync(intentId, cancellationToken);
        if (intent.ActorUserId != _context.UserId)
        {
            Authorize.Require(_context, Permissions.PostingsView);
        }

        return intent;
    }

    /// <summary>
    /// Claims a queued intent and sends it to SAP. Safe to call from several workers: the claim is saved with an
    /// optimistic concurrency check before the SAP call, so only one worker submits.
    /// </summary>
    public async Task<PostingIntent> ProcessAsync(Guid intentId, CancellationToken cancellationToken)
    {
        var intent = await LoadAsync(intentId, cancellationToken);
        if (intent.State != PostingState.Queued)
        {
            return intent;
        }

        intent.BeginSubmission(_time.GetUtcNow());
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictException)
        {
            _logger.LogInformation("Posting {IntentId} was claimed by another worker", intentId);
            return intent;
        }

        SapPostOutcome outcome;
        try
        {
            outcome = await _sap.PostAsync(intent.TenantId, intent.CompanyId, intent.SapObject, intent.TransactionId, intent.Payload, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // We cannot tell whether the connector reached SAP; never assume it did not.
            _logger.LogError(ex, "Unexpected failure submitting posting {IntentId}", intentId);
            outcome = new SapPostOutcome.Unknown("The submission failed unexpectedly; checking SAP before any retry.");
        }

        var now = _time.GetUtcNow();
        switch (outcome)
        {
            case SapPostOutcome.Posted posted:
                intent.MarkPosted(posted.Document.DocEntry, posted.Document.DocNum, now);
                _audit.Record("posting.submitted", "posted", intent.CompanyId, posted.Document.ToString());
                break;
            case SapPostOutcome.DraftForApproval draft:
                intent.MarkAwaitingApproval(draft.DraftEntry, now);
                _audit.Record("posting.submitted", "awaiting_approval", intent.CompanyId, $"Drafts/{draft.DraftEntry}");
                break;
            case SapPostOutcome.Rejected rejected:
                intent.MarkFailed(rejected.Code, rejected.Message, now);
                _audit.Record("posting.submitted", "rejected", intent.CompanyId, detail: rejected.Code);
                break;
            case SapPostOutcome.TransientNotSent transient:
                intent.RetryAfterTransientFailure("sap.unavailable", transient.Message, now, _policy);
                _audit.Record("posting.submitted", intent.State == PostingState.Failed ? "failed_retries_exhausted" : "retry_scheduled", intent.CompanyId);
                break;
            case SapPostOutcome.Unknown unknown:
                intent.MarkOutcomeUnknown(unknown.Message, now);
                _audit.Record("posting.submitted", "outcome_unknown", intent.CompanyId);
                break;
            default:
                throw new InvalidOperationException($"Unhandled SAP outcome {outcome.GetType().Name}.");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return intent;
    }

    /// <summary>Resolves an outcome-unknown intent by looking the document up in SAP via its transaction ID.</summary>
    public async Task<PostingIntent> ReconcileAsync(Guid intentId, CancellationToken cancellationToken)
    {
        Authorize.Require(_context, Permissions.PostingsReconcile);
        var intent = await LoadAsync(intentId, cancellationToken);
        if (intent.State != PostingState.OutcomeUnknown)
        {
            return intent;
        }

        var lookup = await _sap.FindByTransactionIdAsync(intent.TenantId, intent.CompanyId, intent.SapObject, intent.TransactionId, cancellationToken);
        var now = _time.GetUtcNow();
        switch (lookup)
        {
            case SapLookupOutcome.Found found:
                intent.ReconciledAsPosted(found.Document.DocEntry, found.Document.DocNum, now);
                _audit.Record("posting.reconciled", "posted", intent.CompanyId, found.Document.ToString());
                break;
            case SapLookupOutcome.Absent:
                intent.ReconciledAsAbsent(now, _policy);
                _audit.Record("posting.reconciled", intent.State == PostingState.Queued ? "absent_requeued" : "absent_waiting", intent.CompanyId);
                break;
            case SapLookupOutcome.Unavailable unavailable:
                _logger.LogWarning("Cannot reconcile posting {IntentId}: {Reason}", intentId, unavailable.Message);
                return intent;
            default:
                throw new InvalidOperationException($"Unhandled lookup outcome {lookup.GetType().Name}.");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return intent;
    }

    /// <summary>Controlled review: an authorised user who has checked SAP declares the posting not done.</summary>
    public async Task<PostingIntent> ResolveAsFailedAsync(Guid intentId, string reason, CancellationToken cancellationToken)
    {
        Authorize.Require(_context, Permissions.PostingsReconcile);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var intent = await LoadAsync(intentId, cancellationToken);
        intent.ResolveAsFailedByReview(reason, _time.GetUtcNow());
        _audit.Record("posting.resolved_by_review", "failed", intent.CompanyId, detail: reason);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return intent;
    }

    private async Task<PostingIntent> LoadAsync(Guid intentId, CancellationToken cancellationToken)
    {
        var intent = await _intents.GetAsync(intentId, cancellationToken) ?? throw new NotFoundException("Posting not found.");
        Authorize.RequireCompany(_context, intent.CompanyId);
        return intent;
    }
}

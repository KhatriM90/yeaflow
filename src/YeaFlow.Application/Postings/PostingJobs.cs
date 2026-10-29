using Microsoft.Extensions.Logging;
using YeaFlow.Application.Common;
using YeaFlow.Domain.Posting;

namespace YeaFlow.Application.Postings;

/// <summary>Background work for one tenant: submit due postings and reconcile outcome-unknown ones.</summary>
public sealed class PostingJobs
{
    private const int BatchSize = 50;

    private readonly IPostingIntentRepository _intents;
    private readonly PostingService _postings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;
    private readonly RetryPolicy _policy;
    private readonly ILogger<PostingJobs> _logger;

    public PostingJobs(IPostingIntentRepository intents, PostingService postings, IUnitOfWork unitOfWork, TimeProvider time, RetryPolicy policy, ILogger<PostingJobs> logger)
    {
        _intents = intents;
        _postings = postings;
        _unitOfWork = unitOfWork;
        _time = time;
        _policy = policy;
        _logger = logger;
    }

    public async Task<int> SubmitDueAsync(CancellationToken cancellationToken)
    {
        var due = await _intents.ListDueAsync(_time.GetUtcNow(), BatchSize, cancellationToken);
        foreach (var intent in due)
        {
            await _postings.ProcessAsync(intent.Id, cancellationToken);
        }

        return due.Count;
    }

    public async Task<int> ReconcileUnknownAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var stuck = await _intents.ListSubmittingSinceAsync(now - _policy.SettleWindow, BatchSize, cancellationToken);
        var marked = 0;
        foreach (var intent in stuck)
        {
            marked += intent.TryMarkStuckSubmissionUnknown(now, _policy) ? 1 : 0;
        }

        if (marked > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var unknown = await _intents.ListOutcomeUnknownAsync(BatchSize, cancellationToken);
        var stillUnknown = 0;
        foreach (var intent in unknown)
        {
            var result = await _postings.ReconcileAsync(intent.Id, cancellationToken);
            if (result.State == PostingState.OutcomeUnknown && result.RequiresReview)
            {
                stillUnknown++;
            }
        }

        if (stillUnknown > 0)
        {
            _logger.LogWarning("{Count} postings need controlled review", stillUnknown);
        }

        return unknown.Count;
    }
}

using YeaFlow.Domain.Posting;

namespace YeaFlow.Application.Postings;

/// <summary>Tenant-scoped access to posting intents (the tenant comes from <see cref="Common.IExecutionContext"/>).</summary>
public interface IPostingIntentRepository
{
    Task<PostingIntent?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<PostingIntent?> FindByTransactionIdAsync(string transactionId, CancellationToken cancellationToken);

    void Add(PostingIntent intent);

    Task<IReadOnlyList<PostingIntent>> ListDueAsync(DateTimeOffset now, int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<PostingIntent>> ListOutcomeUnknownAsync(int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<PostingIntent>> ListSubmittingSinceAsync(DateTimeOffset submittedBefore, int take, CancellationToken cancellationToken);
}

using YeaFlow.Application.Common;
using YeaFlow.Application.Postings;
using YeaFlow.Domain.Posting;

namespace YeaFlow.UnitTests.Application;

internal sealed class FakeContext : IExecutionContext
{
    public Guid TenantId { get; init; } = Guid.NewGuid();

    public Guid? UserId { get; init; } = Guid.NewGuid();

    public string CorrelationId { get; init; } = "test";

    public IReadOnlySet<string> Permissions { get; init; } = new HashSet<string>();

    public IReadOnlySet<Guid> CompanyIds { get; init; } = new HashSet<Guid>();

    public bool IsSystem { get; init; }
}

internal sealed class InMemoryIntents : IPostingIntentRepository, IUnitOfWork
{
    public List<PostingIntent> Items { get; } = [];

    public int Saves { get; private set; }

    public Task<PostingIntent?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Find(i => i.Id == id));

    public Task<PostingIntent?> FindByTransactionIdAsync(string transactionId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Find(i => i.TransactionId == transactionId));

    public void Add(PostingIntent intent) => Items.Add(intent);

    public Task<IReadOnlyList<PostingIntent>> ListDueAsync(DateTimeOffset now, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PostingIntent>>(Items.Where(i => i.State == PostingState.Queued && i.NextAttemptAt <= now).Take(take).ToList());

    public Task<IReadOnlyList<PostingIntent>> ListOutcomeUnknownAsync(int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PostingIntent>>(Items.Where(i => i.State == PostingState.OutcomeUnknown && !i.RequiresReview).Take(take).ToList());

    public Task<IReadOnlyList<PostingIntent>> ListSubmittingSinceAsync(DateTimeOffset submittedBefore, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PostingIntent>>(Items.Where(i => i.State == PostingState.Submitting && i.LastSubmittedAt <= submittedBefore).Take(take).ToList());

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.CompletedTask;
    }
}

internal sealed class RecordingAudit : IAuditLog
{
    public List<(string Action, string Result)> Events { get; } = [];

    public void Record(string action, string result, Guid? companyId = null, string? sapReference = null, string? detail = null) =>
        Events.Add((action, result));
}

internal sealed class ScriptedSapGateway : ISapGateway
{
    public Queue<SapPostOutcome> PostOutcomes { get; } = new();

    public Queue<SapLookupOutcome> LookupOutcomes { get; } = new();

    public int PostCalls { get; private set; }

    public Exception? ThrowOnPost { get; set; }

    public Task<SapPostOutcome> PostAsync(Guid tenantId, Guid companyId, string sapObject, string transactionId, string payload, CancellationToken cancellationToken)
    {
        PostCalls++;
        if (ThrowOnPost is not null)
        {
            throw ThrowOnPost;
        }

        return Task.FromResult(PostOutcomes.Dequeue());
    }

    public Task<SapLookupOutcome> FindByTransactionIdAsync(Guid tenantId, Guid companyId, string sapObject, string transactionId, CancellationToken cancellationToken) =>
        Task.FromResult(LookupOutcomes.Dequeue());
}

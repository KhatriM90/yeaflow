using YeaFlow.Application.Postings;
using YeaFlow.Domain.Posting;

namespace YeaFlow.Api.Endpoints;

/// <summary>Posting intents and the authorised retry/reconciliation console (scope sections 8 and 9).</summary>
public static class PostingEndpoints
{
    public static RouteGroupBuilder MapPostingEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/postings").WithTags("Postings");

        group.MapPost("/", async (CreatePostingRequest request, PostingService postings, CancellationToken ct) =>
        {
            var command = new CreatePostingCommand(request.CompanyId, request.TransactionId, request.SapObject, request.Payload.GetRawText(), request.SubmitNow);
            var intent = await postings.CreateAsync(command, ct);

            // "App writes submit immediately when connected" (scope section 8); the job picks up anything left queued.
            if (intent.State == PostingState.Queued)
            {
                intent = await postings.ProcessAsync(intent.Id, ct);
            }

            return Results.Ok(PostingIntentDto.From(intent));
        });

        group.MapGet("/{id:guid}", async (Guid id, PostingService postings, CancellationToken ct) =>
            Results.Ok(PostingIntentDto.From(await postings.GetAsync(id, ct))));

        group.MapPost("/{id:guid}/reconcile", async (Guid id, PostingService postings, CancellationToken ct) =>
            Results.Ok(PostingIntentDto.From(await postings.ReconcileAsync(id, ct))));

        group.MapPost("/{id:guid}/resolve-failed", async (Guid id, ResolveRequest request, PostingService postings, CancellationToken ct) =>
            Results.Ok(PostingIntentDto.From(await postings.ResolveAsFailedAsync(id, request.Reason, ct))));

        return api;
    }

    public sealed record CreatePostingRequest(Guid CompanyId, string TransactionId, string SapObject, System.Text.Json.JsonElement Payload, bool SubmitNow);

    public sealed record ResolveRequest(string Reason);

    public sealed record PostingIntentDto(
        Guid Id,
        Guid CompanyId,
        string TransactionId,
        string SapObject,
        string State,
        bool RequiresReview,
        int SubmitAttempts,
        int? SapDocEntry,
        int? SapDocNum,
        int? SapDraftEntry,
        string? ErrorCode,
        string? ErrorMessage,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt)
    {
        public static PostingIntentDto From(PostingIntent i) => new(
            i.Id, i.CompanyId, i.TransactionId, i.SapObject, i.State.ToString(), i.RequiresReview, i.SubmitAttempts,
            i.SapDocEntry, i.SapDocNum, i.SapDraftEntry, i.LastErrorCode, i.LastErrorMessage, i.CreatedAt, i.UpdatedAt);
    }
}

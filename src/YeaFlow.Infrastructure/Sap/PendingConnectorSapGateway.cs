using YeaFlow.Application.Postings;

namespace YeaFlow.Infrastructure.Sap;

/// <summary>
/// Placeholder until the API ↔ connector relay lands (next connector milestone). Reports SAP as unreachable, which the
/// posting state machine already handles: postings retry with backoff and then fail visibly; nothing is assumed posted.
/// </summary>
internal sealed class PendingConnectorSapGateway : ISapGateway
{
    private const string Message = "No SAP connector link is configured for this company yet.";

    public Task<SapPostOutcome> PostAsync(Guid tenantId, Guid companyId, string sapObject, string transactionId, string payload, CancellationToken cancellationToken) =>
        Task.FromResult<SapPostOutcome>(new SapPostOutcome.TransientNotSent(Message));

    public Task<SapLookupOutcome> FindByTransactionIdAsync(Guid tenantId, Guid companyId, string sapObject, string transactionId, CancellationToken cancellationToken) =>
        Task.FromResult<SapLookupOutcome>(new SapLookupOutcome.Unavailable(Message));
}

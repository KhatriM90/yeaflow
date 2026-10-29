using YeaFlow.Sap.Contracts.Documents;

namespace YeaFlow.Application.Postings;

/// <summary>
/// Route from the application to the tenant company's SAP connector. The connector executes the call through
/// Service Layer; the application never talks to SAP directly and never holds SAP credentials.
/// </summary>
public interface ISapGateway
{
    Task<SapPostOutcome> PostAsync(Guid tenantId, Guid companyId, string sapObject, string transactionId, string payload, CancellationToken cancellationToken);

    /// <summary>Finds the document carrying <paramref name="transactionId"/> in its <c>U_YF_TxnId</c> UDF.</summary>
    Task<SapLookupOutcome> FindByTransactionIdAsync(Guid tenantId, Guid companyId, string sapObject, string transactionId, CancellationToken cancellationToken);
}

public abstract record SapPostOutcome
{
    private SapPostOutcome()
    {
    }

    public sealed record Posted(SapDocumentRef Document) : SapPostOutcome;

    /// <summary>SAP saved a draft because an approval procedure applies.</summary>
    public sealed record DraftForApproval(int DraftEntry) : SapPostOutcome;

    /// <summary>SAP rejected the document; the code and message are safe to show to the user.</summary>
    public sealed record Rejected(string Code, string Message) : SapPostOutcome;

    public sealed record TransientNotSent(string Message) : SapPostOutcome;

    public sealed record Unknown(string Message) : SapPostOutcome;
}

public abstract record SapLookupOutcome
{
    private SapLookupOutcome()
    {
    }

    public sealed record Found(SapDocumentRef Document) : SapLookupOutcome;

    public sealed record Absent : SapLookupOutcome;

    /// <summary>SAP could not be asked (connector offline); nothing can be concluded.</summary>
    public sealed record Unavailable(string Message) : SapLookupOutcome;
}

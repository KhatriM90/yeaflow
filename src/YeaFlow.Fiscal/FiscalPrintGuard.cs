namespace YeaFlow.Fiscal;

/// <summary>
/// Blocks printing or emailing a final tax invoice, credit note or debit note until it is signed (scope section 4).
/// Every print and email path for fiscal documents must go through this guard.
/// </summary>
public sealed class FiscalPrintGuard
{
    private readonly IFiscalisationAdapter _adapter;

    public FiscalPrintGuard(IFiscalisationAdapter adapter) => _adapter = adapter;

    public async Task<FiscalSignature> EnsureSignedAsync(FiscalDocumentRef document, CancellationToken cancellationToken)
    {
        var status = await _adapter.GetStatusAsync(document, cancellationToken);
        return status switch
        {
            { State: FiscalState.Signed, Signature: { } signature } => signature,
            { State: FiscalState.Failed } => throw new FiscalisationRequiredException(document, status.FailureReason ?? "Fiscalisation failed."),
            _ => throw new FiscalisationRequiredException(document, "The document is not yet signed by the TIMS control unit."),
        };
    }
}

public sealed class FiscalisationRequiredException : Exception
{
    public FiscalisationRequiredException(FiscalDocumentRef document, string message) : base(message) => Document = document;

    public FiscalDocumentRef Document { get; }
}

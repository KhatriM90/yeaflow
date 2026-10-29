namespace YeaFlow.Fiscal;

/// <summary>Documents that must be fiscalised (scope section 4, KRA TIMS).</summary>
public enum FiscalDocumentType
{
    ArInvoice = 1,
    ArCreditNote = 2,
    ArDebitNote = 3,
}

public enum FiscalState
{
    Pending = 0,
    Signed = 1,
    Failed = 2,
}

/// <summary>Points at an SAP document in a tenant's company.</summary>
public sealed record FiscalDocumentRef(Guid TenantId, Guid CompanyId, FiscalDocumentType DocumentType, string SapObject, int DocEntry);

/// <summary>Fiscal data printed on the final tax invoice once signed by the control unit.</summary>
public sealed record FiscalSignature(
    string ControlUnitInvoiceNumber,
    string ControlUnitSerialNumber,
    string QrCodePayload,
    string? SellerPin,
    string? BuyerPin,
    DateTimeOffset? SignedAt);

public sealed record FiscalStatus(FiscalState State, FiscalSignature? Signature, string? FailureReason)
{
    public static FiscalStatus Pending { get; } = new(FiscalState.Pending, null, null);

    public static FiscalStatus SignedWith(FiscalSignature signature) => new(FiscalState.Signed, signature, null);

    public static FiscalStatus FailedWith(string reason) => new(FiscalState.Failed, null, reason);
}

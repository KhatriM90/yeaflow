namespace YeaFlow.Fiscal;

/// <summary>
/// Where a tenant's existing TIMS add-on keeps its results on the SAP document. Discovered per tenant during onboarding
/// (open question Q-003); every field name must be verified against that tenant's $metadata.
/// </summary>
public sealed record TimsAddOnFieldMap
{
    public required string StatusField { get; init; }

    public required IReadOnlySet<string> SignedValues { get; init; }

    public required IReadOnlySet<string> FailedValues { get; init; }

    public required string ControlUnitInvoiceNumberField { get; init; }

    public required string ControlUnitSerialNumberField { get; init; }

    public required string QrCodeField { get; init; }

    public string? SellerPinField { get; init; }

    public string? BuyerPinField { get; init; }

    public string? SignedAtField { get; init; }

    public string? ErrorField { get; init; }

    /// <summary>For add-ons triggered by a flag rather than on posting: the UDF to set and its value.</summary>
    public string? TriggerField { get; init; }

    public string? TriggerValue { get; init; }

    public IReadOnlyCollection<string> AllReadFields()
    {
        var fields = new List<string> { StatusField, ControlUnitInvoiceNumberField, ControlUnitSerialNumberField, QrCodeField };
        foreach (var optional in new[] { SellerPinField, BuyerPinField, SignedAtField, ErrorField })
        {
            if (!string.IsNullOrWhiteSpace(optional))
            {
                fields.Add(optional);
            }
        }

        return fields;
    }
}

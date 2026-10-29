namespace YeaFlow.Sap.Contracts.BusinessPartners;

/// <summary>Subset of the Service Layer <c>BusinessPartners</c> entity (BP01–BP03). VERIFY fields against $metadata.</summary>
public sealed record BusinessPartner
{
    public required string CardCode { get; init; }

    public string? CardName { get; init; }

    /// <summary><c>cCustomer</c>, <c>cSupplier</c> or <c>cLid</c>.</summary>
    public string? CardType { get; init; }

    public int? GroupCode { get; init; }

    public string? Currency { get; init; }

    public string? FederalTaxID { get; init; }

    public decimal? CreditLimit { get; init; }

    public decimal? CurrentAccountBalance { get; init; }

    public string? Frozen { get; init; }
}

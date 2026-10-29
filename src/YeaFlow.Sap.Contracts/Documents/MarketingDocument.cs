using System.Text.Json.Serialization;

namespace YeaFlow.Sap.Contracts.Documents;

/// <summary>
/// Common shape of Service Layer marketing documents (Orders, Invoices, PurchaseOrders …). Only fields YeaFlow sets or reads
/// are modelled; amounts and quantities are decimal and serialised as JSON numbers, which is what Service Layer expects.
/// </summary>
public sealed record MarketingDocument
{
    public int? DocEntry { get; init; }

    public int? DocNum { get; init; }

    public required string CardCode { get; init; }

    public DateOnly? DocDate { get; init; }

    public DateOnly? DocDueDate { get; init; }

    public DateOnly? TaxDate { get; init; }

    public string? DocCurrency { get; init; }

    public decimal? DocRate { get; init; }

    public string? NumAtCard { get; init; }

    public string? Comments { get; init; }

    public int? Series { get; init; }

    public int? BPL_IDAssignedToInvoice { get; init; }

    public decimal? DiscountPercent { get; init; }

    public decimal? DocTotal { get; init; }

    public string? DocumentStatus { get; init; }

    [JsonPropertyName(SapUdf.TransactionId)]
    public string? YeaFlowTransactionId { get; init; }

    public IReadOnlyList<DocumentLine> DocumentLines { get; init; } = [];
}

public sealed record DocumentLine
{
    public int? LineNum { get; init; }

    public string? ItemCode { get; init; }

    public string? ItemDescription { get; init; }

    public decimal? Quantity { get; init; }

    public decimal? UnitPrice { get; init; }

    public decimal? DiscountPercent { get; init; }

    public string? WarehouseCode { get; init; }

    public string? TaxCode { get; init; }

    public string? UoMCode { get; init; }

    public string? ProjectCode { get; init; }

    public string? CostingCode { get; init; }

    /// <summary>Base document object type (e.g. 17 for a sales order) when copying from a base document.</summary>
    public int? BaseType { get; init; }

    public int? BaseEntry { get; init; }

    public int? BaseLine { get; init; }
}

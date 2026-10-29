namespace YeaFlow.Sap.Contracts.Documents;

/// <summary>
/// Service Layer entity sets used by YeaFlow. VERIFY each against the installed $metadata (SAP B1 10 FP 2505).
/// </summary>
public static class SapObjects
{
    public const string Quotations = "Quotations";             // SAL01
    public const string Orders = "Orders";                     // SAL01
    public const string DeliveryNotes = "DeliveryNotes";       // SAL02
    public const string Returns = "Returns";                   // SAL02
    public const string Invoices = "Invoices";                 // SAL03 (also debit notes via DocumentSubType)
    public const string CreditNotes = "CreditNotes";           // SAL03
    public const string PurchaseRequests = "PurchaseRequests"; // PUR01
    public const string PurchaseQuotations = "PurchaseQuotations"; // PUR01
    public const string PurchaseOrders = "PurchaseOrders";     // PUR02
    public const string PurchaseDeliveryNotes = "PurchaseDeliveryNotes"; // PUR03 (GRPO)
    public const string PurchaseReturns = "PurchaseReturns";   // PUR03
    public const string PurchaseInvoices = "PurchaseInvoices"; // PUR04
    public const string PurchaseCreditNotes = "PurchaseCreditNotes"; // PUR04
    public const string IncomingPayments = "IncomingPayments"; // BNK01
    public const string VendorPayments = "VendorPayments";     // BNK02
    public const string Drafts = "Drafts";                     // approval drafts

    /// <summary>Objects that must be fiscalised through TIMS before a final tax document is printed or emailed.</summary>
    public static readonly IReadOnlySet<string> Fiscalised = new HashSet<string>(StringComparer.Ordinal) { Invoices, CreditNotes };
}

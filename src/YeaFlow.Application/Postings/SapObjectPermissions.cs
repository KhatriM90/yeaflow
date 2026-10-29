using YeaFlow.Domain.Identity;
using YeaFlow.Sap.Contracts.Documents;

namespace YeaFlow.Application.Postings;

/// <summary>
/// The create permission required to post to each SAP object. Decided server-side so a client cannot pick a weaker one.
/// </summary>
public static class SapObjectPermissions
{
    private static readonly Dictionary<string, string> Create = new(StringComparer.Ordinal)
    {
        [SapObjects.Quotations] = Permissions.For("Sales", "Quotation", Permissions.Actions.Create),
        [SapObjects.Orders] = Permissions.For("Sales", "Order", Permissions.Actions.Create),
        [SapObjects.DeliveryNotes] = Permissions.For("Sales", "Delivery", Permissions.Actions.Create),
        [SapObjects.Returns] = Permissions.For("Sales", "Return", Permissions.Actions.Create),
        [SapObjects.Invoices] = Permissions.For("Sales", "Invoice", Permissions.Actions.Create),
        [SapObjects.CreditNotes] = Permissions.For("Sales", "CreditNote", Permissions.Actions.Create),
        [SapObjects.PurchaseRequests] = Permissions.For("Purchasing", "Request", Permissions.Actions.Create),
        [SapObjects.PurchaseQuotations] = Permissions.For("Purchasing", "Quotation", Permissions.Actions.Create),
        [SapObjects.PurchaseOrders] = Permissions.For("Purchasing", "Order", Permissions.Actions.Create),
        [SapObjects.PurchaseDeliveryNotes] = Permissions.For("Purchasing", "GoodsReceipt", Permissions.Actions.Create),
        [SapObjects.PurchaseReturns] = Permissions.For("Purchasing", "GoodsReturn", Permissions.Actions.Create),
        [SapObjects.PurchaseInvoices] = Permissions.For("Purchasing", "Invoice", Permissions.Actions.Create),
        [SapObjects.PurchaseCreditNotes] = Permissions.For("Purchasing", "CreditNote", Permissions.Actions.Create),
        [SapObjects.IncomingPayments] = Permissions.For("Banking", "IncomingPayment", Permissions.Actions.Create),
        [SapObjects.VendorPayments] = Permissions.For("Banking", "OutgoingPayment", Permissions.Actions.Create),
    };

    public static string? CreatePermissionFor(string sapObject) => Create.GetValueOrDefault(sapObject);
}

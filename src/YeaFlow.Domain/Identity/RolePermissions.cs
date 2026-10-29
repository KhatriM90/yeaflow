namespace YeaFlow.Domain.Identity;

/// <summary>
/// Default permission set per role. Tenants will be able to tailor these (milestone 2.2);
/// until then this is the single source used by the backend permission checker.
/// </summary>
public static class RolePermissions
{
    private static readonly Dictionary<string, HashSet<string>> Map = Build();

    public static IReadOnlySet<string> For(string role) =>
        Map.TryGetValue(role, out var set) ? set : new HashSet<string>();

    public static IReadOnlySet<string> ForRoles(IEnumerable<string> roles)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            result.UnionWith(For(role));
        }

        return result;
    }

    private static Dictionary<string, HashSet<string>> Build()
    {
        var view = Permissions.Actions.View;
        var create = Permissions.Actions.Create;
        var update = Permissions.Actions.Update;
        var cancel = Permissions.Actions.Cancel;
        var export = Permissions.Actions.Export;

        static IEnumerable<string> Grant(string module, string[] resources, params string[] actions) =>
            resources.SelectMany(r => actions.Select(a => Permissions.For(module, r, a)));

        string[] salesDocs = ["Quotation", "Order", "Delivery", "Return", "Invoice", "CreditNote"];
        string[] purchaseDocs = ["Request", "Quotation", "Order", "GoodsReceipt", "GoodsReturn", "Invoice", "CreditNote"];
        string[] inventoryDocs = ["Item", "Stock", "TransferRequest", "Transfer", "GoodsReceipt", "GoodsIssue", "Counting"];
        string[] bankingDocs = ["IncomingPayment", "OutgoingPayment", "Reconciliation", "Account"];
        string[] productionDocs = ["Bom", "Order", "Issue", "Receipt"];

        return new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            [Role.Sales] = [.. Grant("Sales", salesDocs, view, create, update, cancel), .. Grant("BusinessPartners", ["Customer"], view, create, update), Permissions.For("Inventory", "Stock", view), Permissions.For("Reports", "Sales", view)],
            [Role.Purchasing] = [.. Grant("Purchasing", purchaseDocs, view, create, update, cancel), .. Grant("BusinessPartners", ["Supplier"], view, create, update), Permissions.For("Inventory", "Stock", view), Permissions.For("Reports", "Purchasing", view)],
            [Role.Warehouse] = [.. Grant("Inventory", inventoryDocs, view, create, update), Permissions.For("Sales", "Delivery", view), Permissions.For("Purchasing", "GoodsReceipt", view), Permissions.For("Reports", "Inventory", view)],
            [Role.Banking] = [.. Grant("Banking", bankingDocs, view, create, update, cancel), .. Grant("BusinessPartners", ["Customer", "Supplier"], view), Permissions.For("Reports", "Banking", view)],
            [Role.Production] = [.. Grant("Production", productionDocs, view, create, update), Permissions.For("Inventory", "Stock", view), Permissions.For("Reports", "Production", view)],
            [Role.Approver] = [.. Grant("Sales", salesDocs, view, Permissions.Actions.Approve), .. Grant("Purchasing", purchaseDocs, view, Permissions.Actions.Approve)],
            [Role.ReportViewer] = [.. Grant("Reports", ["Sales", "Purchasing", "BusinessPartners", "Inventory", "Banking", "Production"], view, export)],
            [Role.AppAdministrator] = [Permissions.AdminUsers, Permissions.AdminAudit, Permissions.AdminConnectors, Permissions.PostingsView, Permissions.PostingsReconcile],
        };
    }
}

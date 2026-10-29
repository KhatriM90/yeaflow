namespace YeaFlow.Domain.Identity;

/// <summary>
/// Permission names follow <c>{Module}.{Resource}.{Action}</c>. Actions are granted separately
/// (view/create/update/approve/cancel/export) as required by scope section 4.
/// </summary>
public static class Permissions
{
    public static class Actions
    {
        public const string View = "View";
        public const string Create = "Create";
        public const string Update = "Update";
        public const string Approve = "Approve";
        public const string Cancel = "Cancel";
        public const string Export = "Export";

        public static readonly string[] All = [View, Create, Update, Approve, Cancel, Export];
    }

    // Sensitive data restrictions.
    public const string ViewCostAndMargin = "Reports.Margin.View";

    // Posting recovery console (scope section 9: authorised retry/reconciliation console).
    public const string PostingsView = "Postings.Intent.View";
    public const string PostingsReconcile = "Postings.Intent.Reconcile";

    // App administration (separate from excluded SAP administration).
    public const string AdminUsers = "Admin.Users.Update";
    public const string AdminAudit = "Admin.Audit.View";
    public const string AdminConnectors = "Admin.Connectors.View";

    public static string For(string module, string resource, string action) => $"{module}.{resource}.{action}";
}

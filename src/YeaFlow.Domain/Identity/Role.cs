namespace YeaFlow.Domain.Identity;

/// <summary>Roles from scope section 4.</summary>
public static class Role
{
    public const string Sales = "sales";
    public const string Purchasing = "purchasing";
    public const string Warehouse = "warehouse";
    public const string Banking = "banking";
    public const string Production = "production";
    public const string Approver = "approver";
    public const string ReportViewer = "report_viewer";
    public const string AppAdministrator = "app_admin";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Sales, Purchasing, Warehouse, Banking, Production, Approver, ReportViewer, AppAdministrator,
    };

    public static bool IsPrivileged(string role) => role is AppAdministrator or Banking or Approver;
}

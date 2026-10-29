namespace YeaFlow.Reporting;

/// <summary>
/// An owner-approved report specification (scope section 9). SQL is built per dialect so the same report runs on
/// SQL Server and HANA; parameters are always bound, never concatenated.
/// </summary>
public sealed record ReportDefinition
{
    public required string Code { get; init; }

    public required string Title { get; init; }

    /// <summary>Permission needed to run or export, e.g. <c>Reports.Sales.View</c>.</summary>
    public required string Permission { get; init; }

    /// <summary>Columns hidden unless the user also holds <c>Reports.Margin.View</c>.</summary>
    public IReadOnlySet<string> CostColumns { get; init; } = new HashSet<string>();

    public required IReadOnlyList<ReportParameter> Parameters { get; init; }

    public required Func<ISqlDialect, string> BuildSql { get; init; }
}

public sealed record ReportParameter(string Name, ReportParameterType Type, bool Required);

public enum ReportParameterType
{
    Text = 1,
    Date = 2,
    Decimal = 3,
    Integer = 4,
}

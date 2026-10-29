namespace YeaFlow.Reporting;

/// <summary>
/// Database-specific SQL for report queries (decision D-004). SQL Server first; HANA is a test-and-tune exercise
/// because every report goes through this interface instead of embedding vendor syntax.
/// </summary>
public interface ISqlDialect
{
    SqlDialectKind Kind { get; }

    /// <summary>Quotes a table or column identifier, e.g. <c>[OINV]</c> or <c>"OINV"</c>.</summary>
    string QuoteIdentifier(string identifier);

    /// <summary>Named parameter placeholder as accepted by Service Layer SQLQueries (<c>:name</c>) or a direct read path.</summary>
    string Parameter(string name);

    /// <summary>Appends row limiting to an ordered query.</summary>
    string Paginate(string orderedSql, int offset, int pageSize);

    string IsNull(string expression, string fallback);

    string CurrentDate();

    /// <summary>Whole days between two date expressions (<paramref name="to"/> minus <paramref name="from"/>), used for ageing.</summary>
    string DaysBetween(string from, string to);
}

public enum SqlDialectKind
{
    SqlServer = 1,
    Hana = 2,
}

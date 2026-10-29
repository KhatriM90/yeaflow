using System.Globalization;
using System.Text.RegularExpressions;

namespace YeaFlow.Reporting;

public abstract partial class SqlDialectBase : ISqlDialect
{
    public abstract SqlDialectKind Kind { get; }

    public abstract string QuoteIdentifier(string identifier);

    public string Parameter(string name)
    {
        EnsureIdentifier(name);
        return ":" + name;
    }

    public abstract string Paginate(string orderedSql, int offset, int pageSize);

    public string IsNull(string expression, string fallback) => $"COALESCE({expression}, {fallback})";

    public abstract string CurrentDate();

    public abstract string DaysBetween(string from, string to);

    protected static void EnsureIdentifier(string identifier)
    {
        if (!IdentifierPattern().IsMatch(identifier))
        {
            throw new ArgumentException($"'{identifier}' is not a safe SQL identifier.", nameof(identifier));
        }
    }

    protected static void EnsurePage(int offset, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 10_000);
    }

    protected static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,127}$")]
    private static partial Regex IdentifierPattern();
}

public sealed class SqlServerDialect : SqlDialectBase
{
    public override SqlDialectKind Kind => SqlDialectKind.SqlServer;

    public override string QuoteIdentifier(string identifier)
    {
        EnsureIdentifier(identifier);
        return $"[{identifier}]";
    }

    public override string Paginate(string orderedSql, int offset, int pageSize)
    {
        EnsurePage(offset, pageSize);
        return $"{orderedSql} OFFSET {Num(offset)} ROWS FETCH NEXT {Num(pageSize)} ROWS ONLY";
    }

    public override string CurrentDate() => "CAST(GETDATE() AS DATE)";

    public override string DaysBetween(string from, string to) => $"DATEDIFF(day, {from}, {to})";
}

public sealed class HanaDialect : SqlDialectBase
{
    public override SqlDialectKind Kind => SqlDialectKind.Hana;

    public override string QuoteIdentifier(string identifier)
    {
        EnsureIdentifier(identifier);
        return $"\"{identifier}\"";
    }

    public override string Paginate(string orderedSql, int offset, int pageSize)
    {
        EnsurePage(offset, pageSize);
        return $"{orderedSql} LIMIT {Num(pageSize)} OFFSET {Num(offset)}";
    }

    public override string CurrentDate() => "CURRENT_DATE";

    public override string DaysBetween(string from, string to) => $"DAYS_BETWEEN({from}, {to})";
}

public static class SqlDialects
{
    public static ISqlDialect For(SqlDialectKind kind) => kind switch
    {
        SqlDialectKind.SqlServer => new SqlServerDialect(),
        SqlDialectKind.Hana => new HanaDialect(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported SQL dialect."),
    };
}

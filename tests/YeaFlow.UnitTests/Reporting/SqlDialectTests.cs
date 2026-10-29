using YeaFlow.Reporting;

namespace YeaFlow.UnitTests.Reporting;

public class SqlDialectTests
{
    private const string Ordered = "SELECT \"DocEntry\" FROM OINV ORDER BY \"DocEntry\"";

    [Fact]
    public void SqlServer_UsesOffsetFetch_AndBrackets()
    {
        var d = SqlDialects.For(SqlDialectKind.SqlServer);
        Assert.Equal($"{Ordered} OFFSET 40 ROWS FETCH NEXT 20 ROWS ONLY", d.Paginate(Ordered, 40, 20));
        Assert.Equal("[OINV]", d.QuoteIdentifier("OINV"));
        Assert.Equal("DATEDIFF(day, T0.DocDueDate, :asOf)", d.DaysBetween("T0.DocDueDate", ":asOf"));
    }

    [Fact]
    public void Hana_UsesLimitOffset_AndDoubleQuotes()
    {
        var d = SqlDialects.For(SqlDialectKind.Hana);
        Assert.Equal($"{Ordered} LIMIT 20 OFFSET 40", d.Paginate(Ordered, 40, 20));
        Assert.Equal("\"OINV\"", d.QuoteIdentifier("OINV"));
        Assert.Equal("DAYS_BETWEEN(T0.DocDueDate, :asOf)", d.DaysBetween("T0.DocDueDate", ":asOf"));
    }

    [Theory]
    [InlineData("OINV]; DROP TABLE OINV--")]
    [InlineData("a b")]
    [InlineData("")]
    public void UnsafeIdentifiers_AreRejected(string identifier)
    {
        Assert.Throws<ArgumentException>(() => new SqlServerDialect().QuoteIdentifier(identifier));
        Assert.Throws<ArgumentException>(() => new HanaDialect().Parameter(identifier));
    }

    [Fact]
    public void PageSize_IsBounded()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SqlServerDialect().Paginate(Ordered, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SqlServerDialect().Paginate(Ordered, 0, 10_001));
    }
}

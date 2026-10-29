using YeaFlow.Domain.Values;

namespace YeaFlow.UnitTests.Domain;

public class MoneyTests
{
    [Fact]
    public void Add_SameCurrency_IsExactDecimal()
    {
        var total = new Money(0.1m, "KES") + new Money(0.2m, "kes");
        Assert.Equal(0.3m, total.Amount);
        Assert.Equal("KES", total.Currency);
    }

    [Fact]
    public void Add_DifferentCurrencies_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new Money(1m, "KES") + new Money(1m, "USD"));
    }

    [Theory]
    [InlineData(2.345, 2, 2.35)]
    [InlineData(-2.345, 2, -2.35)]
    [InlineData(10.5, 0, 11)]
    [InlineData(1.0000005, 6, 1.000001)]
    public void RoundTo_UsesSapHalfAwayFromZero(double amount, int decimals, double expected)
    {
        var money = new Money((decimal)amount, "KES").RoundTo(new CurrencyPrecision("KES", decimals));
        Assert.Equal((decimal)expected, money.Amount);
    }

    [Fact]
    public void RoundTo_OtherCurrencyPrecision_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new Money(1m, "KES").RoundTo(CurrencyPrecision.Default("USD")));
    }

    [Fact]
    public void Precision_OutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CurrencyPrecision("KES", 7));
    }
}

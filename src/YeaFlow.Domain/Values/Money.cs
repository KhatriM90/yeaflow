using System.Globalization;

namespace YeaFlow.Domain.Values;

/// <summary>
/// An amount in a currency. Always <see cref="decimal"/>; stored as numeric(19,6) and serialised as a string in the app API.
/// Arithmetic never rounds implicitly; call <see cref="RoundTo"/> with the SAP currency precision.
/// </summary>
public readonly record struct Money
{
    public Money(decimal amount, string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (currency.Length > 3)
        {
            throw new ArgumentException("SAP currency codes are at most 3 characters.", nameof(currency));
        }

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Money Zero(string currency) => new(0m, currency);

    public Money RoundTo(CurrencyPrecision precision)
    {
        ArgumentNullException.ThrowIfNull(precision);
        if (!string.Equals(precision.Currency, Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Precision for {precision.Currency} cannot round {Currency}.");
        }

        return new Money(precision.Round(Amount), Currency);
    }

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    public static Money operator *(Money left, decimal factor) => left.Multiply(factor);

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Amount} {Currency}");

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Cannot combine {Currency} and {other.Currency} without an SAP exchange rate.");
        }
    }
}

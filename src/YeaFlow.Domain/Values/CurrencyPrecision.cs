namespace YeaFlow.Domain.Values;

/// <summary>
/// Rounding precision for a currency as configured in SAP Business One (OCRN / company decimal settings).
/// Values come from SAP; <see cref="Default"/> only covers the case where SAP has not yet been read.
/// </summary>
public sealed record CurrencyPrecision
{
    public const int MaxDecimals = 6;

    public CurrencyPrecision(string currency, int amountDecimals)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentOutOfRangeException.ThrowIfNegative(amountDecimals);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(amountDecimals, MaxDecimals);
        Currency = currency;
        AmountDecimals = amountDecimals;
    }

    public string Currency { get; }

    public int AmountDecimals { get; }

    public static CurrencyPrecision Default(string currency) => new(currency, 2);

    // SAP Business One rounds half away from zero.
    public decimal Round(decimal amount) => Math.Round(amount, AmountDecimals, MidpointRounding.AwayFromZero);
}

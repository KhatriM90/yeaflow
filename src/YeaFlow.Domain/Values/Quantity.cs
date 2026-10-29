using System.Globalization;

namespace YeaFlow.Domain.Values;

/// <summary>A quantity in a unit of measure. Decimal only; SAP quantity precision is applied by the caller.</summary>
public readonly record struct Quantity
{
    public Quantity(decimal value, string unitOfMeasure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unitOfMeasure);
        Value = value;
        UnitOfMeasure = unitOfMeasure;
    }

    public decimal Value { get; }

    public string UnitOfMeasure { get; }

    public bool IsPositive => Value > 0m;

    public Quantity Round(int decimals) =>
        new(Math.Round(Value, decimals, MidpointRounding.AwayFromZero), UnitOfMeasure);

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Value} {UnitOfMeasure}");
}

namespace MoneyWay.Domain.MarketData;

/// <summary>
/// Identifies the source of market data without defining provider behavior.
/// </summary>
public sealed record MarketDataProviderId
{
    public MarketDataProviderId(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
        {
            throw new ArgumentException(
                "Market data provider identifier must be non-empty and have no surrounding whitespace.",
                nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

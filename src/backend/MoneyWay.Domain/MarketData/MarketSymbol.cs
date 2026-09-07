namespace MoneyWay.Domain.MarketData;

/// <summary>
/// Preserves the provider-facing market symbol exactly.
/// </summary>
public sealed record MarketSymbol
{
    public MarketSymbol(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
        {
            throw new ArgumentException(
                "Market symbol must be non-empty and have no surrounding whitespace.",
                nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

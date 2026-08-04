namespace MoneyWay.Domain.Strategies;

/// <summary>
/// Identifies a strategy with a stable, caller-provided value.
/// </summary>
public sealed record StrategyId
{
    public StrategyId(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
        {
            throw new ArgumentException("Strategy identifier must be non-empty and have no surrounding whitespace.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

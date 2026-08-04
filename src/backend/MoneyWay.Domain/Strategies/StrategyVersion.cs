namespace MoneyWay.Domain.Strategies;

/// <summary>
/// Identifies an immutable strategy specification version without imposing a version format.
/// </summary>
public sealed record StrategyVersion
{
    public StrategyVersion(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
        {
            throw new ArgumentException("Strategy version must be non-empty and have no surrounding whitespace.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

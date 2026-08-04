namespace MoneyWay.Domain.Strategies;

/// <summary>
/// Identifies an auditable strategy rule with a caller-provided value.
/// </summary>
public sealed record RuleId
{
    public RuleId(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
        {
            throw new ArgumentException("Rule identifier must be non-empty and have no surrounding whitespace.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

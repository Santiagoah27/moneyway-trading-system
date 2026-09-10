namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Provides an opaque, strategy-owned identity for the active progression state or reference.</summary>
public sealed record StrategyReplayProgressionStateReference
{
    public StrategyReplayProgressionStateReference(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
            throw new ArgumentException("Progression state reference must be non-empty and have no surrounding whitespace.", nameof(value));
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

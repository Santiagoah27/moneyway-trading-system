namespace MoneyWay.Application.StrategyReplay.Workflow;

/// <summary>Identifies one progression instance by its deterministic replay-local ordinal.</summary>
public sealed record StrategyReplayProgressionInstanceId
{
    public StrategyReplayProgressionInstanceId(int ordinal)
    {
        if (ordinal <= 0) throw new ArgumentOutOfRangeException(nameof(ordinal));
        Ordinal = ordinal;
    }

    public int Ordinal { get; }

    public override string ToString() => Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

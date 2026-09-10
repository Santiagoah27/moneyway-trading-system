namespace MoneyWay.Application.StrategyReplay.Workflow;

public enum StrategyReplayLifecycleTransitionKind
{
    None,
    Start,
    ReplaceActiveState,
    Cancel,
    Expire,
}

/// <summary>Represents one closed, typed lifecycle decision made by a strategy-owned policy.</summary>
public sealed record StrategyReplayLifecycleTransition
{
    private StrategyReplayLifecycleTransition(
        StrategyReplayLifecycleTransitionKind kind,
        StrategyReplayProgressionStateReference? stateReference)
    {
        Kind = kind;
        StateReference = stateReference;
    }

    public StrategyReplayLifecycleTransitionKind Kind { get; }
    public StrategyReplayProgressionStateReference? StateReference { get; }

    public static StrategyReplayLifecycleTransition None { get; } = new(StrategyReplayLifecycleTransitionKind.None, null);

    public static StrategyReplayLifecycleTransition Start(StrategyReplayProgressionStateReference stateReference) =>
        new(StrategyReplayLifecycleTransitionKind.Start, stateReference ?? throw new ArgumentNullException(nameof(stateReference)));

    public static StrategyReplayLifecycleTransition ReplaceActiveState(StrategyReplayProgressionStateReference stateReference) =>
        new(StrategyReplayLifecycleTransitionKind.ReplaceActiveState, stateReference ?? throw new ArgumentNullException(nameof(stateReference)));

    public static StrategyReplayLifecycleTransition Cancel() => new(StrategyReplayLifecycleTransitionKind.Cancel, null);

    public static StrategyReplayLifecycleTransition Expire() => new(StrategyReplayLifecycleTransitionKind.Expire, null);
}

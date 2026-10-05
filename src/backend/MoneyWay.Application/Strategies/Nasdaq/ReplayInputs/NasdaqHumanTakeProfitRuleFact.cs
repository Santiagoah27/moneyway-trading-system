using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Canonical source-backed target selection. Establishes no hit, exit, order or economic result.</summary>
public sealed record NasdaqHumanTakeProfitRuleFact : IReplayRuleFact
{
    internal NasdaqHumanTakeProfitRuleFact(NasdaqHumanStructuralStopLossRuleFact stopLoss,
        NasdaqHumanTakeProfitSelection.Unique selection)
    {
        StopLoss = stopLoss ?? throw new ArgumentNullException(nameof(stopLoss));
        Selection = selection ?? throw new ArgumentNullException(nameof(selection));
    }

    public NasdaqHumanStructuralStopLossRuleFact StopLoss { get; }
    public NasdaqHumanTakeProfitSelection.Unique Selection { get; }
    public NasdaqPreEntryEligibilityRuleFact PreEntryEligibility => StopLoss.PreEntryEligibility;
    public NasdaqDemoSessionIdentity Session => StopLoss.Session;
    public NasdaqHumanH4PermittedDirection Direction => StopLoss.Direction;
    public NasdaqHumanTakeProfitTarget Target => Selection.Fact.Target;
    public decimal TargetReferencePrice => Target.TargetReferencePrice;
    public decimal TakeProfitPrice => Target.TakeProfitPrice;
    public DateTimeOffset EffectiveAtUtc => Selection.Fact.EffectiveAtUtc;
}

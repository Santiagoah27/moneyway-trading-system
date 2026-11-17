using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Established human target under original WF2 SL ancestry; no fill, target hit or outcome.</summary>
public sealed record NasdaqWickfillScenario2TakeProfitRuleFact : IReplayRuleFact
{
    internal NasdaqWickfillScenario2TakeProfitRuleFact(NasdaqWickfillScenario2TakeProfitSelection selection) => Selection = selection;
    public NasdaqWickfillScenario2TakeProfitSelection Selection { get; }
    public NasdaqHumanWickfillScenario2StopLossRuleFact StopLoss => Selection.SupportingObservations[0].StopLoss;
    public NasdaqDemoSessionIdentity Session => StopLoss.Session;
    public NasdaqHumanH4PermittedDirection Direction => StopLoss.Direction;
    public NasdaqHumanTakeProfitTarget Target => Selection.SupportingObservations[0].Target;
    public decimal TargetReferencePrice => Target.TargetReferencePrice;
    public decimal TakeProfitPrice => Target.TakeProfitPrice;
    public DateTimeOffset EffectiveAtUtc => Selection.SupportingObservations[0].EffectiveAtUtc;
}

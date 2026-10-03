using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Canonical decisive take; terminality is scoped to the exact session and retained in replay history.</summary>
public sealed record NasdaqLiquidityTakeRuleFact : IReplayRuleGateFact
{
    internal NasdaqLiquidityTakeRuleFact(NasdaqHumanLiquidityTakeObservation take, NasdaqHumanH4PermittedDirection direction, bool invalidated, string evidence, NasdaqHumanLiquidityTakeObservation? initiatingTake = null)
    { InitiatingTake = initiatingTake ?? take; Take = take; Direction = direction; IsSessionInvalidated = invalidated; EvidenceReference = evidence; }
    public NasdaqHumanLiquidityTakeObservation InitiatingTake { get; }
    public NasdaqHumanLiquidityTakeObservation Take { get; }
    public NasdaqDemoSessionIdentity Session => Take.Session;
    public NasdaqHumanH4PermittedDirection Direction { get; }
    public bool IsSessionInvalidated { get; }
    public string EvidenceReference { get; }
    public bool Blocks(StrategyReplayContext context, RuleId ruleId) => IsSessionInvalidated && Session.Matches(context)
        && (ruleId.Value.StartsWith("NQ-M5-", StringComparison.Ordinal)
            || ruleId.Value.StartsWith("NQ-FVG-", StringComparison.Ordinal)
            || ruleId.Value.StartsWith("NQ-M1-", StringComparison.Ordinal));
}

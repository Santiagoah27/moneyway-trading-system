using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;

/// <summary>Composes already-canonical parameters. No evaluator calls, candle traversal, fill synthesis or outcome calculation.</summary>
public sealed class NasdaqHistoricalTradeSnapshotAssembler
{
    public NasdaqHistoricalTradeSnapshotResult Assemble(StrategyReplayContext context, NasdaqPreEntryEligibilityRuleFact eligibility)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(eligibility);
        var entry = new NasdaqHistoricalObservedEntryObservationSelector().Select(context, eligibility);
        if (entry is not NasdaqHistoricalObservedEntrySelection.Unique unique || entry.UnavailableSourceObservations.Count > 0)
            return new NasdaqHistoricalTradeSnapshotResult.Unavailable(entry, "A unique observed execution with available required sources is needed.");

        var stop = Latest<NasdaqHumanStructuralStopLossRuleFact>(context, "NQ-SL-001");
        var target = Latest<NasdaqHumanTakeProfitRuleFact>(context, "NQ-TP-001");
        // A canonical Failed risk fact still describes documented exposure; this artifact does not authorize a trade.
        var risk = Latest<NasdaqRiskExposureRuleFact>(context, "NQ-RISK-001", allowFailed: true);
        if (stop is null || target is null || risk is null)
            return new NasdaqHistoricalTradeSnapshotResult.Unavailable(entry, "Current canonical SL, TP and risk facts are required; raw evidence is insufficient.");
        if (!NasdaqHistoricalObservedEntry.SameEligibility(eligibility, stop.PreEntryEligibility)
            || !NasdaqHistoricalObservedEntry.SameEligibility(eligibility, target.PreEntryEligibility)
            || !NasdaqHistoricalObservedEntry.SameEligibility(eligibility, risk.PreEntryEligibility)
            || !stop.Selection.Fact.SameFact(target.StopLoss.Selection.Fact)
            || !stop.Selection.Fact.SameFact(risk.StopLoss.Selection.Fact))
            return new NasdaqHistoricalTradeSnapshotResult.Unavailable(entry, "Canonical constituents do not share exact setup and selected SL ancestry.");
        if (stop.Selection.SupportingObservations.Any(o => o.ObservedAtUtc > context.AsOfUtc)
            || target.Selection.SupportingObservations.Any(o => o.ObservedAtUtc > context.AsOfUtc)
            || risk.Selection.SupportingObservations.Any(o => o.ObservedAtUtc > context.AsOfUtc))
            return new NasdaqHistoricalTradeSnapshotResult.Unavailable(entry, "Constituent evidence is not yet observable.");
        return new NasdaqHistoricalTradeSnapshotResult.Available(new(context.AsOfUtc, eligibility, unique, stop, target, risk));
    }

    private static T? Latest<T>(StrategyReplayContext context, string ruleId, bool allowFailed = false) where T : class, IReplayRuleFact
    {
        // Read the latest owning-rule evaluation, never fall back to an older fact after missing/conflicting evidence.
        var owner = context.PriorObservations.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == ruleId));
        var result = owner?.Evaluations.Single(e => e.RuleId.Value == ruleId).Result;
        if (result != RuleEvaluationResult.Passed && !(allowFailed && result == RuleEvaluationResult.Failed)) return null;
        if (!allowFailed && owner?.WorkflowProgression?.RuleEligibility.SingleOrDefault(e => e.RuleId.Value == ruleId)?.EstablishesProgression != true)
            return null;
        return owner?.RuleFacts.Where(f => f.RuleId.Value == ruleId).Select(f => f.Fact).OfType<T>().SingleOrDefault();
    }
}

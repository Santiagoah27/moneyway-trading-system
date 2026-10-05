using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Selects documented risk evidence without sizing, ratio evaluation or execution.</summary>
public sealed class NasdaqRiskExposureObservationSelector
{
    public NasdaqRiskExposureSelection Select(StrategyReplayContext context,
        NasdaqPreEntryEligibilityRuleFact preEntryEligibility, NasdaqHumanStructuralStopLossRuleFact stopLoss)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(preEntryEligibility);
        ArgumentNullException.ThrowIfNull(stopLoss);
        if (!preEntryEligibility.Session.Matches(context)
            || !NasdaqRiskExposureObservation.SameEligibility(preEntryEligibility, stopLoss.PreEntryEligibility)
            || !NasdaqHumanM5FvgObservationSelector.InWindow(context, context.AsOfUtc)
            || context.PriorObservations.SelectMany(o => o.RuleFacts).Select(f => f.Fact)
                .OfType<IReplayRuleGateFact>().Any(g => g.Blocks(context, new RuleId("NQ-RISK-001"))
                    || g.Blocks(context, new RuleId("NQ-M1-003")))
            || context.PriorObservations.LastOrDefault(o => o.LifecycleProgression is not null)?.LifecycleProgression
                is { ActiveInstance: null })
            return new NasdaqRiskExposureSelection.Missing([]);

        var latest = context.PriorObservations.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-SL-001"));
        var current = latest?.RuleFacts.Where(f => f.RuleId.Value == "NQ-SL-001").Select(f => f.Fact)
            .OfType<NasdaqHumanStructuralStopLossRuleFact>().SingleOrDefault();
        if (current is null || latest!.Evaluations.Single(e => e.RuleId.Value == "NQ-SL-001").Result != RuleEvaluationResult.Passed
            || latest.WorkflowProgression?.RuleEligibility.SingleOrDefault(e => e.RuleId.Value == "NQ-SL-001")?.EstablishesProgression != true
            || !current.Selection.Fact.SameFact(stopLoss.Selection.Fact))
            return new NasdaqRiskExposureSelection.Missing([]);

        var valid = new List<NasdaqRiskExposureObservation>();
        var unavailable = new List<NasdaqRiskExposureObservation>();
        foreach (var observation in NasdaqHumanM5FvgObservationSelector.Ordered(context.InputObservations.OfType<NasdaqRiskExposureObservation>())
            .Where(o => NasdaqRiskExposureObservation.SameEligibility(o.PreEntryEligibility, preEntryEligibility)
                && o.StopLoss.Selection.Fact.SameFact(current.Selection.Fact)
                && o.EffectiveAtUtc <= context.AsOfUtc && o.ObservedAtUtc <= context.AsOfUtc))
        {
            var missing = false;
            var unusable = false;
            foreach (var source in observation.StopLoss.Selection.Fact.ProtectionAnchor.SelectedTurnCandles)
            {
                if (source.CloseTimeUtc > context.AsOfUtc) { unusable = true; break; }
                if (!context.TryGetFrame(source.Timeframe, out var frame)) { missing = true; continue; }
                var actual = frame!.AvailableCandles.SingleOrDefault(c => c.OpenTimeUtc == source.OpenTimeUtc);
                if (actual is null) { missing = true; continue; }
                if (!NasdaqStructuralLiquidityReference.SameCandle(actual, source)) { unusable = true; break; }
            }
            if (unusable) continue;
            if (missing) unavailable.Add(observation);
            else valid.Add(observation);
        }
        if (valid.Count == 0) return new NasdaqRiskExposureSelection.Missing(unavailable);
        return valid.All(o => o.SameFact(valid[0]))
            ? new NasdaqRiskExposureSelection.Unique(valid, unavailable)
            : new NasdaqRiskExposureSelection.Conflict(valid, unavailable);
    }
}

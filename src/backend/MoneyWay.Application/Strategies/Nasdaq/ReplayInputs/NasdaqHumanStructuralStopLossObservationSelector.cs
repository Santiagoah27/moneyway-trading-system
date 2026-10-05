using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Selects an exact human SL assertion without detecting swings or calculating a stop price.</summary>
public sealed class NasdaqHumanStructuralStopLossObservationSelector
{
    public NasdaqHumanStructuralStopLossSelection Select(StrategyReplayContext context,
        NasdaqPreEntryEligibilityRuleFact preEntryEligibility)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(preEntryEligibility);
        if (!preEntryEligibility.Session.Matches(context)
            || !NasdaqHumanM5FvgObservationSelector.InWindow(context, context.AsOfUtc)
            || context.PriorObservations.SelectMany(o => o.RuleFacts).Select(f => f.Fact)
                .OfType<IReplayRuleGateFact>().Any(g => g.Blocks(context, new RuleId("NQ-SL-001")))
            || context.PriorObservations.LastOrDefault(o => o.LifecycleProgression is not null)?.LifecycleProgression
                is { ActiveInstance: null })
            return new NasdaqHumanStructuralStopLossSelection.Missing([]);

        var established = context.PriorObservations.LastOrDefault(o => o.WorkflowProgression?.RuleEligibility
            .SingleOrDefault(e => e.RuleId.Value == "NQ-M1-003")?.EstablishesProgression == true);
        var current = established?.RuleFacts.Where(f => f.RuleId.Value == "NQ-M1-003").Select(f => f.Fact)
            .OfType<NasdaqPreEntryEligibilityRuleFact>().SingleOrDefault();
        if (current is null || established!.Evaluations.Single(e => e.RuleId.Value == "NQ-M1-003").Result != RuleEvaluationResult.Passed
            || !SameEligibility(current, preEntryEligibility))
            return new NasdaqHumanStructuralStopLossSelection.Missing([]);

        var valid = new List<NasdaqHumanStructuralStopLossObservation>();
        var unavailable = new List<NasdaqHumanStructuralStopLossObservation>();
        foreach (var observation in NasdaqHumanM5FvgObservationSelector.Ordered(
            context.InputObservations.OfType<NasdaqHumanStructuralStopLossObservation>())
            .Where(o => SameEligibility(o.PreEntryEligibility, current)
                && o.EffectiveAtUtc <= context.AsOfUtc && o.ObservedAtUtc <= context.AsOfUtc))
        {
            var missing = false;
            var unusable = false;
            foreach (var source in observation.ProtectionAnchor.SelectedTurnCandles)
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
        if (valid.Count == 0) return new NasdaqHumanStructuralStopLossSelection.Missing(unavailable);
        return valid.All(o => o.SameFact(valid[0]))
            ? new NasdaqHumanStructuralStopLossSelection.Unique(valid, unavailable)
            : new NasdaqHumanStructuralStopLossSelection.Conflict(valid, unavailable);
    }

    private static bool SameEligibility(NasdaqPreEntryEligibilityRuleFact one, NasdaqPreEntryEligibilityRuleFact other) =>
        one.Session == other.Session && one.Direction == other.Direction
        && one.Realignment.Selection.Fact.SameFact(other.Realignment.Selection.Fact);
}

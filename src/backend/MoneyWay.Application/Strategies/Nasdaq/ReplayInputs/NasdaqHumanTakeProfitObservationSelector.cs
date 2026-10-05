using System.Text.Json;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Selects explicit human target evidence without ranking, price adjustment or execution.</summary>
public sealed class NasdaqHumanTakeProfitObservationSelector
{
    public NasdaqHumanTakeProfitSelection Select(StrategyReplayContext context,
        NasdaqHumanStructuralStopLossRuleFact stopLoss)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(stopLoss);
        if (!stopLoss.Session.Matches(context)
            || !NasdaqHumanM5FvgObservationSelector.InWindow(context, context.AsOfUtc)
            || context.PriorObservations.SelectMany(o => o.RuleFacts).Select(f => f.Fact)
                .OfType<IReplayRuleGateFact>().Any(g => g.Blocks(context, new RuleId("NQ-TP-001"))
                    || g.Blocks(context, new RuleId("NQ-M1-003")))
            || context.PriorObservations.LastOrDefault(o => o.LifecycleProgression is not null)?.LifecycleProgression
                is { ActiveInstance: null })
            return new NasdaqHumanTakeProfitSelection.Missing([]);

        var latest = context.PriorObservations.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-SL-001"));
        var current = latest?.RuleFacts.Where(f => f.RuleId.Value == "NQ-SL-001").Select(f => f.Fact)
            .OfType<NasdaqHumanStructuralStopLossRuleFact>().SingleOrDefault();
        if (current is null || latest!.Evaluations.Single(e => e.RuleId.Value == "NQ-SL-001").Result != RuleEvaluationResult.Passed
            || latest.WorkflowProgression?.RuleEligibility.SingleOrDefault(e => e.RuleId.Value == "NQ-SL-001")?.EstablishesProgression != true
            || !current.Selection.Fact.SameFact(stopLoss.Selection.Fact))
            return new NasdaqHumanTakeProfitSelection.Missing([]);

        var valid = new List<NasdaqHumanTakeProfitObservation>();
        var unavailable = new List<NasdaqHumanTakeProfitObservation>();
        foreach (var observation in context.InputObservations.OfType<NasdaqHumanTakeProfitObservation>()
            // Stable diagnostic order only: every usable assertion participates; no order selects a winner.
            .OrderBy(o => o.ObservedAtUtc).ThenBy(o => o.SourceReference, StringComparer.Ordinal)
            .ThenBy(o => JsonSerializer.Serialize(o.Target.Reference, o.Target.Reference.GetType()), StringComparer.Ordinal)
            .ThenBy(o => JsonSerializer.Serialize(o), StringComparer.Ordinal)
            .Where(o => o.StopLoss.Selection.Fact.SameFact(current.Selection.Fact)
                && o.EffectiveAtUtc <= context.AsOfUtc && o.ObservedAtUtc <= context.AsOfUtc))
        {
            var missing = false;
            var unusable = false;
            foreach (var source in observation.Target.Reference.Sources.Concat(observation.StopLoss.Selection.Fact.ProtectionAnchor.SelectedTurnCandles))
            {
                if (source.CloseTimeUtc > context.AsOfUtc) { unusable = true; break; }
                if (!context.TryGetFrame(source.Timeframe, out var frame)) { missing = true; continue; }
                var actual = frame!.AvailableCandles.SingleOrDefault(c => c.OpenTimeUtc == source.OpenTimeUtc);
                if (actual is null) { missing = true; continue; }
                if (!NasdaqStructuralLiquidityReference.SameCandle(actual, source)) { unusable = true; break; }
            }
            if (unusable) continue;
            if (missing) unavailable.Add(observation);
            else if (observation.Target.Reference is not NasdaqLiquidityTakeReference.Structural structural
                || structural.Selection.References.All(r => r.IsObservable(context, observation.EffectiveAtUtc)))
                valid.Add(observation);
        }
        if (valid.Count == 0) return new NasdaqHumanTakeProfitSelection.Missing(unavailable);
        return valid.All(o => o.SameFact(valid[0]))
            ? new NasdaqHumanTakeProfitSelection.Unique(valid, unavailable)
            : new NasdaqHumanTakeProfitSelection.Conflict(valid, unavailable);
    }
}

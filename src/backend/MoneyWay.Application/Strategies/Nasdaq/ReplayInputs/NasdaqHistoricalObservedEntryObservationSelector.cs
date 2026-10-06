using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Selects one exact documented historical execution without inferring a fill or authorizing an order.</summary>
public sealed class NasdaqHistoricalObservedEntryObservationSelector
{
    public NasdaqHistoricalObservedEntrySelection Select(StrategyReplayContext context,
        NasdaqPreEntryEligibilityRuleFact preEntryEligibility)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(preEntryEligibility);
        if (!preEntryEligibility.Session.Matches(context)
            || !NasdaqHumanM5FvgObservationSelector.InWindow(context, context.AsOfUtc)
            || context.PriorObservations.SelectMany(o => o.RuleFacts).Select(f => f.Fact)
                .OfType<IReplayRuleGateFact>().Any(g => g.Blocks(context, new RuleId("NQ-M1-003")))
            || context.PriorObservations.LastOrDefault(o => o.LifecycleProgression is not null)?.LifecycleProgression
                is { ActiveInstance: null })
            return new NasdaqHistoricalObservedEntrySelection.Missing([]);

        // Canonical availability is required now; its transport timestamp is not a minimum execution time.
        var established = context.PriorObservations.LastOrDefault(o => o.WorkflowProgression?.RuleEligibility
            .SingleOrDefault(e => e.RuleId.Value == "NQ-M1-003")?.EstablishesProgression == true);
        var current = established?.RuleFacts.Where(f => f.RuleId.Value == "NQ-M1-003").Select(f => f.Fact)
            .OfType<NasdaqPreEntryEligibilityRuleFact>().SingleOrDefault();
        if (current is null || established!.Evaluations.Single(e => e.RuleId.Value == "NQ-M1-003").Result != RuleEvaluationResult.Passed
            || !NasdaqHistoricalObservedEntry.SameEligibility(current, preEntryEligibility))
            return new NasdaqHistoricalObservedEntrySelection.Missing([]);

        var valid = new List<NasdaqHistoricalObservedEntryObservation>();
        var unavailable = new List<NasdaqHistoricalObservedEntryObservation>();
        foreach (var observation in NasdaqHumanM5FvgObservationSelector.Ordered(
            context.InputObservations.OfType<NasdaqHistoricalObservedEntryObservation>())
            .Where(o => NasdaqHistoricalObservedEntry.SameEligibility(o.PreEntryEligibility, current)
                && o.Entry.EntryEffectiveAtUtc <= context.AsOfUtc
                && NasdaqHumanM5FvgObservationSelector.InWindow(context, o.Entry.EntryEffectiveAtUtc) && o.ObservedAtUtc <= context.AsOfUtc))
        {
            var missing = false;
            var unusable = false;
            foreach (var source in observation.Entry.SupportingCandles)
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
        if (valid.Count == 0) return new NasdaqHistoricalObservedEntrySelection.Missing(unavailable);
        return valid.All(o => o.Entry.SameFact(valid[0].Entry))
            ? new NasdaqHistoricalObservedEntrySelection.Unique(valid, unavailable)
            : new NasdaqHistoricalObservedEntrySelection.Conflict(valid, unavailable);
    }
}

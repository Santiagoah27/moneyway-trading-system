using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Selects a review for the exact uniquely selected FVG. Rejection remains a semantic fact.</summary>
public sealed class NasdaqHumanM5FvgQualityObservationSelector
{
    public NasdaqHumanM5FvgQualitySelection Select(StrategyReplayContext context, NasdaqHumanM5TriggerRuleFact trigger,
        NasdaqHumanM5FvgSelection.Unique selectedFvg)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(selectedFvg);
        var fvg = selectedFvg.Fact;
        if (!fvg.Trigger.SameFact(trigger.Selection.Fact) || !NasdaqHumanM5FvgObservationSelector.IsEstablished(context, trigger))
            return new NasdaqHumanM5FvgQualitySelection.Missing([]);
        // Recheck selection in this frame: an earlier Unique must not hide new conflicting or unavailable evidence.
        var current = new NasdaqHumanM5FvgObservationSelector().Select(context, trigger);
        var usableSelection = current is NasdaqHumanM5FvgSelection.Unique u && u.Fact.SameFact(fvg);
        return SelectForCandidate(context, trigger, selectedFvg, usableSelection);
    }

    internal NasdaqHumanM5FvgQualitySelection SelectForCandidate(StrategyReplayContext context, NasdaqHumanM5TriggerRuleFact trigger,
        NasdaqHumanM5FvgSelection.Unique selectedFvg, bool usableSelection = true)
    {
        var fvg = selectedFvg.Fact;
        if (!fvg.Trigger.SameFact(trigger.Selection.Fact) || !NasdaqHumanM5FvgObservationSelector.IsEstablished(context, trigger))
            return new NasdaqHumanM5FvgQualitySelection.Missing([]);
        var valid = new List<NasdaqHumanM5FvgQualityObservation>();
        var unavailable = new List<NasdaqHumanM5FvgQualityObservation>();
        foreach (var observation in NasdaqHumanM5FvgObservationSelector.Ordered(context.InputObservations.OfType<NasdaqHumanM5FvgQualityObservation>())
            .Where(o => o.Fact.Fvg.SameFact(fvg) && o.ObservedAtUtc <= context.AsOfUtc
                && NasdaqHumanM5FvgObservationSelector.InWindow(context, o.EffectiveAtUtc)))
        {
            var source = NasdaqHumanM5FvgObservationSelector.ResolveSources(context, observation.Fact.Fvg);
            if (source == NasdaqHumanM5FvgObservationSelector.SourceAvailability.Unavailable) unavailable.Add(observation);
            else if (source == NasdaqHumanM5FvgObservationSelector.SourceAvailability.Usable && usableSelection) valid.Add(observation);
        }
        if (valid.Count == 0) return new NasdaqHumanM5FvgQualitySelection.Missing(unavailable);
        return valid.All(o => o.Fact.SameFact(valid[0].Fact))
            ? new NasdaqHumanM5FvgQualitySelection.Unique(valid, unavailable)
            : new NasdaqHumanM5FvgQualitySelection.Conflict(valid, unavailable);
    }
}

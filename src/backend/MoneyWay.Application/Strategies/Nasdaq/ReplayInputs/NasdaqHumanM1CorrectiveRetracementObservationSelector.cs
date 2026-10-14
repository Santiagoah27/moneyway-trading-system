using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Selects only source-backed human pullback evidence for the current M5 trigger with optional approved confirmation.</summary>
public sealed class NasdaqHumanM1CorrectiveRetracementObservationSelector
{
    public NasdaqHumanM1CorrectiveRetracementSelection Select(StrategyReplayContext context,
        NasdaqHumanM5FvgQualityRuleFact approvedQuality)
    {
        ArgumentNullException.ThrowIfNull(approvedQuality);
        return Select(context, approvedQuality.Candidate.Trigger, approvedQuality);
    }

    public NasdaqHumanM1CorrectiveRetracementSelection Select(StrategyReplayContext context,
        NasdaqHumanM5TriggerRuleFact trigger) => Select(context, trigger, null);

    private NasdaqHumanM1CorrectiveRetracementSelection Select(StrategyReplayContext context,
        NasdaqHumanM5TriggerRuleFact trigger, NasdaqHumanM5FvgQualityRuleFact? requiredSupport)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(trigger);
        if (!NasdaqHumanM5FvgObservationSelector.IsEstablished(context, trigger))
            return new NasdaqHumanM1CorrectiveRetracementSelection.Missing([]);
        var support = NasdaqM1FvgSupportResolver.Resolve(context, trigger);
        if (support.Blocker is not null || requiredSupport is not null
            && (support.ApprovedQuality is null || !support.ApprovedQuality.Selection.Fact.Fact.SameFact(requiredSupport.Selection.Fact.Fact)))
            return new NasdaqHumanM1CorrectiveRetracementSelection.Missing([]);
        var valid = new List<NasdaqHumanM1CorrectiveRetracementObservation>();
        var unavailable = new List<NasdaqHumanM1CorrectiveRetracementObservation>();
        foreach (var observation in NasdaqHumanM5FvgObservationSelector.Ordered(
            context.InputObservations.OfType<NasdaqHumanM1CorrectiveRetracementObservation>())
            .Where(o => o.Session == trigger.Selection.Fact.Session && o.Trigger.SameFact(trigger.Selection.Fact)
                && (o.ApprovedQuality is null || support.ApprovedQuality is not null
                    && o.ApprovedQuality.Fact.SameFact(support.ApprovedQuality.Selection.Fact.Fact))
                && o.ObservedAtUtc <= context.AsOfUtc && o.PullbackEffectiveAtUtc >= trigger.Selection.Fact.EffectiveAtUtc
                && (support.ApprovedQuality is null || NasdaqHumanM5FvgCandidateHistory.Rejections(context, trigger).Count == 0
                    || o.PullbackEffectiveAtUtc >= support.ApprovedQuality.Selection.Fact.EffectiveAtUtc
                        && o.ObservedAtUtc >= support.ApprovedQuality.Selection.Fact.ObservedAtUtc)
                && NasdaqHumanM5FvgObservationSelector.InWindow(context, o.PullbackEffectiveAtUtc)))
        {
            var missing = false;
            var unusable = false;
            foreach (var source in observation.Event.SourceCandles)
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
        if (valid.Count == 0) return new NasdaqHumanM1CorrectiveRetracementSelection.Missing(unavailable);
        return valid.All(o => o.SameFact(valid[0]))
            ? new NasdaqHumanM1CorrectiveRetracementSelection.Unique(valid, unavailable)
            : new NasdaqHumanM1CorrectiveRetracementSelection.Conflict(valid, unavailable);
    }
}

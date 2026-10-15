using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Resolves human reversal assertions for one exact opening-sweep ancestry; never detects or passes a rule.</summary>
public sealed class NasdaqHumanWickfillScenario2PostSweepReversalObservationSelector
{
    public NasdaqHumanWickfillScenario2PostSweepReversalSelection Select(StrategyReplayContext context,
        NasdaqWickfillScenario2OpeningSweepContext openingSweep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(openingSweep);
        if (!openingSweep.Session.Matches(context) || openingSweep.ObservedAtUtc > context.AsOfUtc)
            return new NasdaqHumanWickfillScenario2PostSweepReversalSelection.Missing([]);
        try
        {
            NasdaqWickfillScenario2OpeningSweepContext.From(context, openingSweep.PreOpenContext, openingSweep.TakeObservation);
        }
        catch (ArgumentException)
        {
            // Reuse the composition's canonical source/ancestry checks, without reevaluating strategy rules.
            return new NasdaqHumanWickfillScenario2PostSweepReversalSelection.Missing([]);
        }
        var valid = new List<NasdaqHumanWickfillScenario2PostSweepReversalObservation>();
        var unavailable = new List<NasdaqHumanWickfillScenario2PostSweepReversalObservation>();
        foreach (var observation in NasdaqHumanM5FvgObservationSelector.Ordered(
            context.InputObservations.OfType<NasdaqHumanWickfillScenario2PostSweepReversalObservation>())
            .Where(o => o.ObservedAtUtc <= context.AsOfUtc
                && NasdaqHumanWickfillScenario2PostSweepReversalObservation.SameOpeningSweep(o.OpeningSweep, openingSweep)))
        {
            var missing = false;
            var mismatch = false;
            foreach (var source in observation.SupportingCandles)
            {
                if (!context.TryGetFrame(source.Timeframe, out var frame)) { missing = true; continue; }
                var actual = frame!.AvailableCandles.SingleOrDefault(c => c.OpenTimeUtc == source.OpenTimeUtc);
                if (actual is null) { missing = true; continue; }
                if (!NasdaqStructuralLiquidityReference.SameCandle(actual, source)) { mismatch = true; break; }
            }
            if (mismatch) continue;
            if (missing) unavailable.Add(observation);
            else valid.Add(observation);
        }
        if (valid.Count == 0) return new NasdaqHumanWickfillScenario2PostSweepReversalSelection.Missing(unavailable);
        return valid.All(o => o.SameFact(valid[0]))
            ? new NasdaqHumanWickfillScenario2PostSweepReversalSelection.Unique(valid, unavailable)
            : new NasdaqHumanWickfillScenario2PostSweepReversalSelection.Conflict(valid, unavailable);
    }
}

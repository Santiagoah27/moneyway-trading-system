using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Stateless selection of human pre-open context evidence. It does not select a strategy branch or pass rules.</summary>
public sealed class NasdaqHumanWickfillScenario2ContextObservationSelector
{
    public NasdaqHumanWickfillScenario2ContextSelection Select(StrategyReplayContext context, NasdaqDemoSessionIdentity session)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(session);
        if (!session.Matches(context)) return new NasdaqHumanWickfillScenario2ContextSelection.Missing([]);
        var valid = new List<NasdaqHumanWickfillScenario2ContextObservation>();
        var unavailable = new List<NasdaqHumanWickfillScenario2ContextObservation>();
        foreach (var observation in NasdaqHumanM5FvgObservationSelector.Ordered(
            context.InputObservations.OfType<NasdaqHumanWickfillScenario2ContextObservation>())
            .Where(o => o.Session == session && o.ObservedAtUtc <= context.AsOfUtc))
        {
            var missing = false;
            var mismatch = false;
            foreach (var source in observation.ReviewedSourceCandles)
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
        if (valid.Count == 0) return new NasdaqHumanWickfillScenario2ContextSelection.Missing(unavailable);
        return valid.All(o => o.SameFact(valid[0]))
            ? new NasdaqHumanWickfillScenario2ContextSelection.Unique(valid, unavailable)
            : new NasdaqHumanWickfillScenario2ContextSelection.Conflict(valid, unavailable);
    }
}

using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Selects source-observable session H4 facts without interpreting structure, choosing authority or passing rules.</summary>
public sealed class NasdaqHumanH4ContextObservationSelector
{
    public NasdaqHumanH4ContextSelection Select(StrategyReplayContext context, NasdaqDemoSessionIdentity session)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(session);
        if (!session.Matches(context) || !context.TryGetFrame(NasdaqHumanH4ContextObservation.H4, out var frame))
            return new NasdaqHumanH4ContextSelection.Missing();

        // Only the bounded replay snapshot is inspected, never the original series or future OHLC.
        var closed = frame!.AvailableCandles.ToDictionary(candle => candle.OpenTimeUtc, candle => candle.CloseTimeUtc);
        bool IsObservable(NasdaqHumanH4ContextFact fact)
        {
            bool ClosedByEffectiveTime(DateTimeOffset openTime) => closed.TryGetValue(openTime, out var closeTime)
                && closeTime <= fact.EffectiveAtUtc && closeTime <= context.AsOfUtc;
            return fact.EffectiveAtUtc <= context.AsOfUtc && ClosedByEffectiveTime(fact.ContextCandleOpenTimeUtc)
                && fact.Anchors.All(anchor => anchor.MemberOpenTimesUtc.All(ClosedByEffectiveTime)
                    && (anchor.ValidatingCandleOpenTimeUtc is not { } validation || ClosedByEffectiveTime(validation)));
        }

        var supporting = context.InputObservations.OfType<NasdaqHumanH4ContextObservation>()
            .Where(item => item.Session == session && item.ObservedAtUtc <= context.AsOfUtc && IsObservable(item.Fact))
            .OrderBy(item => item.ObservedAtUtc)
            .ThenBy(item => item.SourceReference, StringComparer.Ordinal)
            .ThenBy(item => item.Fact)
            .ToArray();
        if (supporting.Length == 0) return new NasdaqHumanH4ContextSelection.Missing();
        var fact = supporting[0].Fact;
        return supporting.All(item => item.Fact.Equals(fact))
            ? new NasdaqHumanH4ContextSelection.Unique(fact, supporting)
            : new NasdaqHumanH4ContextSelection.Conflict(supporting);
    }
}

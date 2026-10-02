using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Resolves a selected final cluster without calculating geometry or discovering members.</summary>
public sealed class NasdaqHumanPostCompletionActiveExtremeMemberResolver
{
    public NasdaqHumanPostCompletionActiveExtremeMemberResolution Evaluate(
        NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique selection, StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(context);
        var evt = selection.SupportingObservations[0].MembershipEvent;
        var identity = evt.Episode.PreviousCompletedEpisode;
        if (context.StrategyId != identity.StrategyId || context.StrategyVersion != identity.StrategyVersion
            || context.ProviderId != identity.ProviderId || context.Symbol != identity.Symbol)
            throw new ArgumentException("The selected event must match the replay context identity.", nameof(context));
        if (selection.SupportingObservations.Any(observation => !context.InputObservations.Contains(observation)))
            throw new ArgumentException("The selected evidence must already be visible in this context.", nameof(selection));

        var turn = evt.CorrectionStartCandle;
        // A later same-series open cannot precede the turn in non-overlapping chronological history.
        // Reject a provably later reference even when its exact source interval is unavailable.
        if (selection.SemanticMemberOpenTimesUtc.Any(open => open > turn.OpenTimeUtc))
            throw new ArgumentException("Selected members cannot originate after the turn candle.", nameof(selection));
        if (!context.TryGetFrame(identity.Timeframe, out var frame))
            return new NasdaqHumanPostCompletionActiveExtremeMemberResolution.DataUnavailable(selection,
                selection.SemanticMemberOpenTimesUtc.Append(turn.OpenTimeUtc));

        // ReplayFrame guarantees this exact series, unique chronological opens and closed AsOfUtc visibility.
        var available = frame!.AvailableCandles;
        var byOpen = available.ToDictionary(candle => candle.OpenTimeUtc);
        var missing = new List<DateTimeOffset>();
        if (!byOpen.TryGetValue(turn.OpenTimeUtc, out var actualTurn))
            missing.Add(turn.OpenTimeUtc);
        else if (actualTurn.CloseTimeUtc != turn.CloseTimeUtc || actualTurn.Open != turn.Open
            || actualTurn.High != turn.High || actualTurn.Low != turn.Low || actualTurn.Close != turn.Close
            || actualTurn.Volume != turn.Volume)
            throw new ArgumentException("The source turn candle must match the established boundary.", nameof(context));

        var members = new List<Candle>();
        foreach (var open in selection.SemanticMemberOpenTimesUtc)
        {
            if (!byOpen.TryGetValue(open, out var candle))
            {
                missing.Add(open);
                continue;
            }
            if (candle.CloseTimeUtc > turn.CloseTimeUtc)
                throw new ArgumentException("Selected members must close no later than the turn.", nameof(selection));
            members.Add(candle);
        }

        var first = selection.SemanticMemberOpenTimesUtc[0];
        var last = selection.SemanticMemberOpenTimesUtc[^1];
        var selected = selection.SemanticMemberOpenTimesUtc.ToHashSet();
        if (available.Any(candle => candle.OpenTimeUtc >= first && candle.OpenTimeUtc <= last
            && !selected.Contains(candle.OpenTimeUtc)))
            throw new ArgumentException("Selected members must be contiguous among available H4 candles.", nameof(selection));

        if (missing.Count > 0)
            return new NasdaqHumanPostCompletionActiveExtremeMemberResolution.DataUnavailable(selection, missing);
        return new NasdaqHumanPostCompletionActiveExtremeMemberResolution.Resolved(selection, members);
    }
}

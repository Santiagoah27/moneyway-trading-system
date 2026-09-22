using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>
/// Materializes one already-visible human observation from the context's closed H4 snapshot.
/// The caller owns selection among observations and validation of the structural transition.
/// </summary>
public sealed class NasdaqHumanOriginVertexMemberResolver
{
    public NasdaqHumanOriginVertexMemberResolution Evaluate(
        NasdaqHumanOriginVertexObservation observation,
        StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(context);

        if (observation.StrategyId != context.StrategyId || observation.StrategyVersion != context.StrategyVersion
            || observation.ProviderId != context.ProviderId || observation.Symbol != context.Symbol)
            throw new ArgumentException("Origin vertex observation must match the replay context identity.", nameof(observation));
        if (!context.InputObservations.Contains(observation))
            throw new ArgumentException("Origin vertex observation must be visible in the replay context.", nameof(observation));
        if (!context.TryGetFrame(NasdaqHumanOriginVertexObservation.H4, out var frame))
            throw new InvalidOperationException("An observable H4 candle frame is required.");

        // ReplayFrame guarantees unique OpenTimeUtc and CloseTimeUtc <= its AsOfUtc.
        var invalidating = frame!.AvailableCandles.FirstOrDefault(
            candle => candle.OpenTimeUtc == observation.InvalidatingCandleOpenTimeUtc)
            ?? throw new InvalidOperationException("The invalidating H4 candle is not observable.");

        var members = new List<Candle>(observation.SelectedMemberOpenTimesUtc.Count);
        foreach (var openTimeUtc in observation.SelectedMemberOpenTimesUtc)
        {
            var member = frame.AvailableCandles.FirstOrDefault(candle => candle.OpenTimeUtc == openTimeUtc)
                ?? throw new InvalidOperationException("A selected H4 origin vertex member is not observable.");
            if (member.CloseTimeUtc > invalidating.OpenTimeUtc)
                throw new InvalidOperationException("Selected origin vertex members must close before invalidation begins.");
            members.Add(member);
        }

        var episode = new NasdaqHumanOriginVertexEpisode(
            observation.StrategyId,
            observation.StrategyVersion,
            observation.ProviderId,
            observation.Symbol,
            observation.InvalidatingCandleOpenTimeUtc);
        return new NasdaqHumanOriginVertexMemberResolution(episode, invalidating, members);
    }
}

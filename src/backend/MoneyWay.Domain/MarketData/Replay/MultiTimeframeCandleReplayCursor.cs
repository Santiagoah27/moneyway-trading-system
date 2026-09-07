using System.Collections.ObjectModel;

namespace MoneyWay.Domain.MarketData.Replay;

/// <summary>
/// Synchronizes multiple candle series for one provider and symbol by their actual historical close times.
/// It advances equal-time closes atomically and exposes neither source series nor future scheduling state.
/// </summary>
public sealed class MultiTimeframeCandleReplayCursor
{
    private readonly IReadOnlyList<SeriesState> states;
    private readonly Dictionary<Timeframe, ReplayFrame> latestFrames = [];
    private int stepsCompleted;

    public MultiTimeframeCandleReplayCursor(IEnumerable<CandleSeries> series)
    {
        ArgumentNullException.ThrowIfNull(series); var snapshot = series.ToArray();
        if (snapshot.Length == 0) throw new ArgumentException("At least one candle series is required.", nameof(series));
        if (snapshot.Any(x => x is null)) throw new ArgumentException("Series cannot contain null elements.", nameof(series));
        var first = snapshot[0];
        if (snapshot.Any(x => x.ProviderId != first.ProviderId)) throw new ArgumentException("All series must use the same provider.", nameof(series));
        if (snapshot.Any(x => x.Symbol != first.Symbol)) throw new ArgumentException("All series must use the same symbol.", nameof(series));
        if (snapshot.GroupBy(x => x.Timeframe).Any(x => x.Count() > 1)) throw new ArgumentException("Timeframes must be unique.", nameof(series));
        var ordered = MultiTimeframeReplayFrame.Order(snapshot.Select(x => x.Timeframe)).ToArray();
        ProviderId = first.ProviderId; Symbol = first.Symbol; ConfiguredTimeframes = new ReadOnlyCollection<Timeframe>(ordered);
        states = new ReadOnlyCollection<SeriesState>(ordered.Select(timeframe => new SeriesState(snapshot.Single(x => x.Timeframe == timeframe))).ToArray());
    }
    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public IReadOnlyList<Timeframe> ConfiguredTimeframes { get; }
    public int StepsCompleted => stepsCompleted;
    public bool TryAdvance(out MultiTimeframeReplayFrame? frame)
    {
        var pending = states.Where(x => x.NextIndex < x.Source.Count).ToArray();
        if (pending.Length == 0) { frame = null; return false; }
        var asOfUtc = pending.Min(x => x.Source.Candles[x.NextIndex].CloseTimeUtc);
        var updated = pending.Where(x => x.Source.Candles[x.NextIndex].CloseTimeUtc == asOfUtc).ToArray();
        foreach (var state in updated)
        {
            if (!state.Cursor.TryAdvance(out var child) || child is null) throw new InvalidOperationException("Child replay cursor ended unexpectedly.");
            state.NextIndex++; latestFrames[state.Source.Timeframe] = child;
        }
        stepsCompleted++;
        frame = new MultiTimeframeReplayFrame(ProviderId, Symbol, stepsCompleted, asOfUtc, ConfiguredTimeframes,
            MultiTimeframeReplayFrame.Order(updated.Select(x => x.Source.Timeframe)), latestFrames);
        return true;
    }
    private sealed class SeriesState(CandleSeries source)
    {
        public CandleSeries Source { get; } = source; public CandleReplayCursor Cursor { get; } = new(source); public int NextIndex { get; set; }
    }
}

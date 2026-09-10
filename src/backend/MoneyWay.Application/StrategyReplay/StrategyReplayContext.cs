using System.Collections.ObjectModel;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>
/// Represents the market data observable for one exact strategy version at one canonical historical replay step.
/// It exposes only already-closed candle frames and the bounded prefix of optional price observations visible at
/// <see cref="AsOfUtc"/>. It contains no future scheduling information.
/// </summary>
public sealed class StrategyReplayContext
{
    private readonly IReadOnlyDictionary<Timeframe, ReplayFrame> framesByTimeframe;

    internal StrategyReplayContext(StrategyId strategyId, StrategyVersion strategyVersion, MultiTimeframeReplayFrame replayFrame)
    {
        ArgumentNullException.ThrowIfNull(strategyId); ArgumentNullException.ThrowIfNull(strategyVersion); ArgumentNullException.ThrowIfNull(replayFrame);
        StrategyId = strategyId; StrategyVersion = strategyVersion; ProviderId = replayFrame.ProviderId; Symbol = replayFrame.Symbol;
        Step = replayFrame.Step; AsOfUtc = replayFrame.AsOfUtc;
        var configured = replayFrame.ConfiguredTimeframes.ToArray(); var updated = replayFrame.UpdatedTimeframes.ToArray();
        var frames = replayFrame.FramesByTimeframe.ToDictionary(pair => pair.Key, pair => pair.Value);
        ConfiguredTimeframes = new ReadOnlyCollection<Timeframe>(configured);
        UpdatedTimeframes = new ReadOnlyCollection<Timeframe>(updated);
        AvailableTimeframes = new ReadOnlyCollection<Timeframe>(configured.Where(frames.ContainsKey).ToArray());
        framesByTimeframe = new ReadOnlyDictionary<Timeframe, ReplayFrame>(frames);
        MarketDataAvailability = ReplayMarketDataAvailability.CandleOnly;
        MarketPriceObservations = new(ProviderId, Symbol, null, 0, 0);
    }

    internal StrategyReplayContext(StrategyId strategyId, StrategyVersion strategyVersion, CanonicalMultiTimeframeReplayFrame replayFrame)
    {
        ArgumentNullException.ThrowIfNull(strategyId); ArgumentNullException.ThrowIfNull(strategyVersion); ArgumentNullException.ThrowIfNull(replayFrame);
        StrategyId = strategyId; StrategyVersion = strategyVersion; ProviderId = replayFrame.ProviderId; Symbol = replayFrame.Symbol;
        Step = replayFrame.Step; AsOfUtc = replayFrame.AsOfUtc;
        var configured = replayFrame.ConfiguredTimeframes.ToArray(); var updated = replayFrame.UpdatedTimeframes.ToArray();
        var frames = replayFrame.FramesByTimeframe.ToDictionary(pair => pair.Key, pair => pair.Value);
        ConfiguredTimeframes = new ReadOnlyCollection<Timeframe>(configured);
        UpdatedTimeframes = new ReadOnlyCollection<Timeframe>(updated);
        AvailableTimeframes = new ReadOnlyCollection<Timeframe>(configured.Where(frames.ContainsKey).ToArray());
        framesByTimeframe = new ReadOnlyDictionary<Timeframe, ReplayFrame>(frames);
        CurrentMarketPriceObservations = replayFrame.CurrentMarketPriceObservations;
        MarketPriceObservations = replayFrame.MarketPriceObservations;
        MarketDataAvailability = replayFrame.MarketDataAvailability;
    }

    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public int Step { get; }
    public DateTimeOffset AsOfUtc { get; }
    public IReadOnlyList<Timeframe> ConfiguredTimeframes { get; }
    public IReadOnlyList<Timeframe> UpdatedTimeframes { get; }
    public IReadOnlyList<Timeframe> AvailableTimeframes { get; }
    public HistoricalMarketPriceObservationGroup? CurrentMarketPriceObservations { get; }
    public HistoricalMarketPriceObservationSnapshot MarketPriceObservations { get; }
    public ReplayMarketDataAvailability MarketDataAvailability { get; }

    public bool IsConfigured(Timeframe timeframe) { ArgumentNullException.ThrowIfNull(timeframe); return ConfiguredTimeframes.Contains(timeframe); }
    public bool IsAvailable(Timeframe timeframe) { ArgumentNullException.ThrowIfNull(timeframe); return framesByTimeframe.ContainsKey(timeframe); }
    public bool WasUpdated(Timeframe timeframe) { ArgumentNullException.ThrowIfNull(timeframe); return UpdatedTimeframes.Contains(timeframe); }
    public bool TryGetFrame(Timeframe timeframe, out ReplayFrame? frame) { ArgumentNullException.ThrowIfNull(timeframe); return framesByTimeframe.TryGetValue(timeframe, out frame); }
}

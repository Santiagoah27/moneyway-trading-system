using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>
/// Represents the inputs observable for one exact strategy version at one canonical historical replay step.
/// It exposes only already-closed candle frames and bounded optional observations visible at
/// <see cref="AsOfUtc"/>. It contains no future scheduling information.
/// </summary>
public sealed class StrategyReplayContext
{
    private readonly IReadOnlyDictionary<Timeframe, ReplayFrame> framesByTimeframe;

    internal StrategyReplayContext(StrategyId strategyId, StrategyVersion strategyVersion, MultiTimeframeReplayFrame replayFrame,
        IEnumerable<IStrategyReplayInputObservation>? inputObservations = null,
        IStrategyReplayPreEvaluationState? preEvaluationState = null)
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
        InputObservations = BoundInputObservations(inputObservations);
        PreEvaluationState = BoundPreEvaluationState(preEvaluationState);
    }

    internal StrategyReplayContext(StrategyId strategyId, StrategyVersion strategyVersion, CanonicalMultiTimeframeReplayFrame replayFrame,
        IEnumerable<IStrategyReplayInputObservation>? inputObservations = null,
        IStrategyReplayPreEvaluationState? preEvaluationState = null)
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
        InputObservations = BoundInputObservations(inputObservations);
        PreEvaluationState = BoundPreEvaluationState(preEvaluationState);
    }

    private StrategyReplayContext(StrategyReplayContext source, IEnumerable<StrategyReplayContextObservation> priorObservations,
        IEnumerable<IStrategyReplayInputObservation>? inputObservations = null)
    {
        var prior = priorObservations.ToArray();
        if (prior.Any(o => o is null || o.StrategyId != source.StrategyId || o.StrategyVersion != source.StrategyVersion
            || o.ProviderId != source.ProviderId || o.Symbol != source.Symbol || o.AsOfUtc >= source.AsOfUtc || o.Step >= source.Step)
            || prior.Where((o, i) => i > 0 && (o.AsOfUtc <= prior[i - 1].AsOfUtc || o.Step <= prior[i - 1].Step)).Any())
            throw new ArgumentException("Prior observations must match identity and be strictly chronological before this frame.", nameof(priorObservations));
        StrategyId = source.StrategyId; StrategyVersion = source.StrategyVersion; ProviderId = source.ProviderId; Symbol = source.Symbol;
        Step = source.Step; AsOfUtc = source.AsOfUtc; ConfiguredTimeframes = source.ConfiguredTimeframes;
        UpdatedTimeframes = source.UpdatedTimeframes; AvailableTimeframes = source.AvailableTimeframes;
        framesByTimeframe = source.framesByTimeframe; CurrentMarketPriceObservations = source.CurrentMarketPriceObservations;
        MarketPriceObservations = source.MarketPriceObservations; MarketDataAvailability = source.MarketDataAvailability;
        InputObservations = inputObservations is null ? source.InputObservations : BoundInputObservations(inputObservations); PreEvaluationState = source.PreEvaluationState;
        PriorObservations = new ReadOnlyCollection<StrategyReplayContextObservation>(prior);
    }

    public StrategyReplayContext WithPriorObservations(IEnumerable<StrategyReplayContextObservation> priorObservations)
    {
        ArgumentNullException.ThrowIfNull(priorObservations);
        return new(this, priorObservations);
    }

    internal StrategyReplayContext WithInputObservations(IEnumerable<IStrategyReplayInputObservation> inputs) =>
        new(this, PriorObservations, inputs);

    public IReadOnlyList<StrategyReplayContextObservation> PriorObservations { get; } = Array.Empty<StrategyReplayContextObservation>();

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
    public IReadOnlyList<IStrategyReplayInputObservation> InputObservations { get; }
    public IStrategyReplayPreEvaluationState? PreEvaluationState { get; }

    public bool TryGetPreEvaluationState<T>([NotNullWhen(true)] out T? state)
        where T : class, IStrategyReplayPreEvaluationState
    {
        state = PreEvaluationState as T;
        return state is not null;
    }

    public bool IsConfigured(Timeframe timeframe) { ArgumentNullException.ThrowIfNull(timeframe); return ConfiguredTimeframes.Contains(timeframe); }
    public bool IsAvailable(Timeframe timeframe) { ArgumentNullException.ThrowIfNull(timeframe); return framesByTimeframe.ContainsKey(timeframe); }
    public bool WasUpdated(Timeframe timeframe) { ArgumentNullException.ThrowIfNull(timeframe); return UpdatedTimeframes.Contains(timeframe); }
    public bool TryGetFrame(Timeframe timeframe, out ReplayFrame? frame) { ArgumentNullException.ThrowIfNull(timeframe); return framesByTimeframe.TryGetValue(timeframe, out frame); }

    private IReadOnlyList<IStrategyReplayInputObservation> BoundInputObservations(IEnumerable<IStrategyReplayInputObservation>? input)
    {
        var snapshot = (input ?? []).ToArray();
        if (snapshot.Any(item => item is null || item.StrategyId != StrategyId || item.StrategyVersion != StrategyVersion
            || item.ProviderId != ProviderId || item.Symbol != Symbol || item.ObservedAtUtc.Offset != TimeSpan.Zero))
            throw new ArgumentException("Input observations must match the context identity and use UTC timestamps.", nameof(input));
        return new ReadOnlyCollection<IStrategyReplayInputObservation>(
            snapshot.Where(item => item.ObservedAtUtc <= AsOfUtc).ToArray());
    }

    private IStrategyReplayPreEvaluationState? BoundPreEvaluationState(IStrategyReplayPreEvaluationState? state)
    {
        if (state is not null && (state.StrategyId != StrategyId || state.StrategyVersion != StrategyVersion
            || state.ProviderId != ProviderId || state.Symbol != Symbol))
            throw new ArgumentException("Pre-evaluation state must match the exact replay identity.", nameof(state));
        return state;
    }
}

using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Human-selected mandatory FVG classification; no price geometry or quality is inferred.</summary>
public sealed class NasdaqHumanM5FvgEvent
{
    public NasdaqHumanM5FvgEvent(IEnumerable<Candle> sourceCandles)
    {
        ArgumentNullException.ThrowIfNull(sourceCandles);
        var sources = sourceCandles.ToArray();
        if (sources.Length != 3 || sources.Any(c => c is null || c.Timeframe != NasdaqHumanM5TriggerEvent.M5)
            || sources.Select(c => c.OpenTimeUtc).Distinct().Count() != 3)
            throw new ArgumentException("Exactly three distinct 5M source candles are required.", nameof(sourceCandles));
        sources = sources.OrderBy(c => c.OpenTimeUtc).ToArray();
        if (sources.Any(c => c.ProviderId != sources[0].ProviderId || c.Symbol != sources[0].Symbol)
            || sources[0].CloseTimeUtc > sources[1].OpenTimeUtc || sources[1].CloseTimeUtc > sources[2].OpenTimeUtc)
            throw new ArgumentException("Source candles must be ordered, non-overlapping and in the same series.", nameof(sourceCandles));
        SourceCandles = new ReadOnlyCollection<Candle>(sources);
    }
    public IReadOnlyList<Candle> SourceCandles { get; }
    public Candle Candle1 => SourceCandles[0];
    public Candle Candle2 => SourceCandles[1];
    public Candle Candle3 => SourceCandles[2];
    public DateTimeOffset EffectiveAtUtc => Candle3.CloseTimeUtc;
    internal bool SameFact(NasdaqHumanM5FvgEvent other) => SourceCandles.Zip(other.SourceCandles)
        .All(pair => NasdaqStructuralLiquidityReference.SameCandle(pair.First, pair.Second));
}

using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Human-confirmed countertrend movement toward or into the selected FVG.</summary>
public sealed class NasdaqHumanM1CorrectiveRetracementEvent
{
    public static Timeframe M1 { get; } = new(1, TimeframeUnit.Minute);

    public NasdaqHumanM1CorrectiveRetracementEvent(IEnumerable<Candle> sourceCandles)
    {
        ArgumentNullException.ThrowIfNull(sourceCandles);
        var sources = sourceCandles.ToArray();
        if (sources.Length == 0 || sources.Any(c => c is null || c.Timeframe != M1)
            || sources.Select(c => c.OpenTimeUtc).Distinct().Count() != sources.Length)
            throw new ArgumentException("Distinct 1M closed source candles are required.", nameof(sourceCandles));
        sources = sources.OrderBy(c => c.OpenTimeUtc).ToArray();
        if (sources.Any(c => c.ProviderId != sources[0].ProviderId || c.Symbol != sources[0].Symbol)
            || sources.Where((c, i) => i > 0 && sources[i - 1].CloseTimeUtc > c.OpenTimeUtc).Any())
            throw new ArgumentException("1M sources must belong to one non-overlapping series.", nameof(sourceCandles));
        SourceCandles = new ReadOnlyCollection<Candle>(sources);
    }

    public IReadOnlyList<Candle> SourceCandles { get; }
    public DateTimeOffset PullbackEffectiveAtUtc => SourceCandles[^1].CloseTimeUtc;

    internal bool SameFact(NasdaqHumanM1CorrectiveRetracementEvent other) => SourceCandles.Count == other.SourceCandles.Count
        && SourceCandles.Zip(other.SourceCandles).All(pair => NasdaqStructuralLiquidityReference.SameCandle(pair.First, pair.Second));
}

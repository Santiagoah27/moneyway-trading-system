using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

public enum NasdaqM5ProtectionAnchorKind { HigherLow, LowerHigh }

/// <summary>Exact human-selected 5M structural turn members and their wick protection coordinate.</summary>
public sealed class NasdaqHumanM5ProtectionAnchor
{
    public static readonly Timeframe M5 = new(5, TimeframeUnit.Minute);

    public NasdaqHumanM5ProtectionAnchor(NasdaqM5ProtectionAnchorKind kind, IEnumerable<Candle> selectedTurnCandles)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        ArgumentNullException.ThrowIfNull(selectedTurnCandles);
        var sources = selectedTurnCandles.ToArray();
        if (sources.Length == 0 || sources.Any(c => c is null || c.Timeframe != M5))
            throw new ArgumentException("A protection anchor requires exact 5M turn members.", nameof(selectedTurnCandles));
        var first = sources[0];
        if (sources.Any(c => c.ProviderId != first.ProviderId || c.Symbol != first.Symbol)
            || sources.Select(c => c.OpenTimeUtc).Distinct().Count() != sources.Length)
            throw new ArgumentException("Protection members must be unique candles in one exact source series.", nameof(selectedTurnCandles));
        Kind = kind;
        SelectedTurnCandles = new ReadOnlyCollection<Candle>(sources.OrderBy(c => c.OpenTimeUtc).ToArray());
        ProtectionAnchorPrice = kind == NasdaqM5ProtectionAnchorKind.HigherLow
            ? sources.Min(c => c.Low) : sources.Max(c => c.High);
    }

    public NasdaqM5ProtectionAnchorKind Kind { get; }
    public IReadOnlyList<Candle> SelectedTurnCandles { get; }
    public decimal ProtectionAnchorPrice { get; }

    internal bool SameFact(NasdaqHumanM5ProtectionAnchor other) => Kind == other.Kind
        && ProtectionAnchorPrice == other.ProtectionAnchorPrice
        && SelectedTurnCandles.Count == other.SelectedTurnCandles.Count
        && SelectedTurnCandles.Zip(other.SelectedTurnCandles)
            .All(pair => NasdaqStructuralLiquidityReference.SameCandle(pair.First, pair.Second));
}

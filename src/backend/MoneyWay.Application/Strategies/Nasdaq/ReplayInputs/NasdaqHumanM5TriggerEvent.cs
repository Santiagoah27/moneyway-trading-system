using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

public enum NasdaqHumanM5TriggerKind { StructuralChange, Ifvg }

/// <summary>Human-confirmed internal order, never inferred from OHLC or timestamp equality.</summary>
public enum NasdaqHumanM5TakeTriggerOrder { Unspecified, TakeBeforeTriggerConfirmation }

/// <summary>A human-classified close-confirmed event. Sources identify reviewed facts, not a detection algorithm.</summary>
public abstract class NasdaqHumanM5TriggerEvent
{
    public static Timeframe M5 { get; } = new(5, TimeframeUnit.Minute);
    private NasdaqHumanM5TriggerEvent(Candle confirmation, NasdaqHumanH4PermittedDirection direction,
        IEnumerable<Candle> activeReferenceCandles, IEnumerable<Candle> reviewedSourceCandles)
    {
        ConfirmationCandle = confirmation ?? throw new ArgumentNullException(nameof(confirmation));
        if (direction is not (NasdaqHumanH4PermittedDirection.Buy or NasdaqHumanH4PermittedDirection.Sell))
            throw new ArgumentException("Trigger direction must be resolved.", nameof(direction));
        Direction = direction;
        ActiveReferenceCandles = Sources(activeReferenceCandles);
        ReviewedSourceCandles = Sources(reviewedSourceCandles);
        if (confirmation.Timeframe != M5)
            throw new ArgumentException("Confirmation must be an exact 5M source candle.", nameof(confirmation));
    }
    private IReadOnlyList<Candle> Sources(IEnumerable<Candle> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var snapshot = sources.ToArray();
        if (snapshot.Length == 0 || snapshot.Any(c => c is null || c.Timeframe != M5
            || c.ProviderId != ConfirmationCandle.ProviderId || c.Symbol != ConfirmationCandle.Symbol
            || c.CloseTimeUtc > EffectiveAtUtc) || snapshot.Select(c => c.OpenTimeUtc).Distinct().Count() != snapshot.Length)
            throw new ArgumentException("Reviewed sources must be unique exact 5M candles closed by confirmation, in the same series.", nameof(sources));
        return new ReadOnlyCollection<Candle>(snapshot.OrderBy(c => c.OpenTimeUtc).ToArray());
    }
    public abstract NasdaqHumanM5TriggerKind Kind { get; }
    public Candle ConfirmationCandle { get; }
    public NasdaqHumanH4PermittedDirection Direction { get; }
    public DateTimeOffset EffectiveAtUtc => ConfirmationCandle.CloseTimeUtc;
    public IReadOnlyList<Candle> ActiveReferenceCandles { get; }
    public IReadOnlyList<Candle> ReviewedSourceCandles { get; }
    internal IEnumerable<Candle> SourcesToResolve => ActiveReferenceCandles.Concat(ReviewedSourceCandles).Append(ConfirmationCandle);
    internal bool SameFact(NasdaqHumanM5TriggerEvent other) => Kind == other.Kind && Direction == other.Direction
        && NasdaqStructuralLiquidityReference.SameCandle(ConfirmationCandle, other.ConfirmationCandle)
        && SameSources(ActiveReferenceCandles, other.ActiveReferenceCandles)
        && SameSources(ReviewedSourceCandles, other.ReviewedSourceCandles)
        && (this is not StructuralChange sc || other is StructuralChange otherSc && sc.PriorSwingPrice == otherSc.PriorSwingPrice);
    private static bool SameSources(IReadOnlyList<Candle> one, IReadOnlyList<Candle> two) => one.Count == two.Count
        && one.Zip(two).All(pair => NasdaqStructuralLiquidityReference.SameCandle(pair.First, pair.Second));

    /// <summary>The human asserts the source-backed strong/decisive close beyond the selected prior swing. No threshold is computed.</summary>
    public sealed class StructuralChange : NasdaqHumanM5TriggerEvent
    {
        public StructuralChange(Candle confirmation, NasdaqHumanH4PermittedDirection direction,
            IEnumerable<Candle> activeReferenceCandles, IEnumerable<Candle> priorSwingCandles, decimal priorSwingPrice)
            : base(confirmation, direction, activeReferenceCandles, priorSwingCandles) => PriorSwingPrice = priorSwingPrice;
        public override NasdaqHumanM5TriggerKind Kind => NasdaqHumanM5TriggerKind.StructuralChange;
        public decimal PriorSwingPrice { get; }
    }
    /// <summary>The human asserts inversion of this exact reviewed prior FVG. No gap geometry is computed.</summary>
    public sealed class Ifvg : NasdaqHumanM5TriggerEvent
    {
        public Ifvg(Candle confirmation, NasdaqHumanH4PermittedDirection direction,
            IEnumerable<Candle> activeReferenceCandles, IEnumerable<Candle> priorFvgCandles)
            : base(confirmation, direction, activeReferenceCandles, priorFvgCandles) { }
        public override NasdaqHumanM5TriggerKind Kind => NasdaqHumanM5TriggerKind.Ifvg;
    }
}

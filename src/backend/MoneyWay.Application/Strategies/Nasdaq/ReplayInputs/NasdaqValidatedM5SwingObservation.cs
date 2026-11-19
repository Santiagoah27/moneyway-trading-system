using System.Collections.ObjectModel;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

public enum NasdaqM5SwingValidation { AwaitingHumanValidation, Validated }

/// <summary>A selected reference, not an assertion that any later candle is an SC.</summary>
public sealed class NasdaqValidatedM5SwingObservation : IStrategyReplayInputObservation
{
    public NasdaqValidatedM5SwingObservation(NasdaqHumanLiquidityTakeObservation decisiveTake, string referenceId,
        NasdaqStructuralLiquiditySide side, decimal price, string priceUnit, IEnumerable<Candle> sourceCandles,
        DateTimeOffset effectiveAtUtc, DateTimeOffset observedAtUtc, string sourceReference,
        NasdaqM5SwingValidation validation = NasdaqM5SwingValidation.Validated)
    {
        DecisiveTake = decisiveTake ?? throw new ArgumentNullException(nameof(decisiveTake));
        if (string.IsNullOrWhiteSpace(referenceId) || referenceId != referenceId.Trim() || referenceId.Any(char.IsControl)) throw new ArgumentException("Explicit selected swing identity required.");
        if (!Enum.IsDefined(side) || !Enum.IsDefined(validation) || priceUnit != "NativeInstrumentPrice") throw new ArgumentException("Exact native price, swing side and validation state required.");
        ArgumentNullException.ThrowIfNull(sourceCandles); var sources = sourceCandles.ToArray();
        if (sources.Length == 0 || sources.Any(c => c is null || c.ProviderId != ProviderId || c.Symbol != Symbol || c.Timeframe != NasdaqHumanM5TriggerEvent.M5 || c.CloseTimeUtc > effectiveAtUtc)
            || sources.Select(c => c.OpenTimeUtc).Distinct().Count() != sources.Length) throw new ArgumentException("Exact closed M5 swing sources required.");
        if (effectiveAtUtc.Offset != TimeSpan.Zero || observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < effectiveAtUtc || observedAtUtc < decisiveTake.ObservedAtUtc)
            throw new ArgumentException("Reference availability cannot precede validation or the relied-upon take.");
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        ReferenceId = referenceId; Side = side; Price = price; PriceUnit = priceUnit; Validation = validation;
        SourceCandles = new ReadOnlyCollection<Candle>(sources.OrderBy(c => c.OpenTimeUtc).ToArray());
        EffectiveAtUtc = effectiveAtUtc; ObservedAtUtc = observedAtUtc; SourceReference = sourceReference;
    }
    public NasdaqHumanLiquidityTakeObservation DecisiveTake { get; }
    public NasdaqDemoSessionIdentity Session => DecisiveTake.Session;
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;
    public string ReferenceId { get; }
    public NasdaqStructuralLiquiditySide Side { get; }
    public decimal Price { get; }
    public string PriceUnit { get; }
    public NasdaqM5SwingValidation Validation { get; }
    public IReadOnlyList<Candle> SourceCandles { get; }
    public DateTimeOffset EffectiveAtUtc { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
    internal bool SameSelection(NasdaqValidatedM5SwingObservation other) => ReferenceId == other.ReferenceId && Side == other.Side && Price == other.Price
        && Validation == other.Validation && EffectiveAtUtc == other.EffectiveAtUtc && SourceCandles.Count == other.SourceCandles.Count
        && SourceCandles.Zip(other.SourceCandles).All(p => NasdaqStructuralLiquidityReference.SameCandle(p.First, p.Second));
}

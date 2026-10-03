using System.Text.Json;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Positive human take evidence. Historical availability is an authentic source assertion, never backdated from OHLC.</summary>
public sealed class NasdaqHumanLiquidityTakeObservation : IStrategyReplayInputObservation
{
    public NasdaqHumanLiquidityTakeObservation(NasdaqLiquidityTakeReference reference, NasdaqHumanLiquidityTakeEvent marketEvent,
        DateTimeOffset referenceEligibleAtUtc, DateTimeOffset observedAtUtc, string sourceReference)
    {
        Reference = reference ?? throw new ArgumentNullException(nameof(reference));
        Event = marketEvent ?? throw new ArgumentNullException(nameof(marketEvent));
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        if (marketEvent.ProviderId != reference.Session.ProviderId || marketEvent.Symbol != reference.Session.Symbol)
            throw new ArgumentException("Take event must match the exact upstream source and instrument.");
        if (referenceEligibleAtUtc.Offset != TimeSpan.Zero || observedAtUtc.Offset != TimeSpan.Zero
            || referenceEligibleAtUtc < reference.AvailableAtUtc || referenceEligibleAtUtc > marketEvent.EffectiveAtUtc
            || marketEvent.EffectiveAtUtc > observedAtUtc)
            throw new ArgumentException("Reference availability <= eligibility <= exact take time <= assertion availability must hold in UTC.");
        if (reference.Side == NasdaqStructuralLiquiditySide.High ? marketEvent.ObservedPrice <= reference.ReferencePrice
            : marketEvent.ObservedPrice >= reference.ReferencePrice)
            throw new ArgumentException("Positive take evidence requires strict penetration; equality is invalid.", nameof(marketEvent));
        if (marketEvent is NasdaqHumanLiquidityTakeEvent.Documented d && d.SupportingCandles.Any(c => c.CloseTimeUtc > observedAtUtc))
            throw new ArgumentException("All supporting candles must be closed by assertion availability.");
        ReferenceEligibleAtUtc = referenceEligibleAtUtc; ObservedAtUtc = observedAtUtc; SourceReference = sourceReference;
    }

    public NasdaqLiquidityTakeReference Reference { get; }
    public NasdaqHumanLiquidityTakeEvent Event { get; }
    public NasdaqDemoSessionIdentity Session => Reference.Session;
    public decimal ReferencePrice => Reference.ReferencePrice;
    public NasdaqStructuralLiquiditySide Side => Reference.Side;
    public decimal ObservedPrice => Event.ObservedPrice;
    public DateTimeOffset EffectiveAtUtc => Event.EffectiveAtUtc;
    public DateTimeOffset ReferenceEligibleAtUtc { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;
    internal string AuditKey => JsonSerializer.Serialize(new
    {
        SourceReference,
        ReferenceSupport = Reference switch
        {
            NasdaqLiquidityTakeReference.SessionLevel s => JsonSerializer.Serialize(new { s.SourceReference }),
            NasdaqLiquidityTakeReference.Structural s => JsonSerializer.Serialize(s.Selection.SupportingObservations.Select(o => new
            {
                o.SourceReference,
                o.ObservedAtUtc,
                References = o.ProvenanceKey,
            })),
            _ => throw new InvalidOperationException("Unknown closed reference branch."),
        },
        EventSupport = Event switch
        {
            NasdaqHumanLiquidityTakeEvent.Documented d => JsonSerializer.Serialize(new { d.SourceReference, d.SupportingCandles }),
            NasdaqHumanLiquidityTakeEvent.CanonicalPrice c => JsonSerializer.Serialize(c.Observation),
            _ => throw new InvalidOperationException("Unknown closed event branch."),
        },
    });
    internal bool SameFact(NasdaqHumanLiquidityTakeObservation other) => Reference.SameSlot(other.Reference)
        && ReferenceEligibleAtUtc == other.ReferenceEligibleAtUtc && Event.SameFact(other.Event);
}

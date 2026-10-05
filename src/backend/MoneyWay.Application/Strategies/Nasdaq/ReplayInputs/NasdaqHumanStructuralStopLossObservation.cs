using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Contemporaneous human selection of structural protection and a separately documented stop parameter.</summary>
public sealed class NasdaqHumanStructuralStopLossObservation : IStrategyReplayInputObservation
{
    public NasdaqHumanStructuralStopLossObservation(NasdaqPreEntryEligibilityRuleFact preEntryEligibility,
        NasdaqHumanM5ProtectionAnchor protectionAnchor, decimal stopPrice, DateTimeOffset effectiveAtUtc,
        DateTimeOffset observedAtUtc, string sourceReference)
    {
        PreEntryEligibility = preEntryEligibility ?? throw new ArgumentNullException(nameof(preEntryEligibility));
        ProtectionAnchor = protectionAnchor ?? throw new ArgumentNullException(nameof(protectionAnchor));
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        if (protectionAnchor.SelectedTurnCandles.Any(c => c.ProviderId != Session.ProviderId || c.Symbol != Session.Symbol))
            throw new ArgumentException("Protection sources must match the exact eligible setup market series.", nameof(protectionAnchor));
        if (Direction == NasdaqHumanH4PermittedDirection.Buy
            ? protectionAnchor.Kind != NasdaqM5ProtectionAnchorKind.HigherLow || stopPrice >= protectionAnchor.ProtectionAnchorPrice
            : protectionAnchor.Kind != NasdaqM5ProtectionAnchorKind.LowerHigh || stopPrice <= protectionAnchor.ProtectionAnchorPrice)
            throw new ArgumentException("The documented stop must protect the directionally selected HL/LH wick anchor.", nameof(stopPrice));
        if (effectiveAtUtc.Offset != TimeSpan.Zero || protectionAnchor.SelectedTurnCandles.Any(c => c.CloseTimeUtc > effectiveAtUtc))
            throw new ArgumentException("Structural selection must be UTC and follow its closed 5M sources.", nameof(effectiveAtUtc));
        if (observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < effectiveAtUtc
            || observedAtUtc < preEntryEligibility.Realignment.Selection.Fact.ObservedAtUtc)
            throw new ArgumentException("Availability must follow the selected parameter and relied-upon realignment evidence.", nameof(observedAtUtc));
        StopPrice = stopPrice;
        EffectiveAtUtc = effectiveAtUtc;
        ObservedAtUtc = observedAtUtc;
        SourceReference = sourceReference;
    }

    public NasdaqPreEntryEligibilityRuleFact PreEntryEligibility { get; }
    public NasdaqHumanM5ProtectionAnchor ProtectionAnchor { get; }
    public decimal StopPrice { get; }
    /// <summary>Documented structural selection and explicit stop-parameter time, not the anchor candle close.</summary>
    public DateTimeOffset EffectiveAtUtc { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
    public NasdaqDemoSessionIdentity Session => PreEntryEligibility.Session;
    public NasdaqHumanH4PermittedDirection Direction => PreEntryEligibility.Direction;
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;

    internal bool SameFact(NasdaqHumanStructuralStopLossObservation other) =>
        PreEntryEligibility.Realignment.Selection.Fact.SameFact(other.PreEntryEligibility.Realignment.Selection.Fact)
        && Direction == other.Direction && ProtectionAnchor.SameFact(other.ProtectionAnchor)
        && StopPrice == other.StopPrice && EffectiveAtUtc == other.EffectiveAtUtc;
}

using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Human assertion of intended-direction realignment for one exact canonical pullback.</summary>
public sealed class NasdaqHumanM1RealignmentObservation : IStrategyReplayInputObservation
{
    public NasdaqHumanM1RealignmentObservation(NasdaqHumanM1CorrectiveRetracementRuleFact pullback,
        NasdaqHumanM1RealignmentEvent marketEvent, DateTimeOffset observedAtUtc, string sourceReference,
        bool pullbackBeforeRealignmentAtSameClose = false)
    {
        Pullback = pullback ?? throw new ArgumentNullException(nameof(pullback));
        Event = marketEvent ?? throw new ArgumentNullException(nameof(marketEvent));
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        if (marketEvent.Direction != SetupDirection
            || marketEvent.ConfirmationCandle.ProviderId != Session.ProviderId
            || marketEvent.ConfirmationCandle.Symbol != Session.Symbol)
            throw new ArgumentException("Realignment must match the exact pullback setup direction and source series.", nameof(marketEvent));
        if (marketEvent.RealignmentEffectiveAtUtc < PullbackEffectiveAtUtc)
            throw new ArgumentException("Realignment confirmation cannot precede its exact pullback.", nameof(marketEvent));
        if (marketEvent.RealignmentEffectiveAtUtc == PullbackEffectiveAtUtc && !pullbackBeforeRealignmentAtSameClose)
            throw new ArgumentException("Same-close realignment requires an explicit human causal-order assertion.", nameof(pullbackBeforeRealignmentAtSameClose));
        if (observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < marketEvent.RealignmentEffectiveAtUtc
            || observedAtUtc < pullback.Selection.Fact.ObservedAtUtc)
            throw new ArgumentException("Availability must be UTC and follow the realignment and relied-upon pullback evidence.", nameof(observedAtUtc));
        ObservedAtUtc = observedAtUtc;
        SourceReference = sourceReference;
        PullbackBeforeRealignmentAtSameClose = marketEvent.RealignmentEffectiveAtUtc == PullbackEffectiveAtUtc
            && pullbackBeforeRealignmentAtSameClose;
    }

    public NasdaqHumanM1CorrectiveRetracementRuleFact Pullback { get; }
    public NasdaqHumanM1RealignmentEvent Event { get; }
    public NasdaqDemoSessionIdentity Session => Pullback.Selection.Fact.Session;
    public NasdaqHumanH4PermittedDirection SetupDirection => Pullback.Selection.Fact.SetupDirection;
    public DateTimeOffset PullbackEffectiveAtUtc => Pullback.Selection.Fact.PullbackEffectiveAtUtc;
    public DateTimeOffset RealignmentEffectiveAtUtc => Event.RealignmentEffectiveAtUtc;
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
    public bool PullbackBeforeRealignmentAtSameClose { get; }
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;

    internal bool SameFact(NasdaqHumanM1RealignmentObservation other) =>
        Pullback.ApprovedQuality.Selection.Fact.Fact.SameFact(other.Pullback.ApprovedQuality.Selection.Fact.Fact)
        && Pullback.Selection.Fact.SameFact(other.Pullback.Selection.Fact)
        && SetupDirection == other.SetupDirection && Event.SameFact(other.Event)
        && PullbackBeforeRealignmentAtSameClose == other.PullbackBeforeRealignmentAtSameClose;
}

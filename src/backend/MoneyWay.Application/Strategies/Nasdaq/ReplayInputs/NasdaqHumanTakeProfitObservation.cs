using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Human selection of an exact target for the setup carried by the canonical structural SL.</summary>
public sealed class NasdaqHumanTakeProfitObservation : IStrategyReplayInputObservation
{
    public NasdaqHumanTakeProfitObservation(NasdaqHumanStructuralStopLossRuleFact stopLoss,
        NasdaqHumanTakeProfitTarget target, DateTimeOffset effectiveAtUtc, DateTimeOffset observedAtUtc,
        string sourceReference)
    {
        StopLoss = stopLoss ?? throw new ArgumentNullException(nameof(stopLoss));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        if (target.Reference.Session != stopLoss.Session)
            throw new ArgumentException("Selected target must match the exact upstream session.", nameof(target));
        if (target.Side != (stopLoss.Direction == NasdaqHumanH4PermittedDirection.Buy
                ? NasdaqStructuralLiquiditySide.High : NasdaqStructuralLiquiditySide.Low))
            throw new ArgumentException("Buy selects a High; Sell selects a Low.", nameof(target));
        if (effectiveAtUtc.Offset != TimeSpan.Zero || effectiveAtUtc < target.Reference.AvailableAtUtc
            || target.Reference.Sources.Any(c => c.CloseTimeUtc > effectiveAtUtc))
            throw new ArgumentException("Target selection must be UTC and follow observable target sources.", nameof(effectiveAtUtc));
        if (observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < effectiveAtUtc
            || observedAtUtc < stopLoss.Selection.SupportingObservations.Min(o => o.ObservedAtUtc))
            throw new ArgumentException("Assertion availability must be UTC and follow selection and upstream evidence availability.", nameof(observedAtUtc));
        EffectiveAtUtc = effectiveAtUtc;
        ObservedAtUtc = observedAtUtc;
        SourceReference = sourceReference;
    }

    public NasdaqHumanStructuralStopLossRuleFact StopLoss { get; }
    public NasdaqPreEntryEligibilityRuleFact PreEntryEligibility => StopLoss.PreEntryEligibility;
    public NasdaqHumanTakeProfitTarget Target { get; }
    public NasdaqDemoSessionIdentity Session => StopLoss.Session;
    public NasdaqHumanH4PermittedDirection Direction => StopLoss.Direction;
    /// <summary>Documented target selection time for this exact setup, distinct from source, SL and replay times.</summary>
    public DateTimeOffset EffectiveAtUtc { get; }
    /// <summary>Authentic availability of the human/source assertion to replay.</summary>
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;

    internal bool SameFact(NasdaqHumanTakeProfitObservation other) =>
        StopLoss.Selection.Fact.SameFact(other.StopLoss.Selection.Fact)
        && Target.SameFact(other.Target) && EffectiveAtUtc == other.EffectiveAtUtc;
}

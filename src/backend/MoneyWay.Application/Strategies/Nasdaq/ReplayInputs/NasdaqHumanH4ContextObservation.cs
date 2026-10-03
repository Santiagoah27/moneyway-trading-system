using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Immutable evaluator-driving human H4 review for one preparation session (ADR 0025).</summary>
/// <remarks>SourceReference retains the reviewer/file/mentor evidence location. Retrospective comparison annotations
/// are not this input type and must not be backdated into its authoritative availability timestamp.</remarks>
public sealed record NasdaqHumanH4ContextObservation : IStrategyReplayInputObservation
{
    public static readonly Timeframe H4 = new(4, TimeframeUnit.Hour);

    public NasdaqHumanH4ContextObservation(NasdaqDemoSessionIdentity session, NasdaqHumanH4ContextFact fact,
        DateTimeOffset observedAtUtc, string sourceReference)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(fact);
        ArgumentNullException.ThrowIfNull(sourceReference);
        if (session.StrategyId != MoneyWayNasdaqStrategyDefinition.Instance.StrategyId)
            throw new ArgumentException("Human H4 context must belong to MoneyWay Nasdaq.", nameof(session));
        if (observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < fact.EffectiveAtUtc)
            throw new ArgumentException("Availability must be UTC and cannot precede the effective fact.", nameof(observedAtUtc));
        if (string.IsNullOrWhiteSpace(sourceReference) || sourceReference != sourceReference.Trim())
            throw new ArgumentException("Source reference must be non-empty and trimmed.", nameof(sourceReference));
        Session = session;
        Fact = fact;
        ObservedAtUtc = observedAtUtc;
        SourceReference = sourceReference;
    }

    public NasdaqDemoSessionIdentity Session { get; }
    public NasdaqHumanH4ContextFact Fact { get; }
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;
    public Timeframe Timeframe => H4;
    public DateTimeOffset EffectiveAtUtc => Fact.EffectiveAtUtc;
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
}

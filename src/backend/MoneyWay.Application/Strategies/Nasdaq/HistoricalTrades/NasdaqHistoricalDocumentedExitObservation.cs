using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;

/// <summary>Authentic availability of a retained documented exit and any accompanying causal proof.</summary>
public sealed class NasdaqHistoricalDocumentedExitObservation : IStrategyReplayInputObservation
{
    public NasdaqHistoricalDocumentedExitObservation(NasdaqHistoricalDocumentedExit exit,
        DateTimeOffset observedAtUtc, string sourceReference)
    {
        Exit = exit ?? throw new ArgumentNullException(nameof(exit));
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        if (observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < exit.ExitEffectiveAtUtc
            || exit.EntryBeforeExitEvidence is { } proof && observedAtUtc < proof.ObservedAtUtc
            || exit.SupportingCandles.Any(c => c.CloseTimeUtc > observedAtUtc))
            throw new ArgumentException("Availability must be UTC and follow exit, causal proof and supporting records.", nameof(observedAtUtc));
        ObservedAtUtc = observedAtUtc;
        SourceReference = sourceReference;
    }

    public NasdaqHistoricalDocumentedExit Exit { get; }
    public NasdaqHistoricalTradeSnapshot Snapshot => Exit.Snapshot;
    public NasdaqDemoSessionIdentity Session => Snapshot.Session;
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;
}

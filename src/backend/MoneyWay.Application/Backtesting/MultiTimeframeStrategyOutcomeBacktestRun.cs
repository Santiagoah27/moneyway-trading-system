using System.Collections.ObjectModel;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Backtesting;

/// <summary>
/// Represents a completed canonical synchronized multi-timeframe strategy replay with one safe outcome per global
/// historical step. Verdict and coverage counts are descriptive only and represent neither trades nor profitability.
/// </summary>
public sealed class MultiTimeframeStrategyOutcomeBacktestRun
{
    public MultiTimeframeStrategyOutcomeBacktestRun(
        MultiTimeframeStrategyBacktestRun strategyRun,
        IEnumerable<StrategyReplayContextOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(strategyRun);
        ArgumentNullException.ThrowIfNull(outcomes);
        var snapshot = outcomes.ToArray();
        if (snapshot.Any(x => x is null)) throw new ArgumentException("Outcomes cannot contain null.", nameof(outcomes));
        if (snapshot.Length != strategyRun.ObservationCount || snapshot.Length != strategyRun.StrategyObservations.Count || snapshot.Length != strategyRun.MarketObservations.Count)
            throw new ArgumentException("Every global replay observation must have exactly one outcome.", nameof(outcomes));
        for (var index = 0; index < snapshot.Length; index++)
        {
            var outcome = snapshot[index]; var strategyObservation = strategyRun.StrategyObservations[index]; var marketObservation = strategyRun.MarketObservations[index];
            if (outcome.Step != marketObservation.Step || outcome.AsOfUtc != marketObservation.AsOfUtc || !ObservationsMatch(outcome.Observation, strategyObservation))
                throw new ArgumentException("Outcomes must align structurally with strategy observations in global-step order.", nameof(outcomes));
        }
        StrategyRun = strategyRun;
        Outcomes = new ReadOnlyCollection<StrategyReplayContextOutcome>(snapshot);
    }

    public MultiTimeframeStrategyBacktestRun StrategyRun { get; }
    public IReadOnlyList<StrategyReplayContextOutcome> Outcomes { get; }
    public StrategyId StrategyId => StrategyRun.StrategyId;
    public StrategyVersion StrategyVersion => StrategyRun.StrategyVersion;
    public MarketDataProviderId ProviderId => StrategyRun.ProviderId;
    public MarketSymbol Symbol => StrategyRun.Symbol;
    public IReadOnlyList<Timeframe> ConfiguredTimeframes => StrategyRun.ConfiguredTimeframes;
    public int OutcomeCount => Outcomes.Count;
    public DateTimeOffset? FirstAsOfUtc => StrategyRun.FirstAsOfUtc;
    public DateTimeOffset? LastAsOfUtc => StrategyRun.LastAsOfUtc;
    public int ReadyCount => Count(StrategyVerdict.Ready);
    public int WaitCount => Count(StrategyVerdict.Wait);
    public int NoTradeCount => Count(StrategyVerdict.NoTrade);
    public int HumanValidationRequiredCount => Count(StrategyVerdict.HumanValidationRequired);
    public int DataUnavailableCount => Count(StrategyVerdict.DataUnavailable);
    public int CompleteRequiredCoverageCount => Outcomes.Count(x => x.HasCompleteRequiredCoverage);
    public int IncompleteRequiredCoverageCount => Outcomes.Count(x => !x.HasCompleteRequiredCoverage);
    public int CompleteCoverageDataUnavailableCount => Outcomes.Count(x => x.HasCompleteRequiredCoverage && x.Verdict == StrategyVerdict.DataUnavailable);

    private int Count(StrategyVerdict verdict) => Outcomes.Count(x => x.Verdict == verdict);

    private static bool ObservationsMatch(StrategyReplayContextObservation actual, StrategyReplayContextObservation expected)
    {
        if (actual.StrategyId != expected.StrategyId || actual.StrategyVersion != expected.StrategyVersion
            || actual.ProviderId != expected.ProviderId || actual.Symbol != expected.Symbol || actual.Step != expected.Step
            || actual.AsOfUtc != expected.AsOfUtc || actual.Evaluations.Count != expected.Evaluations.Count)
            return false;
        return actual.Evaluations.Zip(expected.Evaluations).All(pair =>
            pair.First.RuleId == pair.Second.RuleId && pair.First.DefinitionStatus == pair.Second.DefinitionStatus
            && pair.First.Result == pair.Second.Result && pair.First.Sequence == pair.Second.Sequence
            && pair.First.IsRequired == pair.Second.IsRequired && pair.First.Reason == pair.Second.Reason
            && pair.First.EvaluatedAtUtc == pair.Second.EvaluatedAtUtc && pair.First.EvidenceReference == pair.Second.EvidenceReference);
    }
}

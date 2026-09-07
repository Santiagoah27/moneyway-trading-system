using System.Collections.ObjectModel;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Backtesting;

/// <summary>
/// Represents a completed strategy replay with one safe strategy outcome per historical replay frame.
/// Verdict counts are descriptive replay statistics and do not represent trades or profitability.
/// </summary>
public sealed class StrategyOutcomeBacktestRun
{
    public StrategyOutcomeBacktestRun(
        StrategyBacktestRun strategyRun,
        IEnumerable<StrategyReplayFrameOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(strategyRun);
        ArgumentNullException.ThrowIfNull(outcomes);

        var snapshot = outcomes.ToArray();
        if (snapshot.Any(outcome => outcome is null))
        {
            throw new ArgumentException("Outcomes cannot contain null elements.", nameof(outcomes));
        }

        if (snapshot.Length != strategyRun.StrategyObservations.Count
            || snapshot.Length != strategyRun.MarketReplay.ObservationCount)
        {
            throw new ArgumentException("Every strategy replay observation must have exactly one outcome.", nameof(outcomes));
        }

        for (var index = 0; index < snapshot.Length; index++)
        {
            var outcome = snapshot[index];
            var observation = strategyRun.StrategyObservations[index];
            if (outcome.Step != index + 1 || !ObservationsMatch(outcome.Observation, observation))
            {
                throw new ArgumentException("Outcomes must align with strategy observations in consecutive step order.", nameof(outcomes));
            }
        }

        StrategyRun = strategyRun;
        Outcomes = new ReadOnlyCollection<StrategyReplayFrameOutcome>(snapshot);
    }

    public StrategyBacktestRun StrategyRun { get; }
    public IReadOnlyList<StrategyReplayFrameOutcome> Outcomes { get; }
    public StrategyId StrategyId => StrategyRun.StrategyId;
    public StrategyVersion StrategyVersion => StrategyRun.StrategyVersion;
    public MarketDataProviderId ProviderId => StrategyRun.ProviderId;
    public MarketSymbol Symbol => StrategyRun.Symbol;
    public Timeframe Timeframe => StrategyRun.Timeframe;
    public int OutcomeCount => Outcomes.Count;
    public DateTimeOffset? FirstAsOfUtc => StrategyRun.FirstAsOfUtc;
    public DateTimeOffset? LastAsOfUtc => StrategyRun.LastAsOfUtc;
    public int ReadyCount => Count(StrategyVerdict.Ready);
    public int WaitCount => Count(StrategyVerdict.Wait);
    public int NoTradeCount => Count(StrategyVerdict.NoTrade);
    public int HumanValidationRequiredCount => Count(StrategyVerdict.HumanValidationRequired);
    public int DataUnavailableCount => Count(StrategyVerdict.DataUnavailable);
    public int CompleteRequiredCoverageCount => Outcomes.Count(outcome => outcome.HasCompleteRequiredCoverage);
    public int IncompleteRequiredCoverageCount => Outcomes.Count(outcome => !outcome.HasCompleteRequiredCoverage);
    public int CompleteCoverageDataUnavailableCount => Outcomes.Count(
        outcome => outcome.HasCompleteRequiredCoverage && outcome.Verdict == StrategyVerdict.DataUnavailable);

    private int Count(StrategyVerdict verdict) => Outcomes.Count(outcome => outcome.Verdict == verdict);

    private static bool ObservationsMatch(
        StrategyReplayFrameObservation actual,
        StrategyReplayFrameObservation expected)
    {
        if (actual.StrategyId != expected.StrategyId
            || actual.StrategyVersion != expected.StrategyVersion
            || actual.Step != expected.Step
            || actual.AsOfUtc != expected.AsOfUtc
            || !CandlesMatch(actual.CurrentCandle, expected.CurrentCandle)
            || actual.Evaluations.Count != expected.Evaluations.Count)
        {
            return false;
        }

        return actual.Evaluations.Zip(expected.Evaluations).All(pair =>
            pair.First.RuleId == pair.Second.RuleId
            && pair.First.DefinitionStatus == pair.Second.DefinitionStatus
            && pair.First.Result == pair.Second.Result
            && pair.First.Sequence == pair.Second.Sequence
            && pair.First.IsRequired == pair.Second.IsRequired
            && pair.First.Reason == pair.Second.Reason
            && pair.First.EvaluatedAtUtc == pair.Second.EvaluatedAtUtc
            && pair.First.EvidenceReference == pair.Second.EvidenceReference);
    }

    private static bool CandlesMatch(Candle actual, Candle expected) =>
        actual.ProviderId == expected.ProviderId
        && actual.Symbol == expected.Symbol
        && actual.Timeframe == expected.Timeframe
        && actual.OpenTimeUtc == expected.OpenTimeUtc
        && actual.CloseTimeUtc == expected.CloseTimeUtc
        && actual.Open == expected.Open
        && actual.High == expected.High
        && actual.Low == expected.Low
        && actual.Close == expected.Close
        && actual.Volume == expected.Volume;
}

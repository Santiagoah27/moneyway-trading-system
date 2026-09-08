using MoneyWay.Application.Backtesting;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;
using MoneyWay.Domain.Strategies.Evaluation;

namespace MoneyWay.Application.UnitTests.Backtesting;

public sealed class MultiTimeframeStrategyOutcomeBacktestRunTests
{
    private static readonly StrategyId Strategy = new("synthetic"); private static readonly StrategyVersion Version = new("v1");
    private static readonly MarketDataProviderId Provider = new("fixture"); private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute); private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EmptyRunPreservesDelegatedMetadataAndHasZeroCounts()
    {
        var strategyRun = Run([]); var result = new MultiTimeframeStrategyOutcomeBacktestRun(strategyRun, []);
        Assert.Same(strategyRun, result.StrategyRun); Assert.Equal(Strategy, result.StrategyId); Assert.Equal(Version, result.StrategyVersion); Assert.Equal(Provider, result.ProviderId); Assert.Equal(Symbol, result.Symbol); Assert.Equal([Minute], result.ConfiguredTimeframes);
        Assert.Equal(0, result.OutcomeCount); Assert.Null(result.FirstAsOfUtc); Assert.Null(result.LastAsOfUtc);
        Assert.Equal(0, result.ReadyCount + result.WaitCount + result.NoTradeCount + result.HumanValidationRequiredCount + result.DataUnavailableCount);
        Assert.Equal(0, result.CompleteRequiredCoverageCount + result.IncompleteRequiredCoverageCount + result.CompleteCoverageDataUnavailableCount);
    }

    [Fact]
    public void AllVerdictsAndCoverageClassesProduceConsistentDescriptiveCounts()
    {
        var results = new[] { RuleEvaluationResult.Passed, RuleEvaluationResult.Waiting, RuleEvaluationResult.Failed, RuleEvaluationResult.HumanValidationRequired, RuleEvaluationResult.DataUnavailable };
        var observations = results.Select((result, index) => Observation(index + 1, result)).Append(Observation(6)).ToArray(); var strategyRun = Run(observations);
        var useCase = new EvaluateStrategyReplayContextOutcomeUseCase(); var definition = Definition(); var outcomes = observations.Select(x => useCase.Execute(definition, x)).ToArray();
        var run = new MultiTimeframeStrategyOutcomeBacktestRun(strategyRun, outcomes);
        Assert.Equal(6, run.OutcomeCount); Assert.Equal(1, run.ReadyCount); Assert.Equal(1, run.WaitCount); Assert.Equal(1, run.NoTradeCount); Assert.Equal(1, run.HumanValidationRequiredCount); Assert.Equal(2, run.DataUnavailableCount);
        Assert.Equal(5, run.CompleteRequiredCoverageCount); Assert.Equal(1, run.IncompleteRequiredCoverageCount); Assert.Equal(1, run.CompleteCoverageDataUnavailableCount);
        Assert.Equal(run.OutcomeCount, run.ReadyCount + run.WaitCount + run.NoTradeCount + run.HumanValidationRequiredCount + run.DataUnavailableCount);
        Assert.Equal(run.OutcomeCount, run.CompleteRequiredCoverageCount + run.IncompleteRequiredCoverageCount);
        Assert.Equal(run.DataUnavailableCount, run.IncompleteRequiredCoverageCount + run.CompleteCoverageDataUnavailableCount);
        Assert.Equal(Start.AddMinutes(1), run.FirstAsOfUtc); Assert.Equal(Start.AddMinutes(6), run.LastAsOfUtc);
    }

    [Fact]
    public void NullItemsCountAndOrderMismatchesAreRejected()
    {
        var observation = Observation(1, RuleEvaluationResult.Passed); var strategyRun = Run([observation]); var outcome = Outcome(observation);
        Assert.Throws<ArgumentNullException>(() => new MultiTimeframeStrategyOutcomeBacktestRun(null!, []));
        Assert.Throws<ArgumentNullException>(() => new MultiTimeframeStrategyOutcomeBacktestRun(strategyRun, null!));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeStrategyOutcomeBacktestRun(strategyRun, new StrategyReplayContextOutcome[] { null! }));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeStrategyOutcomeBacktestRun(strategyRun, []));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeStrategyOutcomeBacktestRun(Run([observation, Observation(2, RuleEvaluationResult.Passed)]), [Outcome(Observation(2, RuleEvaluationResult.Passed)), outcome]));
    }

    [Theory]
    [InlineData("step")]
    [InlineData("time")]
    [InlineData("strategy")]
    [InlineData("version")]
    [InlineData("provider")]
    [InlineData("symbol")]
    [InlineData("result")]
    [InlineData("reason")]
    [InlineData("evidence")]
    public void StructuralObservationMismatchesAreRejected(string mismatch)
    {
        var expected = Observation(1, RuleEvaluationResult.Passed); var strategyRun = Run([expected]);
        var actual = mismatch switch
        {
            "step" => Observation(2, RuleEvaluationResult.Passed),
            "time" => Observation(1, RuleEvaluationResult.Passed, asOf: Start.AddMinutes(2)),
            "strategy" => Observation(1, RuleEvaluationResult.Passed, strategy: new("other")),
            "version" => Observation(1, RuleEvaluationResult.Passed, version: new("v2")),
            "provider" => Observation(1, RuleEvaluationResult.Passed, provider: new("other")),
            "symbol" => Observation(1, RuleEvaluationResult.Passed, symbol: new("OTHER")),
            "result" => Observation(1, RuleEvaluationResult.Waiting),
            "reason" => Observation(1, RuleEvaluationResult.Passed, reason: "Different."),
            _ => Observation(1, RuleEvaluationResult.Passed, evidence: "different"),
        };
        Assert.Throws<ArgumentException>(() => new MultiTimeframeStrategyOutcomeBacktestRun(strategyRun, [Outcome(actual)]));
    }

    [Fact]
    public void OutcomesAreDefensivelyCopied()
    {
        var observation = Observation(1, RuleEvaluationResult.Passed); var outcomes = new List<StrategyReplayContextOutcome> { Outcome(observation) };
        var run = new MultiTimeframeStrategyOutcomeBacktestRun(Run([observation]), outcomes); outcomes.Clear(); Assert.Equal(1, run.OutcomeCount); Assert.Single(run.Outcomes);
    }

    private static MultiTimeframeStrategyBacktestRun Run(StrategyReplayContextObservation[] observations) => new(Strategy, Version, Provider, Symbol, [Minute], observations.Select(x => new MultiTimeframeBacktestObservation(x.Step, x.AsOfUtc, [Minute], [Minute])), observations);
    private static StrategyDefinition Definition() => new(Strategy, Version, "Synthetic", "test", [new(new("A"), "A", "stage", 10, true, RuleDefinitionStatus.Confirmed, "description", "source")]);
    private static StrategyReplayContextOutcome Outcome(StrategyReplayContextObservation observation)
    {
        if (observation.Evaluations.Count == 0) return new(observation, false, [new("A")], StrategyVerdict.DataUnavailable, StrategyReplayContextOutcome.IncompleteCoverageReason, null);
        var domain = new SequentialStrategyEvaluator().Evaluate(observation.Evaluations); return new(observation, true, [], domain.Verdict, domain.Reason, domain);
    }
    private static StrategyReplayContextObservation Observation(int step, RuleEvaluationResult? result = null, DateTimeOffset? asOf = null, StrategyId? strategy = null, StrategyVersion? version = null, MarketDataProviderId? provider = null, MarketSymbol? symbol = null, string reason = "Synthetic.", string? evidence = null)
    {
        var at = asOf ?? Start.AddMinutes(step); var evaluations = result is null ? [] : new[] { new RuleEvaluation(new("A"), RuleDefinitionStatus.Confirmed, result.Value, 10, true, reason, at, evidence) };
        return new(strategy ?? Strategy, version ?? Version, provider ?? Provider, symbol ?? Symbol, step, at, evaluations);
    }
}

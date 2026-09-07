using MoneyWay.Application.Backtesting;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;
using MoneyWay.Domain.Strategies.Evaluation;

namespace MoneyWay.Application.UnitTests.Backtesting;

public sealed class StrategyOutcomeBacktestRunTests
{
    private static readonly StrategyId Strategy = new("test-strategy");
    private static readonly StrategyVersion Version = new("v1");
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Frame = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EmptyRunHasEmptyMetadataAndCounts()
    {
        var aggregate = new StrategyOutcomeBacktestRun(Run([]), []);

        Assert.Equal(0, aggregate.OutcomeCount);
        Assert.Equal(0, aggregate.ReadyCount + aggregate.WaitCount + aggregate.NoTradeCount
            + aggregate.HumanValidationRequiredCount + aggregate.DataUnavailableCount);
        Assert.Equal(0, aggregate.CompleteRequiredCoverageCount);
        Assert.Equal(0, aggregate.IncompleteRequiredCoverageCount);
        Assert.Equal(0, aggregate.CompleteCoverageDataUnavailableCount);
        Assert.Null(aggregate.FirstAsOfUtc);
        Assert.Null(aggregate.LastAsOfUtc);
    }

    [Fact]
    public void MetadataIsDerivedAndOutcomeSourceIsDefensivelyCopied()
    {
        var observations = Observations(2);
        var strategyRun = Run(observations);
        var source = observations.Select(observation => Outcome(observation, RuleEvaluationResult.Passed)).ToList();
        var aggregate = new StrategyOutcomeBacktestRun(strategyRun, source);
        source.Clear();

        Assert.Same(strategyRun, aggregate.StrategyRun);
        Assert.Equal(Strategy, aggregate.StrategyId);
        Assert.Equal(Version, aggregate.StrategyVersion);
        Assert.Equal(Provider, aggregate.ProviderId);
        Assert.Equal(Symbol, aggregate.Symbol);
        Assert.Equal(Frame, aggregate.Timeframe);
        Assert.Equal(2, aggregate.OutcomeCount);
        Assert.Equal(strategyRun.FirstAsOfUtc, aggregate.FirstAsOfUtc);
        Assert.Equal(strategyRun.LastAsOfUtc, aggregate.LastAsOfUtc);
    }

    [Fact]
    public void CountsEveryVerdictAndCoverageCategoryExactlyOnce()
    {
        var results = new[]
        {
            RuleEvaluationResult.Passed,
            RuleEvaluationResult.Waiting,
            RuleEvaluationResult.Failed,
            RuleEvaluationResult.HumanValidationRequired,
            RuleEvaluationResult.DataUnavailable,
        };
        var observations = results.Select((result, index) =>
        {
            var candle = CandleAt(Start.AddMinutes(index * 5));
            return new StrategyReplayFrameObservation(Strategy, Version, index + 1, candle.CloseTimeUtc, candle, [Evaluation(candle.CloseTimeUtc, result)]);
        }).ToList();
        var lastCandle = CandleAt(Start.AddMinutes(25));
        observations.Add(new(Strategy, Version, 6, lastCandle.CloseTimeUtc, lastCandle, []));
        var outcomes = new[]
        {
            Outcome(observations[0], results[0]),
            Outcome(observations[1], results[1]),
            Outcome(observations[2], results[2]),
            Outcome(observations[3], results[3]),
            Outcome(observations[4], results[4]),
            IncompleteOutcome(observations[5]),
        };

        var aggregate = new StrategyOutcomeBacktestRun(Run(observations), outcomes);

        Assert.Equal(1, aggregate.ReadyCount);
        Assert.Equal(1, aggregate.WaitCount);
        Assert.Equal(1, aggregate.NoTradeCount);
        Assert.Equal(1, aggregate.HumanValidationRequiredCount);
        Assert.Equal(2, aggregate.DataUnavailableCount);
        Assert.Equal(5, aggregate.CompleteRequiredCoverageCount);
        Assert.Equal(1, aggregate.IncompleteRequiredCoverageCount);
        Assert.Equal(1, aggregate.CompleteCoverageDataUnavailableCount);
        Assert.Equal(aggregate.OutcomeCount, aggregate.ReadyCount + aggregate.WaitCount + aggregate.NoTradeCount
            + aggregate.HumanValidationRequiredCount + aggregate.DataUnavailableCount);
        Assert.Equal(aggregate.OutcomeCount, aggregate.CompleteRequiredCoverageCount + aggregate.IncompleteRequiredCoverageCount);
        Assert.Equal(aggregate.DataUnavailableCount, aggregate.IncompleteRequiredCoverageCount + aggregate.CompleteCoverageDataUnavailableCount);
    }

    [Fact]
    public void ConstructorRejectsNullsNullItemsAndCountMismatch()
    {
        var run = Run([]);
        Assert.Throws<ArgumentNullException>(() => new StrategyOutcomeBacktestRun(null!, []));
        Assert.Throws<ArgumentNullException>(() => new StrategyOutcomeBacktestRun(run, null!));
        Assert.Throws<ArgumentException>(() => new StrategyOutcomeBacktestRun(run, new StrategyReplayFrameOutcome[] { null! }));
        Assert.Throws<ArgumentException>(() => new StrategyOutcomeBacktestRun(Run(Observations(1)), []));
    }

    [Theory]
    [InlineData("step")]
    [InlineData("time")]
    [InlineData("candle")]
    [InlineData("strategy")]
    [InlineData("version")]
    [InlineData("evaluation")]
    public void ConstructorRejectsStructurallyMismatchedObservation(string mismatch)
    {
        var expected = Observations(2);
        var original = expected[0];
        var candle = mismatch == "time" ? CandleAt(Start.AddMinutes(5))
            : mismatch == "candle" ? CandleAt(Start, 101)
            : original.CurrentCandle;
        var evaluation = Evaluation(candle.CloseTimeUtc, mismatch == "evaluation" ? RuleEvaluationResult.Failed : RuleEvaluationResult.Passed);
        var actual = new StrategyReplayFrameObservation(
            mismatch == "strategy" ? new("other") : Strategy,
            mismatch == "version" ? new("v2") : Version,
            mismatch == "step" ? 2 : 1,
            candle.CloseTimeUtc,
            candle,
            [evaluation]);
        var outcomes = new[] { DirectOutcome(actual), Outcome(expected[1], RuleEvaluationResult.Passed) };

        Assert.Throws<ArgumentException>(() => new StrategyOutcomeBacktestRun(Run(expected), outcomes));
    }

    private static List<StrategyReplayFrameObservation> Observations(int count) => Enumerable.Range(0, count)
        .Select(index =>
        {
            var candle = CandleAt(Start.AddMinutes(index * 5));
            return new StrategyReplayFrameObservation(Strategy, Version, index + 1, candle.CloseTimeUtc, candle, [Evaluation(candle.CloseTimeUtc, RuleEvaluationResult.Passed)]);
        }).ToList();

    private static StrategyBacktestRun Run(IReadOnlyList<StrategyReplayFrameObservation> observations)
    {
        var market = new BacktestRun(Provider, Symbol, Frame, observations.Select(item => new BacktestObservation(item.Step, item.AsOfUtc, item.CurrentCandle)));
        return new StrategyBacktestRun(Strategy, Version, market, observations);
    }

    private static StrategyReplayFrameOutcome Outcome(StrategyReplayFrameObservation observation, RuleEvaluationResult result)
    {
        var adjusted = result == observation.Evaluations[0].Result
            ? observation
            : new StrategyReplayFrameObservation(Strategy, Version, observation.Step, observation.AsOfUtc, observation.CurrentCandle, [Evaluation(observation.AsOfUtc, result)]);
        var definition = Definition();
        return new EvaluateStrategyReplayFrameOutcomeUseCase().Execute(definition, adjusted);
    }

    private static StrategyReplayFrameOutcome IncompleteOutcome(StrategyReplayFrameObservation observation) => new(
        new StrategyReplayFrameObservation(Strategy, Version, observation.Step, observation.AsOfUtc, observation.CurrentCandle, []),
        false,
        [new RuleId("R-1")],
        StrategyVerdict.DataUnavailable,
        StrategyReplayFrameOutcome.IncompleteCoverageReason,
        null);

    private static StrategyReplayFrameOutcome DirectOutcome(StrategyReplayFrameObservation observation)
    {
        var evaluated = new SequentialStrategyEvaluator().Evaluate(observation.Evaluations);
        return new StrategyReplayFrameOutcome(observation, true, [], evaluated.Verdict, evaluated.Reason, evaluated);
    }

    private static StrategyDefinition Definition() => new(Strategy, Version, "Test", "test", [new(new("R-1"), "Rule", "stage", 1, true, RuleDefinitionStatus.Confirmed, "d", "s")]);
    private static RuleEvaluation Evaluation(DateTimeOffset at, RuleEvaluationResult result) => new(new("R-1"), RuleDefinitionStatus.Confirmed, result, 1, true, "Synthetic.", at, "fixture");
    private static Candle CandleAt(DateTimeOffset open, decimal close = 100) => new(Provider, Symbol, Frame, open, open.AddMinutes(5), 100, 102, 99, close, null);
}

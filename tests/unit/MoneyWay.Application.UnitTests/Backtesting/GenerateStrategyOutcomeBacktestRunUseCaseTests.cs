using MoneyWay.Application.Backtesting;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Backtesting;

public sealed class GenerateStrategyOutcomeBacktestRunUseCaseTests
{
    private static readonly StrategyId Strategy = new("test-strategy");
    private static readonly StrategyVersion Version = new("v1");
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Frame = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DependenciesAndInputsCannotBeNull()
    {
        var strategyUseCase = StrategyUseCase([]);
        Assert.Throws<ArgumentNullException>(() => new GenerateStrategyOutcomeBacktestRunUseCase(null!, new()));
        Assert.Throws<ArgumentNullException>(() => new GenerateStrategyOutcomeBacktestRunUseCase(strategyUseCase, null!));
        var useCase = new GenerateStrategyOutcomeBacktestRunUseCase(strategyUseCase, new());
        Assert.Throws<ArgumentNullException>(() => useCase.Execute(null!, Series(0)));
        Assert.Throws<ArgumentNullException>(() => useCase.Execute(Definition(), null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public void ProducesOneAlignedOutcomePerInputCandle(int count)
    {
        var series = Series(count);
        var aggregate = UseCase([new Fake("R-1", _ => RuleEvaluationResult.Passed)]).Execute(Definition(), series);

        Assert.Equal(count, aggregate.StrategyRun.MarketReplay.ObservationCount);
        Assert.Equal(count, aggregate.StrategyRun.StrategyObservations.Count);
        Assert.Equal(count, aggregate.OutcomeCount);
        Assert.All(aggregate.Outcomes.Select((outcome, index) => (outcome, index)), pair =>
        {
            var observation = aggregate.StrategyRun.StrategyObservations[pair.index];
            Assert.Equal(pair.index + 1, pair.outcome.Step);
            Assert.Equal(observation.AsOfUtc, pair.outcome.AsOfUtc);
            Assert.Same(observation.CurrentCandle, pair.outcome.CurrentCandle);
        });
        Assert.Equal(count, series.Count);
    }

    [Fact]
    public void ZeroEvaluatorsProducesSafeIncompleteOutcomeForEveryFrame()
    {
        var aggregate = UseCase([]).Execute(Definition(), Series(3));
        Assert.Equal(3, aggregate.DataUnavailableCount);
        Assert.Equal(3, aggregate.IncompleteRequiredCoverageCount);
        Assert.Equal(0, aggregate.CompleteCoverageDataUnavailableCount);
        Assert.Equal(0, aggregate.ReadyCount);
        Assert.All(aggregate.Outcomes, outcome => Assert.Null(outcome.EvaluationOutcome));
    }

    [Fact]
    public void CompleteEvaluatorsPreserveWaitReadyNoTradeAndAreReproducible()
    {
        var evaluators = new ISingleTimeframeReplayRuleEvaluator[]
        {
            new Fake("R-1", _ => RuleEvaluationResult.Passed),
            new Fake("R-2", frame => frame.Step switch { 1 => RuleEvaluationResult.Waiting, 2 => RuleEvaluationResult.Passed, _ => RuleEvaluationResult.Failed }),
        };
        var series = Series(3, gapAfterFirst: true);
        var first = UseCase(evaluators).Execute(Definition(twoRequired: true), series);
        var second = UseCase(evaluators).Execute(Definition(twoRequired: true), series);

        Assert.Equal([StrategyVerdict.Wait, StrategyVerdict.Ready, StrategyVerdict.NoTrade], first.Outcomes.Select(item => item.Verdict));
        Assert.Equal(1, first.WaitCount); Assert.Equal(1, first.ReadyCount); Assert.Equal(1, first.NoTradeCount);
        Assert.Equal(first.Outcomes.Select(item => (item.Step, item.AsOfUtc, item.Verdict)), second.Outcomes.Select(item => (item.Step, item.AsOfUtc, item.Verdict)));
    }

    private static GenerateStrategyOutcomeBacktestRunUseCase UseCase(IEnumerable<ISingleTimeframeReplayRuleEvaluator> evaluators) => new(StrategyUseCase(evaluators), new());
    private static GenerateStrategyBacktestRunUseCase StrategyUseCase(IEnumerable<ISingleTimeframeReplayRuleEvaluator> evaluators) => new(new RunCandleReplayUseCase(), new(evaluators));
    private static StrategyDefinition Definition(bool twoRequired = false) => new(Strategy, Version, "Test", "test", twoRequired
        ? [Rule("R-1", 1, true), Rule("R-2", 2, true)]
        : [Rule("R-1", 1, true)]);
    private static StrategyRuleDefinition Rule(string id, int sequence, bool required) => new(new(id), id, "stage", sequence, required, RuleDefinitionStatus.Confirmed, "d", "s");
    private static CandleSeries Series(int count, bool gapAfterFirst = false)
    {
        var candles = Enumerable.Range(0, count).Select(index =>
        {
            var minute = index * 5 + (gapAfterFirst && index > 0 ? 5 : 0);
            var open = Start.AddMinutes(minute);
            var close = 100 + index;
            return new Candle(Provider, Symbol, Frame, open, open.AddMinutes(5), 100, Math.Max(101, close), 99, close, null);
        });
        return new CandleSeries(Provider, Symbol, Frame, candles);
    }

    private sealed class Fake(string ruleId, Func<ReplayFrame, RuleEvaluationResult> result) : ISingleTimeframeReplayRuleEvaluator
    {
        public StrategyId StrategyId => Strategy;
        public StrategyVersion StrategyVersion => Version;
        public RuleId RuleId { get; } = new(ruleId);
        public ReplayRuleEvaluationDecision Evaluate(ReplayFrame frame) => new(result(frame), "Synthetic.", null);
    }
}

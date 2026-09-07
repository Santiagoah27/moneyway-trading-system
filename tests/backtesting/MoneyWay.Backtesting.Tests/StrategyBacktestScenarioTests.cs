using MoneyWay.Application.Backtesting;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Backtesting.Tests;

public sealed class StrategyBacktestScenarioTests
{
    [Fact]
    public void FiveCandlesProduceAlignedNeutralAndStrategyObservations()
    {
        var provider = new MarketDataProviderId("fixture"); var symbol = new MarketSymbol("DEMO"); var timeframe = new Timeframe(5, TimeframeUnit.Minute); var start = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var candles = Enumerable.Range(0, 5).Select(i => new Candle(provider, symbol, timeframe, start.AddMinutes(i * 5), start.AddMinutes((i + 1) * 5), 100, 101, 99, 100, null)).ToArray();
        var evaluator = new Fake(); var definition = new StrategyDefinition(evaluator.StrategyId, evaluator.StrategyVersion, "Test strategy", "test", [new(evaluator.RuleId, "Rule", "stage", 1, true, RuleDefinitionStatus.Confirmed, "d", "s")]);
        var run = new GenerateStrategyBacktestRunUseCase(new RunCandleReplayUseCase(), new([evaluator])).Execute(definition, new(provider, symbol, timeframe, candles));
        Assert.Equal(5, run.MarketReplay.ObservationCount); Assert.Equal(5, run.StrategyObservations.Count); Assert.Equal([1, 2, 3, 4, 5], run.StrategyObservations.Select(x => x.Step)); Assert.Equal([1, 2, 3, 4, 5], evaluator.Counts);
    }
    private sealed class Fake : ISingleTimeframeReplayRuleEvaluator
    {
        public StrategyId StrategyId { get; } = new("test-strategy"); public StrategyVersion StrategyVersion { get; } = new("v1"); public RuleId RuleId { get; } = new("R-1"); public List<int> Counts { get; } = [];
        public ReplayRuleEvaluationDecision Evaluate(ReplayFrame frame) { Counts.Add(frame.AvailableCandles.Count); return new(RuleEvaluationResult.Passed, "Synthetic observation.", null); }
    }
}

using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Backtesting.Tests;

public sealed class ReplayRuleEvaluationScenarioTests
{
    [Fact]
    public void ThreeFramesProduceThreePartialObservationsWithoutFutureCandles()
    {
        var provider = new MarketDataProviderId("fixture"); var symbol = new MarketSymbol("DEMO"); var timeframe = new Timeframe(5, TimeframeUnit.Minute);
        var start = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var candles = Enumerable.Range(0, 3).Select(i => new Candle(provider, symbol, timeframe, start.AddMinutes(i * 5), start.AddMinutes((i + 1) * 5), 100, 101, 99, 100, null)).ToArray();
        var strategyId = new StrategyId("synthetic"); var version = new StrategyVersion("v1"); var ruleId = new RuleId("R-1");
        var definition = new StrategyDefinition(strategyId, version, "Synthetic", "test", [new(ruleId, "Rule", "stage", 1, true, RuleDefinitionStatus.Confirmed, "d", "s")]);
        var evaluator = new Fake(strategyId, version, ruleId); var useCase = new EvaluateStrategyReplayFrameUseCase([evaluator]); var observations = new List<StrategyReplayFrameObservation>();
        new RunCandleReplayUseCase().Execute(new(provider, symbol, timeframe, candles), frame => observations.Add(useCase.Execute(definition, frame)));
        Assert.Equal([1, 2, 3], observations.Select(x => x.Step));
        Assert.Equal([1, 2, 3], evaluator.VisibleCounts);
        Assert.All(observations, x => Assert.Equal(x.AsOfUtc, Assert.Single(x.Evaluations).EvaluatedAtUtc));
    }
    private sealed class Fake(StrategyId strategyId, StrategyVersion version, RuleId ruleId) : ISingleTimeframeReplayRuleEvaluator
    {
        public StrategyId StrategyId { get; } = strategyId; public StrategyVersion StrategyVersion { get; } = version; public RuleId RuleId { get; } = ruleId;
        public List<int> VisibleCounts { get; } = [];
        public ReplayRuleEvaluationDecision Evaluate(ReplayFrame frame) { VisibleCounts.Add(frame.AvailableCandles.Count); return new(RuleEvaluationResult.Passed, "Synthetic observation.", null); }
    }
}

using MoneyWay.Application.Backtesting;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Backtesting.Tests;

public sealed class StrategyReplayFrameOutcomeScenarioTests
{
    [Fact]
    public void PartialCoverageNeverProducesReadyWhileCompleteCoverageTracksFrameResults()
    {
        var definition = Definition(); var series = Series(); var outcomeUseCase = new EvaluateStrategyReplayFrameOutcomeUseCase();
        var partialRun = Run(definition, series, [new Fake(definition.Rules[0].RuleId, _ => RuleEvaluationResult.Passed)]);
        Assert.All(partialRun.StrategyObservations, observation =>
        {
            var outcome = outcomeUseCase.Execute(definition, observation); Assert.False(outcome.HasCompleteRequiredCoverage); Assert.Equal(StrategyVerdict.DataUnavailable, outcome.Verdict);
        });

        var changing = new Fake(definition.Rules[1].RuleId, frame => frame.Step switch { 1 => RuleEvaluationResult.Waiting, 2 => RuleEvaluationResult.Passed, _ => RuleEvaluationResult.Failed });
        var completeRun = Run(definition, series, [new Fake(definition.Rules[0].RuleId, _ => RuleEvaluationResult.Passed), changing]);
        Assert.Equal([StrategyVerdict.Wait, StrategyVerdict.Ready, StrategyVerdict.NoTrade], completeRun.StrategyObservations.Select(observation => outcomeUseCase.Execute(definition, observation).Verdict));
    }

    private static StrategyBacktestRun Run(StrategyDefinition definition, CandleSeries series, IReplayRuleEvaluator[] evaluators) => new GenerateStrategyBacktestRunUseCase(new RunCandleReplayUseCase(), new(evaluators)).Execute(definition, series);
    private static StrategyDefinition Definition() => new(new("test-strategy"), new("v1"), "Test strategy", "test", [Rule("A", 10), Rule("B", 20)]);
    private static StrategyRuleDefinition Rule(string id, int sequence) => new(new(id), id, "stage", sequence, true, RuleDefinitionStatus.Confirmed, "d", "s");
    private static CandleSeries Series()
    {
        var provider = new MarketDataProviderId("fixture"); var symbol = new MarketSymbol("DEMO"); var timeframe = new Timeframe(5, TimeframeUnit.Minute); var start = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        return new(provider, symbol, timeframe, Enumerable.Range(0, 3).Select(i => new Candle(provider, symbol, timeframe, start.AddMinutes(i * 5), start.AddMinutes((i + 1) * 5), 100, 101, 99, 100, null)));
    }
    private sealed class Fake(RuleId ruleId, Func<ReplayFrame, RuleEvaluationResult> result) : IReplayRuleEvaluator
    {
        public StrategyId StrategyId { get; } = new("test-strategy"); public StrategyVersion StrategyVersion { get; } = new("v1"); public RuleId RuleId { get; } = ruleId;
        public ReplayRuleEvaluationDecision Evaluate(ReplayFrame frame) => new(result(frame), "Synthetic result.", null);
    }
}

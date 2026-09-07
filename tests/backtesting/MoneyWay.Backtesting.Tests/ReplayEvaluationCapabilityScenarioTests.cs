using MoneyWay.Application.Backtesting;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Forex;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Capabilities;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Backtesting.Tests;

public sealed class ReplayEvaluationCapabilityScenarioTests
{
    [Fact]
    public void EvaluatorGapMatchesIncompleteRuntimeCoverage()
    {
        var definition = MoneyWayForexStrategyDefinition.Instance; var evaluator = new Fake(definition, definition.Rules[0], RuleEvaluationResult.Passed);
        var capability = Catalog([evaluator]).Find(definition.StrategyId, definition.Version)!;
        var outcome = Execute(definition, [evaluator]);
        Assert.False(capability.HasFullRequiredEvaluatorRegistration); Assert.False(outcome.HasCompleteRequiredCoverage); Assert.Equal(StrategyVerdict.DataUnavailable, outcome.Verdict);
    }

    [Fact]
    public void FullRegistrationAllowsCoverageButDoesNotGuaranteeReady()
    {
        var definition = MoneyWayForexStrategyDefinition.Instance;
        var evaluators = definition.Rules.Select(rule => (IReplayRuleEvaluator)new Fake(definition, rule,
            rule == definition.Rules[0] ? RuleEvaluationResult.Waiting : RuleEvaluationResult.Passed)).ToArray();
        var capability = Catalog(evaluators).Find(definition.StrategyId, definition.Version)!;
        var outcome = Execute(definition, evaluators);
        Assert.True(capability.HasFullRequiredEvaluatorRegistration); Assert.True(outcome.HasCompleteRequiredCoverage); Assert.Equal(StrategyVerdict.Wait, outcome.Verdict);
    }

    private static StrategyReplayEvaluationCapabilityCatalog Catalog(IEnumerable<IReplayRuleEvaluator> evaluators) => new(new StrategyDefinitionCatalog(), evaluators, []);
    private static StrategyReplayFrameOutcome Execute(StrategyDefinition definition, IEnumerable<IReplayRuleEvaluator> evaluators)
    {
        var provider = new MarketDataProviderId("fixture"); var symbol = new MarketSymbol("DEMO"); var timeframe = new Timeframe(5, TimeframeUnit.Minute); var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var series = new CandleSeries(provider, symbol, timeframe, [new(provider, symbol, timeframe, start, start.AddMinutes(5), 100, 101, 99, 100, null)]);
        var generator = new GenerateStrategyOutcomeBacktestRunUseCase(new(new RunCandleReplayUseCase(), new(evaluators)), new());
        return generator.Execute(definition, series).Outcomes.Single();
    }
    private sealed class Fake(StrategyDefinition definition, StrategyRuleDefinition rule, RuleEvaluationResult result) : IReplayRuleEvaluator
    {
        public StrategyId StrategyId => definition.StrategyId; public StrategyVersion StrategyVersion => definition.Version; public RuleId RuleId => rule.RuleId;
        public ReplayRuleEvaluationDecision Evaluate(ReplayFrame frame) => new(result, "Synthetic.", null);
    }
}

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
        var definition = MoneyWayForexStrategyDefinition.Instance;
        var canonical = new CanonicalFake(definition, definition.Rules[0]);
        var legacy = new LegacyFake(definition, definition.Rules[0], RuleEvaluationResult.Passed);
        var capability = Catalog([canonical]).Find(definition.StrategyId, definition.Version)!;
        var outcome = Execute(definition, [legacy]);
        Assert.False(capability.HasFullRequiredEvaluatorRegistration); Assert.False(outcome.HasCompleteRequiredCoverage); Assert.Equal(StrategyVerdict.DataUnavailable, outcome.Verdict);
    }

    [Fact]
    public void FullRegistrationAllowsCoverageButDoesNotGuaranteeReady()
    {
        var definition = MoneyWayForexStrategyDefinition.Instance;
        var canonical = definition.Rules.Select(rule => (IReplayRuleEvaluator)new CanonicalFake(definition, rule)).ToArray();
        var legacy = definition.Rules.Select(rule => (ISingleTimeframeReplayRuleEvaluator)new LegacyFake(definition, rule,
            rule == definition.Rules[0] ? RuleEvaluationResult.Waiting : RuleEvaluationResult.Passed)).ToArray();
        var capability = Catalog(canonical).Find(definition.StrategyId, definition.Version)!;
        var outcome = Execute(definition, legacy);
        Assert.True(capability.HasFullRequiredEvaluatorRegistration); Assert.True(outcome.HasCompleteRequiredCoverage); Assert.Equal(StrategyVerdict.Wait, outcome.Verdict);
    }

    private static StrategyReplayEvaluationCapabilityCatalog Catalog(IEnumerable<IReplayRuleEvaluator> evaluators) => new(new StrategyDefinitionCatalog(), evaluators, []);
    private static StrategyReplayFrameOutcome Execute(StrategyDefinition definition, IEnumerable<ISingleTimeframeReplayRuleEvaluator> evaluators)
    {
        var provider = new MarketDataProviderId("fixture"); var symbol = new MarketSymbol("DEMO"); var timeframe = new Timeframe(5, TimeframeUnit.Minute); var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var series = new CandleSeries(provider, symbol, timeframe, [new(provider, symbol, timeframe, start, start.AddMinutes(5), 100, 101, 99, 100, null)]);
        var generator = new GenerateStrategyOutcomeBacktestRunUseCase(new(new RunCandleReplayUseCase(), new(evaluators)), new());
        return generator.Execute(definition, series).Outcomes.Single();
    }
    private sealed class CanonicalFake(StrategyDefinition definition, StrategyRuleDefinition rule) : IReplayRuleEvaluator
    {
        public StrategyId StrategyId => definition.StrategyId; public StrategyVersion StrategyVersion => definition.Version; public RuleId RuleId => rule.RuleId;
        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context) => new(RuleEvaluationResult.Passed, "Synthetic.", null);
    }
    private sealed class LegacyFake(StrategyDefinition definition, StrategyRuleDefinition rule, RuleEvaluationResult result) : ISingleTimeframeReplayRuleEvaluator
    {
        public StrategyId StrategyId => definition.StrategyId; public StrategyVersion StrategyVersion => definition.Version; public RuleId RuleId => rule.RuleId;
        public ReplayRuleEvaluationDecision Evaluate(ReplayFrame frame) => new(result, "Synthetic.", null);
    }
}

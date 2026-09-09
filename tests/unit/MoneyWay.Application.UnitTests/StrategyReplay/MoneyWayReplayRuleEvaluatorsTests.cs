using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Forex;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Capabilities;

namespace MoneyWay.Application.UnitTests.StrategyReplay;

public sealed class MoneyWayReplayRuleEvaluatorsTests
{
    [Fact]
    public void RegistryContainsExactlyThreeStableCanonicalEvaluators()
    {
        var first = MoneyWayReplayRuleEvaluators.GetAll();
        var second = MoneyWayReplayRuleEvaluators.GetAll();

        Assert.NotNull(first);
        Assert.Collection(first,
            evaluator => Assert.IsType<MoneyWayNasdaqSessionLiquidityEvaluator>(evaluator),
            evaluator => Assert.IsType<MoneyWayNasdaqTradingWindowStartEvaluator>(evaluator),
            evaluator => Assert.IsType<MoneyWayNasdaqTradingWindowEndEvaluator>(evaluator));
        Assert.DoesNotContain(first, item => item is null);
        Assert.All(first, evaluator => Assert.False(evaluator is ISingleTimeframeReplayRuleEvaluator));
        Assert.Same(first, second);
        Assert.Equal(first.Select(Identity), second.Select(Identity));
        Assert.Equal(first.Count, first.Select(Identity).Distinct().Count());
    }

    [Fact]
    public void SessionLiquidityRegistrationNaturallyChangesOnlySelectedNasdaqCapability()
    {
        var declarations = MoneyWayReplayEvaluationCapabilityDeclarations.GetAll();
        var beforeCatalog = new StrategyReplayEvaluationCapabilityCatalog(
            new StrategyDefinitionCatalog(),
            [new MoneyWayNasdaqTradingWindowStartEvaluator(), new MoneyWayNasdaqTradingWindowEndEvaluator()],
            declarations);
        var afterCatalog = new StrategyReplayEvaluationCapabilityCatalog(
            new StrategyDefinitionCatalog(), MoneyWayReplayRuleEvaluators.GetAll(), declarations);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var before = beforeCatalog.Find(definition.StrategyId, definition.Version)!;
        var after = afterCatalog.Find(definition.StrategyId, definition.Version)!;

        Assert.Equal((32, 13, 2, 0, 26, 4, 2, 11, false), Counts(before));
        Assert.Equal((32, 13, 3, 0, 25, 4, 3, 10, false), Counts(after));

        var beforeByRule = before.Rules.ToDictionary(rule => rule.RuleId);
        var afterByRule = after.Rules.ToDictionary(rule => rule.RuleId);
        var timingRuleIds = new[]
        {
            new MoneyWayNasdaqTradingWindowStartEvaluator().RuleId,
            new MoneyWayNasdaqTradingWindowEndEvaluator().RuleId,
        };
        var selected = new MoneyWayNasdaqSessionLiquidityEvaluator(new()).RuleId;
        Assert.All(timingRuleIds, ruleId =>
        {
            Assert.Equal(ReplayRuleEvaluationCapabilityStatus.Implemented, beforeByRule[ruleId].CapabilityStatus);
            Assert.Equal(ReplayRuleEvaluationCapabilityStatus.Implemented, afterByRule[ruleId].CapabilityStatus);
        });
        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.NotImplemented, beforeByRule[selected].CapabilityStatus);
        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.Implemented, afterByRule[selected].CapabilityStatus);
        Assert.Equal(StrategyReplayEvaluationCapabilityCatalog.ImplementedReason, afterByRule[selected].CapabilityReason);
        Assert.Null(afterByRule[selected].CapabilitySourceReference);
        Assert.All(after.Rules.Where(rule => rule.RuleId != selected), rule =>
            Assert.Equal(beforeByRule[rule.RuleId].CapabilityStatus, rule.CapabilityStatus));

        var forexDefinition = MoneyWayForexStrategyDefinition.Instance;
        var forexBefore = beforeCatalog.Find(forexDefinition.StrategyId, forexDefinition.Version)!;
        var forexAfter = afterCatalog.Find(forexDefinition.StrategyId, forexDefinition.Version)!;
        Assert.Equal((17, 15, 0, 0, 13, 4, 0, 15, false), Counts(forexBefore));
        Assert.Equal(Counts(forexBefore), Counts(forexAfter));
    }

    [Fact]
    public void SessionLiquidityEvaluatorNaturallyProducesImplementedCapability()
    {
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var sessionRule = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-LIQ-001");
        var evaluators = MoneyWayReplayRuleEvaluators.GetAll();
        var declarations = MoneyWayReplayEvaluationCapabilityDeclarations.GetAll();
        var report = new StrategyReplayEvaluationCapabilityCatalog(
            new StrategyDefinitionCatalog(), evaluators, declarations)
            .Find(definition.StrategyId, definition.Version)!;
        var capability = report.Rules.Single(rule => rule.RuleId == sessionRule.RuleId);

        Assert.DoesNotContain(evaluators, evaluator =>
            evaluator is ISingleTimeframeReplayRuleEvaluator);
        Assert.Contains(evaluators, evaluator =>
            evaluator.StrategyId == definition.StrategyId &&
            evaluator.StrategyVersion == definition.Version &&
            evaluator.RuleId == sessionRule.RuleId);
        Assert.DoesNotContain(declarations, declaration =>
            declaration.StrategyId == definition.StrategyId &&
            declaration.StrategyVersion == definition.Version &&
            declaration.RuleId == sessionRule.RuleId);
        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.Implemented, capability.CapabilityStatus);
        Assert.Equal(StrategyReplayEvaluationCapabilityCatalog.ImplementedReason, capability.CapabilityReason);
        Assert.Null(capability.CapabilitySourceReference);
        Assert.Equal((32, 13, 3, 0, 25, 4, 3, 10, false), Counts(report));
    }

    private static (string StrategyId, string Version, string RuleId) Identity(IReplayRuleEvaluator evaluator) =>
        (evaluator.StrategyId.Value, evaluator.StrategyVersion.Value, evaluator.RuleId.Value);

    private static (int, int, int, int, int, int, int, int, bool) Counts(StrategyReplayEvaluationCapabilityReport report) =>
        (report.TotalRuleCount, report.RequiredRuleCount, report.ImplementedCount, report.HumanOnlyCount,
            report.NotImplementedCount, report.BlockedByUnresolvedSpecificationCount,
            report.RequiredImplementedCount, report.RequiredEvaluatorGapCount,
            report.HasFullRequiredEvaluatorRegistration);
}

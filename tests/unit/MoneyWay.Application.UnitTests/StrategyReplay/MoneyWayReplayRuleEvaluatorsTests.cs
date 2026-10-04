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
    public void RegistryContainsExactlyTenStableCanonicalEvaluators()
    {
        var first = MoneyWayReplayRuleEvaluators.GetAll();
        var second = MoneyWayReplayRuleEvaluators.GetAll();

        Assert.NotNull(first);
        Assert.Collection(first,
            evaluator => Assert.IsType<MoneyWayNasdaqSessionPreparationCompletionEvaluator>(evaluator),
            evaluator => Assert.IsType<MoneyWayNasdaqHumanH4ContextEvaluator>(evaluator),
            evaluator => Assert.IsType<MoneyWayNasdaqSessionLiquidityEvaluator>(evaluator),
            evaluator => Assert.IsType<MoneyWayNasdaqHumanStructuralLiquidityEvaluator>(evaluator),
            evaluator => Assert.IsType<MoneyWayNasdaqTradingWindowStartEvaluator>(evaluator),
            evaluator => Assert.IsType<MoneyWayNasdaqTradingWindowEndEvaluator>(evaluator),
            evaluator => Assert.IsType<MoneyWayNasdaqHumanLiquidityTakeEvaluator>(evaluator),
            evaluator => Assert.IsType<MoneyWayNasdaqHumanM5TriggerEvaluator>(evaluator),
            evaluator => Assert.IsType<MoneyWayNasdaqHumanM5FvgEvaluator>(evaluator),
            evaluator => Assert.IsType<MoneyWayNasdaqHumanM5FvgQualityEvaluator>(evaluator));
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
            [new MoneyWayNasdaqSessionPreparationCompletionEvaluator(),
                new MoneyWayNasdaqTradingWindowStartEvaluator(), new MoneyWayNasdaqTradingWindowEndEvaluator()],
            declarations);
        var afterCatalog = new StrategyReplayEvaluationCapabilityCatalog(
            new StrategyDefinitionCatalog(), MoneyWayReplayRuleEvaluators.GetAll().Where(item => item.RuleId.Value is not ("NQ-H4-001" or "NQ-LIQ-002" or "NQ-LIQ-003" or "NQ-M5-001" or "NQ-FVG-001" or "NQ-FVG-002")), declarations);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var before = beforeCatalog.Find(definition.StrategyId, definition.Version)!;
        var after = afterCatalog.Find(definition.StrategyId, definition.Version)!;

        Assert.Equal((32, 14, 3, 0, 26, 3, 3, 11, false), Counts(before));
        Assert.Equal((32, 14, 4, 0, 25, 3, 4, 10, false), Counts(after));

        var beforeByRule = before.Rules.ToDictionary(rule => rule.RuleId);
        var afterByRule = after.Rules.ToDictionary(rule => rule.RuleId);
        var timingRuleIds = new[]
        {
            new MoneyWayNasdaqSessionPreparationCompletionEvaluator().RuleId,
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
        Assert.Equal((32, 14, 10, 0, 19, 3, 10, 4, false), Counts(report));
    }

    [Fact]
    public void HumanM5RegistrationChangesOnlyItsDerivedRuntimeCapability()
    {
        var evaluators = MoneyWayReplayRuleEvaluators.GetAll();
        var declarations = MoneyWayReplayEvaluationCapabilityDeclarations.GetAll();
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var selected = new MoneyWayNasdaqHumanM5TriggerEvaluator().RuleId;
        var beforeCatalog = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), evaluators.Where(e => e.RuleId != selected), declarations);
        var afterCatalog = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), evaluators, declarations);
        var before = beforeCatalog.Find(definition.StrategyId, definition.Version)!;
        var after = afterCatalog.Find(definition.StrategyId, definition.Version)!;
        Assert.Equal((32, 14, 9, 0, 20, 3, 9, 5, false), Counts(before));
        Assert.Equal((32, 14, 10, 0, 19, 3, 10, 4, false), Counts(after));
        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.Implemented, after.Rules.Single(r => r.RuleId == selected).CapabilityStatus);
        var beforeByRule = before.Rules.ToDictionary(r => r.RuleId);
        Assert.All(after.Rules.Where(r => r.RuleId != selected), r => Assert.Equal(beforeByRule[r.RuleId].CapabilityStatus, r.CapabilityStatus));
        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.BlockedByUnresolvedSpecification,
            after.Rules.Single(r => r.RuleId.Value == "NQ-M5-004").CapabilityStatus);
        var forex = MoneyWayForexStrategyDefinition.Instance;
        Assert.Equal(Counts(beforeCatalog.Find(forex.StrategyId, forex.Version)!), Counts(afterCatalog.Find(forex.StrategyId, forex.Version)!));
    }

    private static (string StrategyId, string Version, string RuleId) Identity(IReplayRuleEvaluator evaluator) =>
        (evaluator.StrategyId.Value, evaluator.StrategyVersion.Value, evaluator.RuleId.Value);

    private static (int, int, int, int, int, int, int, int, bool) Counts(StrategyReplayEvaluationCapabilityReport report) =>
        (report.TotalRuleCount, report.RequiredRuleCount, report.ImplementedCount, report.HumanOnlyCount,
            report.NotImplementedCount, report.BlockedByUnresolvedSpecificationCount,
            report.RequiredImplementedCount, report.RequiredEvaluatorGapCount,
            report.HasFullRequiredEvaluatorRegistration);
}

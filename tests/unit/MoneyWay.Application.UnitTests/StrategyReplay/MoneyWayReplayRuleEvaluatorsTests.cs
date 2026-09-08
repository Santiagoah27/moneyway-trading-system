using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Forex;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Capabilities;

namespace MoneyWay.Application.UnitTests.StrategyReplay;

public sealed class MoneyWayReplayRuleEvaluatorsTests
{
    [Fact]
    public void RegistryContainsExactlyOneStableCanonicalEvaluator()
    {
        var first = MoneyWayReplayRuleEvaluators.GetAll();
        var second = MoneyWayReplayRuleEvaluators.GetAll();

        Assert.NotNull(first);
        var evaluator = Assert.Single(first);
        Assert.IsType<MoneyWayNasdaqTradingWindowStartEvaluator>(evaluator);
        Assert.DoesNotContain(first, item => item is null);
        Assert.False(evaluator is ISingleTimeframeReplayRuleEvaluator);
        Assert.Same(first, second);
        Assert.Equal(first.Select(Identity), second.Select(Identity));
        Assert.Equal(first.Count, first.Select(Identity).Distinct().Count());
    }

    [Fact]
    public void RegistrationNaturallyChangesOnlySelectedNasdaqCapability()
    {
        var declarations = MoneyWayReplayEvaluationCapabilityDeclarations.GetAll();
        var beforeCatalog = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), [], declarations);
        var afterCatalog = new StrategyReplayEvaluationCapabilityCatalog(
            new StrategyDefinitionCatalog(), MoneyWayReplayRuleEvaluators.GetAll(), declarations);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var before = beforeCatalog.Find(definition.StrategyId, definition.Version)!;
        var after = afterCatalog.Find(definition.StrategyId, definition.Version)!;

        Assert.Equal((32, 13, 0, 0, 29, 3, 0, 13, false), Counts(before));
        Assert.Equal((32, 13, 1, 0, 28, 3, 1, 12, false), Counts(after));

        var beforeByRule = before.Rules.ToDictionary(rule => rule.RuleId);
        var afterByRule = after.Rules.ToDictionary(rule => rule.RuleId);
        var selected = new MoneyWayNasdaqTradingWindowStartEvaluator().RuleId;
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

    private static (string StrategyId, string Version, string RuleId) Identity(IReplayRuleEvaluator evaluator) =>
        (evaluator.StrategyId.Value, evaluator.StrategyVersion.Value, evaluator.RuleId.Value);

    private static (int, int, int, int, int, int, int, int, bool) Counts(StrategyReplayEvaluationCapabilityReport report) =>
        (report.TotalRuleCount, report.RequiredRuleCount, report.ImplementedCount, report.HumanOnlyCount,
            report.NotImplementedCount, report.BlockedByUnresolvedSpecificationCount,
            report.RequiredImplementedCount, report.RequiredEvaluatorGapCount,
            report.HasFullRequiredEvaluatorRegistration);
}

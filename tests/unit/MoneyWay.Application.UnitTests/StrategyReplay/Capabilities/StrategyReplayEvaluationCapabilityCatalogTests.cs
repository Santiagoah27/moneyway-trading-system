using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Forex;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Capabilities;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.StrategyReplay.Capabilities;

public sealed class StrategyReplayEvaluationCapabilityCatalogTests
{
    [Theory]
    [InlineData(ReplayRuleEvaluationCapabilityStatus.HumanOnly)]
    [InlineData(ReplayRuleEvaluationCapabilityStatus.NotImplemented)]
    [InlineData(ReplayRuleEvaluationCapabilityStatus.BlockedByUnresolvedSpecification)]
    public void DeclarationAcceptsNonImplementedStatuses(ReplayRuleEvaluationCapabilityStatus status)
    {
        var item = new ReplayRuleEvaluationCapabilityDeclaration(Forex.StrategyId, Forex.Version, Forex.Rules[0].RuleId, status, "Audited limitation.", Forex.Rules[0].SourceReference);
        Assert.Equal(status, item.Status); Assert.Equal("Audited limitation.", item.Reason);
    }

    [Theory]
    [InlineData(null, "source")]
    [InlineData("", "source")]
    [InlineData(" ", "source")]
    [InlineData(" reason", "source")]
    [InlineData("reason ", "source")]
    [InlineData("reason", null)]
    [InlineData("reason", "")]
    [InlineData("reason", " ")]
    [InlineData("reason", " source")]
    public void DeclarationRejectsInvalidText(string? reason, string? source) => Assert.ThrowsAny<ArgumentException>(() =>
        new ReplayRuleEvaluationCapabilityDeclaration(Forex.StrategyId, Forex.Version, Forex.Rules[0].RuleId, ReplayRuleEvaluationCapabilityStatus.HumanOnly, reason!, source!));

    [Fact]
    public void DeclarationCannotClaimImplemented() => Assert.Throws<ArgumentException>(() =>
        new ReplayRuleEvaluationCapabilityDeclaration(Forex.StrategyId, Forex.Version, Forex.Rules[0].RuleId, ReplayRuleEvaluationCapabilityStatus.Implemented, "reason", "source"));

    [Fact]
    public void BuiltInsWithoutEvaluatorsUseFallbackIndependentOfDefinitionStatus()
    {
        var catalog = Catalog([], MoneyWayReplayEvaluationCapabilityDeclarations.GetAll());
        foreach (var definition in new[] { Forex, Nasdaq })
        {
            var report = catalog.Find(definition.StrategyId, definition.Version)!;
            Assert.Equal(definition.Rules.Count, report.TotalRuleCount); Assert.Equal(definition.Rules.Select(x => x.Sequence), report.Rules.Select(x => x.Sequence));
            Assert.Equal(0, report.ImplementedCount);
            Assert.Equal(report.TotalRuleCount, report.ImplementedCount + report.HumanOnlyCount + report.NotImplementedCount + report.BlockedByUnresolvedSpecificationCount);
        }
    }

    [Fact]
    public void ExactEvaluatorIsImplementedAndOptionalGapsDoNotAffectRequiredCoverageMetric()
    {
        var required = Forex.Rules.First(x => x.IsRequired); var evaluator = new Fake(Forex.StrategyId, Forex.Version, required.RuleId);
        var report = Catalog([evaluator], []).Find(Forex.StrategyId, Forex.Version)!;
        var item = report.Rules.Single(x => x.RuleId == required.RuleId);
        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.Implemented, item.CapabilityStatus); Assert.Equal(StrategyReplayEvaluationCapabilityCatalog.ImplementedReason, item.CapabilityReason); Assert.Null(item.CapabilitySourceReference);
        Assert.Equal(1, report.RequiredImplementedCount); Assert.Equal(report.RequiredRuleCount - 1, report.RequiredEvaluatorGapCount);
    }

    [Fact]
    public void ExplicitDeclarationOverridesFallbackWithoutMappingDefinitionStatus()
    {
        var rule = Forex.Rules[0]; var declaration = new ReplayRuleEvaluationCapabilityDeclaration(Forex.StrategyId, Forex.Version, rule.RuleId, ReplayRuleEvaluationCapabilityStatus.HumanOnly, "Explicit audited limitation.", rule.SourceReference);
        var item = Catalog([], [declaration]).Find(Forex.StrategyId, Forex.Version)!.Rules.Single(x => x.RuleId == rule.RuleId);
        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.HumanOnly, item.CapabilityStatus); Assert.Equal(rule.SourceReference, item.CapabilitySourceReference);
    }

    [Fact]
    public void ConflictsDuplicatesAndOrphansAreRejected()
    {
        var rule = Forex.Rules[0]; var evaluator = new Fake(Forex.StrategyId, Forex.Version, rule.RuleId);
        var declaration = new ReplayRuleEvaluationCapabilityDeclaration(Forex.StrategyId, Forex.Version, rule.RuleId, ReplayRuleEvaluationCapabilityStatus.NotImplemented, "Explicit.", rule.SourceReference);
        Assert.Throws<InvalidOperationException>(() => Catalog([evaluator], [declaration]));
        Assert.Throws<ArgumentException>(() => Catalog([evaluator, evaluator], []));
        Assert.Throws<ArgumentException>(() => Catalog([], [declaration, declaration]));
        Assert.Throws<InvalidOperationException>(() => Catalog([new Fake(new("unknown"), Forex.Version, rule.RuleId)], []));
        Assert.Throws<InvalidOperationException>(() => Catalog([new Fake(Forex.StrategyId, Forex.Version, new("unknown"))], []));
    }

    [Fact]
    public void BuiltInDeclarationsAreValidAndNeverClaimImplemented()
    {
        var declarations = MoneyWayReplayEvaluationCapabilityDeclarations.GetAll();
        Assert.DoesNotContain(declarations, x => x.Status == ReplayRuleEvaluationCapabilityStatus.Implemented);
        Assert.Equal(declarations.Count, declarations.Select(x => (x.StrategyId, x.StrategyVersion, x.RuleId)).Distinct().Count());
        _ = Catalog([], declarations);
        foreach (var declaration in declarations)
        {
            var capability = Catalog([], declarations).Find(declaration.StrategyId, declaration.StrategyVersion)!.Rules.Single(x => x.RuleId == declaration.RuleId);
            Assert.Equal(declaration.Status, capability.CapabilityStatus); Assert.Equal(declaration.SourceReference, capability.CapabilitySourceReference);
        }
    }

    private static StrategyDefinition Forex => MoneyWayForexStrategyDefinition.Instance;
    private static StrategyDefinition Nasdaq => MoneyWayNasdaqStrategyDefinition.Instance;
    private static StrategyReplayEvaluationCapabilityCatalog Catalog(IEnumerable<IReplayRuleEvaluator> evaluators, IEnumerable<ReplayRuleEvaluationCapabilityDeclaration> declarations) => new(new StrategyDefinitionCatalog(), evaluators, declarations);
    private sealed class Fake(StrategyId strategyId, StrategyVersion version, RuleId ruleId) : IReplayRuleEvaluator
    {
        public StrategyId StrategyId { get; } = strategyId; public StrategyVersion StrategyVersion { get; } = version; public RuleId RuleId { get; } = ruleId;
        public ReplayRuleEvaluationDecision Evaluate(ReplayFrame frame) => new(RuleEvaluationResult.Passed, "Synthetic.", null);
    }
}

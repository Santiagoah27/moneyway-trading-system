using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.StrategyReplay.Workflow;

public sealed class StrategyReplayWorkflowCatalogTests
{
    private static readonly StrategyId Strategy = new("synthetic");
    private static readonly StrategyVersion Version = new("v1");

    [Fact]
    public void WorkflowMetadataPreservesExplicitAndPrerequisiteFreeRulesImmutably()
    {
        var prerequisites = new List<RuleId> { new("A"), new("B") };
        var declaration = new StrategyReplayRulePrerequisite(new("C"), prerequisites);
        var declarations = new List<StrategyReplayRulePrerequisite>
        {
            new(new("A"), []),
            declaration,
        };
        var workflow = new StrategyReplayWorkflowDefinition(Strategy, Version, declarations);
        prerequisites.Add(new("D"));
        declarations.Clear();

        Assert.Equal(Strategy, workflow.StrategyId);
        Assert.Equal(Version, workflow.StrategyVersion);
        Assert.Equal(["A", "C"], workflow.RulePrerequisites.Select(item => item.DownstreamRuleId.Value));
        Assert.Empty(workflow.GetPrerequisiteRuleIds(new("A")));
        Assert.Equal(["A", "B"], workflow.GetPrerequisiteRuleIds(new("C")).Select(ruleId => ruleId.Value));
        Assert.Empty(workflow.GetPrerequisiteRuleIds(new("D")));
    }

    [Fact]
    public void CatalogResolvesExactStrategyVersionAndDoesNotInferMissingWorkflows()
    {
        var definition = Definition("synthetic", "v1", "A", "B", "C");
        var workflow = Workflow(Strategy, Version, new StrategyReplayRulePrerequisite(new("B"), [new("A")]));
        var catalog = new StrategyReplayWorkflowCatalog([definition], [workflow]);

        Assert.Same(workflow, catalog.Find(Strategy, Version));
        Assert.Null(catalog.Find(new("synthetic"), new("v2")));
        Assert.Null(StrategyReplayWorkflowCatalog.Empty.Find(Strategy, Version));
        Assert.Single(catalog.WorkflowDefinitions);
    }

    [Fact]
    public void SequenceAndRequiredFlagsDoNotDefineOrRestrictDependencies()
    {
        var definition = new StrategyDefinition(
            Strategy,
            Version,
            "Synthetic",
            "test",
            [Rule("A", 30, false), Rule("B", 10, true), Rule("C", 20, false)]);
        var workflow = Workflow(Strategy, Version, new StrategyReplayRulePrerequisite(new("B"), [new("A")]));

        var catalog = new StrategyReplayWorkflowCatalog([definition], [workflow]);

        Assert.Equal([new RuleId("A")], catalog.Find(Strategy, Version)!.GetPrerequisiteRuleIds(new("B")));
        Assert.Empty(catalog.Find(Strategy, Version)!.GetPrerequisiteRuleIds(new("C")));
    }

    [Fact]
    public void UnknownStrategyDownstreamAndPrerequisiteAreRejected()
    {
        var definition = Definition("synthetic", "v1", "A", "B");
        var unknownStrategy = Workflow(new("other"), Version, new StrategyReplayRulePrerequisite(new("B"), [new("A")]));
        var unknownVersion = Workflow(Strategy, new("v2"), new StrategyReplayRulePrerequisite(new("B"), [new("A")]));
        var unknownDownstream = Workflow(Strategy, Version, new StrategyReplayRulePrerequisite(new("UNKNOWN"), [new("A")]));
        var unknownPrerequisite = Workflow(Strategy, Version, new StrategyReplayRulePrerequisite(new("B"), [new("UNKNOWN")]));

        Assert.Contains("unknown strategy 'other' version 'v1'", Assert.Throws<InvalidOperationException>(
            () => new StrategyReplayWorkflowCatalog([definition], [unknownStrategy])).Message, StringComparison.Ordinal);
        Assert.Contains("unknown strategy 'synthetic' version 'v2'", Assert.Throws<InvalidOperationException>(
            () => new StrategyReplayWorkflowCatalog([definition], [unknownVersion])).Message, StringComparison.Ordinal);
        Assert.Contains("unknown downstream rule 'UNKNOWN'", Assert.Throws<InvalidOperationException>(
            () => new StrategyReplayWorkflowCatalog([definition], [unknownDownstream])).Message, StringComparison.Ordinal);
        Assert.Contains("unknown prerequisite rule 'UNKNOWN'", Assert.Throws<InvalidOperationException>(
            () => new StrategyReplayWorkflowCatalog([definition], [unknownPrerequisite])).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateWorkflowDeclarationAndPrerequisiteAreRejected()
    {
        Assert.Contains("duplicate declarations for rule 'B'", Assert.Throws<ArgumentException>(() => Workflow(
            Strategy,
            Version,
            new StrategyReplayRulePrerequisite(new("B"), [new("A")]),
            new StrategyReplayRulePrerequisite(new("B"), [new("C")]))).Message, StringComparison.Ordinal);
        var duplicatePrerequisite = Assert.Throws<ArgumentException>(() => Workflow(
            Strategy,
            Version,
            new StrategyReplayRulePrerequisite(new("B"), [new("A"), new("A")])));
        Assert.Contains("Workflow 'synthetic' version 'v1' rule 'B'", duplicatePrerequisite.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate prerequisite", duplicatePrerequisite.Message, StringComparison.Ordinal);

        var definition = Definition("synthetic", "v1", "A", "B");
        var workflow = Workflow(Strategy, Version, new StrategyReplayRulePrerequisite(new("B"), [new("A")]));
        Assert.Contains("Duplicate workflow definition", Assert.Throws<ArgumentException>(() =>
            new StrategyReplayWorkflowCatalog([definition], [workflow, workflow])).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SelfDependencyIsRejected()
    {
        var exception = Assert.Throws<ArgumentException>(() => Workflow(
            Strategy,
            Version,
            new StrategyReplayRulePrerequisite(new("A"), [new("A")])));

        Assert.Contains("Workflow 'synthetic' version 'v1' rule 'A' cannot depend on itself", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectDependencyCycleIsRejected()
    {
        var exception = Assert.Throws<ArgumentException>(() => Workflow(
            Strategy,
            Version,
            new StrategyReplayRulePrerequisite(new("A"), [new("B")]),
            new StrategyReplayRulePrerequisite(new("B"), [new("A")])));

        Assert.Contains("Workflow 'synthetic' version 'v1'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("dependency cycle", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IndirectDependencyCycleIsRejected()
    {
        var exception = Assert.Throws<ArgumentException>(() => Workflow(
            Strategy,
            Version,
            new StrategyReplayRulePrerequisite(new("A"), [new("B")]),
            new StrategyReplayRulePrerequisite(new("B"), [new("C")]),
            new StrategyReplayRulePrerequisite(new("C"), [new("A")])));

        Assert.Contains("dependency cycle", exception.Message, StringComparison.Ordinal);
    }

    private static StrategyReplayWorkflowDefinition Workflow(
        StrategyId strategyId,
        StrategyVersion version,
        params StrategyReplayRulePrerequisite[] declarations) => new(strategyId, version, declarations);

    private static StrategyDefinition Definition(string strategyId, string version, params string[] ruleIds) => new(
        new(strategyId),
        new(version),
        "Synthetic",
        "test",
        ruleIds.Select((ruleId, index) => Rule(ruleId, (index + 1) * 10, true)));

    private static StrategyRuleDefinition Rule(string ruleId, int sequence, bool isRequired) => new(
        new(ruleId), ruleId, "stage", sequence, isRequired, RuleDefinitionStatus.Confirmed, "description", "source");
}

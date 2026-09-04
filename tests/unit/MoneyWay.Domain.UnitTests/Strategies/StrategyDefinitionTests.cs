using MoneyWay.Domain.Strategies;

namespace MoneyWay.Domain.UnitTests.Strategies;

public sealed class StrategyDefinitionTests
{
    [Fact]
    public void ValidValuesArePreservedAndRulesAreSorted()
    {
        var id = new StrategyId("strategy");
        var version = new StrategyVersion("version");
        var later = Rule("RULE-002", 20);
        var earlier = Rule("RULE-001", 10);

        var definition = new StrategyDefinition(id, version, "Strategy", "docs/strategy.md", [later, earlier]);

        Assert.Same(id, definition.StrategyId);
        Assert.Same(version, definition.Version);
        Assert.Equal("Strategy", definition.DisplayName);
        Assert.Equal("docs/strategy.md", definition.SpecificationReference);
        Assert.Equal([earlier, later], definition.Rules);
    }

    [Fact]
    public void RulesAreDefensivelyCopiedAndReadOnly()
    {
        var first = Rule("RULE-001", 10);
        var source = new List<StrategyRuleDefinition> { first };
        var definition = Create(source);

        source.Clear();
        var exposed = Assert.IsAssignableFrom<ICollection<StrategyRuleDefinition>>(definition.Rules);

        Assert.Equal([first], definition.Rules);
        Assert.True(exposed.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => exposed.Add(Rule("RULE-002", 20)));
    }

    [Fact]
    public void NullStrategyIdIsRejected() => Assert.Throws<ArgumentNullException>(() =>
        new StrategyDefinition(null!, new StrategyVersion("version"), "Name", "docs/spec.md", [Rule()]));

    [Fact]
    public void NullVersionIsRejected() => Assert.Throws<ArgumentNullException>(() =>
        new StrategyDefinition(new StrategyId("id"), null!, "Name", "docs/spec.md", [Rule()]));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" Name")]
    [InlineData("Name ")]
    public void InvalidDisplayNameIsRejected(string? value) => Assert.ThrowsAny<ArgumentException>(() =>
        new StrategyDefinition(new StrategyId("id"), new StrategyVersion("version"), value!, "docs/spec.md", [Rule()]));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" docs/spec.md")]
    [InlineData("docs/spec.md ")]
    public void InvalidSpecificationReferenceIsRejected(string? value) => Assert.ThrowsAny<ArgumentException>(() =>
        new StrategyDefinition(new StrategyId("id"), new StrategyVersion("version"), "Name", value!, [Rule()]));

    [Fact]
    public void NullRulesAreRejected() => Assert.Throws<ArgumentNullException>(() => Create(null!));

    [Fact]
    public void EmptyRulesAreRejected() => Assert.Throws<ArgumentException>(() => Create([]));

    [Fact]
    public void NullRuleIsRejected() => Assert.Throws<ArgumentException>(() => Create([null!]));

    [Fact]
    public void DuplicateRuleIdIsRejected() => Assert.Throws<ArgumentException>(() =>
        Create([Rule("RULE-001", 10), Rule("RULE-001", 20)]));

    [Fact]
    public void DuplicateSequenceIsRejected() => Assert.Throws<ArgumentException>(() =>
        Create([Rule("RULE-001", 10), Rule("RULE-002", 10)]));

    [Fact]
    public void RejectedAiInferenceIsRejected() => Assert.Throws<ArgumentException>(() =>
        Create([Rule(status: RuleDefinitionStatus.RejectedAiInference)]));

    [Theory]
    [InlineData(RuleDefinitionStatus.HumanValidationRequired)]
    [InlineData(RuleDefinitionStatus.Unresolved)]
    [InlineData(RuleDefinitionStatus.Candidate)]
    [InlineData(RuleDefinitionStatus.ContextSpecific)]
    public void DraftStatusesAreAccepted(RuleDefinitionStatus status) =>
        Assert.Equal(status, Create([Rule(status: status)]).Rules.Single().DefinitionStatus);

    [Fact]
    public void OptionalRuleIsAccepted() => Assert.False(Create([Rule(isRequired: false)]).Rules.Single().IsRequired);

    private static StrategyDefinition Create(IEnumerable<StrategyRuleDefinition> rules) =>
        new(new StrategyId("strategy"), new StrategyVersion("version"), "Strategy", "docs/spec.md", rules);

    private static StrategyRuleDefinition Rule(
        string id = "RULE-001",
        int sequence = 10,
        bool isRequired = true,
        RuleDefinitionStatus status = RuleDefinitionStatus.Confirmed) =>
        new(new RuleId(id), "Rule", "Stage", sequence, isRequired, status, "Description.", "docs/rules.md");
}

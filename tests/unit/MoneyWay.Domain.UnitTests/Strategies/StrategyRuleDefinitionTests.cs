using MoneyWay.Domain.Strategies;

namespace MoneyWay.Domain.UnitTests.Strategies;

public sealed class StrategyRuleDefinitionTests
{
    [Fact]
    public void ValidValuesArePreserved()
    {
        var ruleId = new RuleId("FX-W-001");

        var definition = new StrategyRuleDefinition(
            ruleId, "Weekly first", "Weekly", 10, false, RuleDefinitionStatus.Candidate,
            "Review weekly first.", "docs/rules.md");

        Assert.Same(ruleId, definition.RuleId);
        Assert.Equal("Weekly first", definition.Name);
        Assert.Equal("Weekly", definition.Stage);
        Assert.Equal(10, definition.Sequence);
        Assert.False(definition.IsRequired);
        Assert.Equal(RuleDefinitionStatus.Candidate, definition.DefinitionStatus);
        Assert.Equal("Review weekly first.", definition.Description);
        Assert.Equal("docs/rules.md", definition.SourceReference);
    }

    [Fact]
    public void NullRuleIdIsRejected() => Assert.Throws<ArgumentNullException>(() =>
        new StrategyRuleDefinition(null!, "Rule", "Stage", 10, true, RuleDefinitionStatus.Confirmed,
            "Description.", "docs/rules.md"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" Name")]
    [InlineData("Name ")]
    public void InvalidNameIsRejected(string? value) => Assert.ThrowsAny<ArgumentException>(() =>
        Create(name: value!));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" Stage")]
    [InlineData("Stage ")]
    public void InvalidStageIsRejected(string? value) => Assert.ThrowsAny<ArgumentException>(() =>
        Create(stage: value!));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveSequenceIsRejected(int value) => Assert.Throws<ArgumentOutOfRangeException>(() =>
        Create(sequence: value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" Description")]
    [InlineData("Description ")]
    public void InvalidDescriptionIsRejected(string? value) => Assert.ThrowsAny<ArgumentException>(() =>
        Create(description: value!));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" docs/rules.md")]
    [InlineData("docs/rules.md ")]
    public void InvalidSourceReferenceIsRejected(string? value) => Assert.ThrowsAny<ArgumentException>(() =>
        Create(sourceReference: value!));

    private static StrategyRuleDefinition Create(
        RuleId? ruleId = null,
        string name = "Rule name",
        string stage = "Stage",
        int sequence = 10,
        string description = "Rule description.",
        string sourceReference = "docs/rules.md") =>
        new(ruleId ?? new RuleId("RULE-001"), name, stage, sequence, true,
            RuleDefinitionStatus.Confirmed, description, sourceReference);
}

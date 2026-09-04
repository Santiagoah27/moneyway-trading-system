using MoneyWay.Application.StrategyDefinitions.Forex;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.StrategyDefinitions.Forex;

public sealed class MoneyWayForexStrategyDefinitionTests
{
    private readonly StrategyDefinition definition = MoneyWayForexStrategyDefinition.Instance;

    [Fact]
    public void MetadataMatchesAuditedDraft()
    {
        Assert.Equal("moneyway-forex", definition.StrategyId.Value);
        Assert.Equal("forex-0.1.0-draft", definition.Version.Value);
        Assert.Equal("MoneyWay Forex", definition.DisplayName);
        Assert.Equal("docs/strategies/forex/strategy-specification.md", definition.SpecificationReference);
        Assert.NotEmpty(definition.Rules);
    }

    [Fact]
    public void RulesHaveUniqueIdentifiersAndSequencesAndAreOrdered()
    {
        Assert.Equal(definition.Rules.Count, definition.Rules.Select(rule => rule.RuleId).Distinct().Count());
        Assert.Equal(definition.Rules.Count, definition.Rules.Select(rule => rule.Sequence).Distinct().Count());
        Assert.Equal(definition.Rules.OrderBy(rule => rule.Sequence), definition.Rules);
        Assert.DoesNotContain(definition.Rules, rule => rule.DefinitionStatus == RuleDefinitionStatus.RejectedAiInference);
    }

    [Fact]
    public void DefinitionDoesNotContainNasdaqOrRejectedInferences()
    {
        var text = string.Join(' ', definition.Rules.SelectMany(rule =>
            new[] { rule.RuleId.Value, rule.Name, rule.Description }));

        Assert.DoesNotContain("Nasdaq", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("top third", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bottom third", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("2H > 1H", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1%", text, StringComparison.Ordinal);
    }

    [Fact]
    public void OptionalEmaIsNotRepresentedAsRequired()
    {
        Assert.DoesNotContain(definition.Rules, rule =>
            rule.IsRequired && (rule.RuleId.Value == "FX-EMA-001" || rule.Name.Contains("EMA 50", StringComparison.Ordinal)));
    }

    [Fact]
    public void OrRelationshipsRemainSingleHighLevelRules()
    {
        var patternRules = definition.Rules.Where(rule => rule.RuleId.Value == "FX-H4-001").ToArray();
        var timeframeRules = definition.Rules.Where(rule => rule.RuleId.Value == "FX-ENTRY-001").ToArray();

        Assert.Single(patternRules);
        Assert.True(patternRules[0].IsRequired);
        Assert.Contains("or", patternRules[0].Description, StringComparison.OrdinalIgnoreCase);
        Assert.Single(timeframeRules);
        Assert.True(timeframeRules[0].IsRequired);
        Assert.Contains("or", timeframeRules[0].Description, StringComparison.OrdinalIgnoreCase);
    }
}

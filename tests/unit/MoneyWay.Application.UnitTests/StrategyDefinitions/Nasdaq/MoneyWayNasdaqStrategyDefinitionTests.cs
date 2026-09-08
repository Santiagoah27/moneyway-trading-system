using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.StrategyDefinitions.Nasdaq;

public sealed class MoneyWayNasdaqStrategyDefinitionTests
{
    private readonly StrategyDefinition definition = MoneyWayNasdaqStrategyDefinition.Instance;

    [Fact]
    public void MetadataMatchesAuditedDraft()
    {
        Assert.Equal("moneyway-nasdaq", definition.StrategyId.Value);
        Assert.Equal("nasdaq-0.1.0-draft", definition.Version.Value);
        Assert.Equal("MoneyWay Nasdaq", definition.DisplayName);
        Assert.Equal("docs/strategies/nasdaq/strategy-specification.md", definition.SpecificationReference);
        Assert.NotEmpty(definition.Rules);
    }

    [Fact]
    public void RulesAreUniqueOrderedAndExcludeRejectedInferences()
    {
        Assert.Equal(definition.Rules.Count, definition.Rules.Select(rule => rule.RuleId).Distinct().Count());
        Assert.Equal(definition.Rules.Count, definition.Rules.Select(rule => rule.Sequence).Distinct().Count());
        Assert.Equal(definition.Rules.OrderBy(rule => rule.Sequence), definition.Rules);
        Assert.DoesNotContain(definition.Rules, rule => rule.DefinitionStatus == RuleDefinitionStatus.RejectedAiInference);
        Assert.DoesNotContain(definition.Rules, rule => rule.RuleId.Value.StartsWith("FX-", StringComparison.Ordinal));
    }

    [Fact]
    public void CriticalOpenVariablesAndEvidenceStatusesArePreserved()
    {
        AssertRule("NQ-SL-001", RuleDefinitionStatus.Unresolved, true);
        AssertRule("NQ-TP-001", RuleDefinitionStatus.Unresolved, false);
        AssertRule("NQ-FVG-002", RuleDefinitionStatus.HumanValidationRequired, true);
        AssertRule("NQ-BE-003", RuleDefinitionStatus.Candidate, false);
        AssertRule("NQ-REENTRY-001", RuleDefinitionStatus.Unresolved, false);
        AssertRule("NQ-NEWS-001", RuleDefinitionStatus.ContextSpecific, false);
        AssertRule("NQ-RISK-001", RuleDefinitionStatus.Confirmed, true);
        AssertRule("NQ-RISK-002", RuleDefinitionStatus.Candidate, false);
    }

    [Fact]
    public void TimingMetadataMatchesManualSourceVideoReverification()
    {
        var preparation = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-TIME-003");
        var start = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-TIME-001");
        var end = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-TIME-002");

        Assert.Equal((90, false, RuleDefinitionStatus.Confirmed), (preparation.Sequence, preparation.IsRequired, preparation.DefinitionStatus));
        Assert.Contains("08:00 America/Bogota", preparation.Description, StringComparison.Ordinal);
        Assert.Contains("preparation and analysis", preparation.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not the trading-window start", preparation.Description, StringComparison.OrdinalIgnoreCase);

        Assert.Equal((100, true, RuleDefinitionStatus.Confirmed), (start.Sequence, start.IsRequired, start.DefinitionStatus));
        Assert.Contains("08:30 America/Bogota", start.Description, StringComparison.Ordinal);
        Assert.Contains("trading window starts", start.Description, StringComparison.OrdinalIgnoreCase);

        Assert.Equal((260, true, RuleDefinitionStatus.Confirmed), (end.Sequence, end.IsRequired, end.DefinitionStatus));
        Assert.Contains("11:30 America/Bogota", end.Description, StringComparison.Ordinal);
        Assert.Contains("new entries", end.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not require closing existing positions", end.Description, StringComparison.OrdinalIgnoreCase);

        Assert.All(new[] { preparation, start, end }, rule =>
            Assert.Equal("docs/strategies/nasdaq/rule-catalog.md", rule.SourceReference));
    }

    [Fact]
    public void ReverifiedConceptsRemainExplicitWithoutClaimingDeterministicGeometry()
    {
        var context = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-H4-001");
        Assert.Contains("HH/LL", context.Description, StringComparison.Ordinal);
        Assert.Contains("Breakout, Wickfill, or Fakeout", context.Description, StringComparison.Ordinal);
        Assert.Contains("human validation", context.Description, StringComparison.OrdinalIgnoreCase);

        var structuralLiquidity = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-LIQ-002");
        Assert.Contains("1H and 4H", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("without an invented hierarchy or tolerance", structuralLiquidity.Description, StringComparison.OrdinalIgnoreCase);

        var sweep = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-LIQ-003");
        Assert.Contains("exceed a marked liquidity high or low", sweep.Description, StringComparison.OrdinalIgnoreCase);

        var inversion = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-M5-001");
        Assert.Contains("or IFVG", inversion.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("whichever occurs first", inversion.Description, StringComparison.OrdinalIgnoreCase);

        var stop = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-SL-001");
        Assert.Equal(RuleDefinitionStatus.Unresolved, stop.DefinitionStatus);
        Assert.Contains("structural 5M HL", stop.Description, StringComparison.Ordinal);
        Assert.Contains("structural 5M LH", stop.Description, StringComparison.Ordinal);
        Assert.Contains("remain unresolved", stop.Description, StringComparison.OrdinalIgnoreCase);

        var target = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-TP-001");
        Assert.Equal(RuleDefinitionStatus.Unresolved, target.DefinitionStatus);
        Assert.Contains("important highs", target.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("important lows", target.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("remain unresolved", target.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefinitionDoesNotInventTimezoneOrdersThresholdsOrUniversalPolicies()
    {
        var text = string.Join(' ', definition.Rules.SelectMany(rule =>
            new[] { rule.RuleId.Value, rule.Name, rule.Description }));

        Assert.Contains("America/Bogota", text, StringComparison.Ordinal);
        Assert.DoesNotContain("America/New_York", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EST", text, StringComparison.Ordinal);
        Assert.DoesNotContain("EDT", text, StringComparison.Ordinal);
        Assert.DoesNotContain("DST adjust", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("minimum 3", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Market Order", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Limit Order", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("always at", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fixed risk", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("always close 15", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClassificationAlternativesAreNotThreeRequiredRules()
    {
        var classificationIds = new[] { "NQ-H4-002", "NQ-H4-003", "NQ-H4-004" };

        Assert.DoesNotContain(definition.Rules, rule =>
            classificationIds.Contains(rule.RuleId.Value) && rule.IsRequired);
    }

    [Fact]
    public void InversionAlternativesAreNotRequiredTogether()
    {
        Assert.True(definition.Rules.Single(rule => rule.RuleId.Value == "NQ-M5-001").IsRequired);
        Assert.False(definition.Rules.Single(rule => rule.RuleId.Value == "NQ-M5-002").IsRequired);
        Assert.False(definition.Rules.Single(rule => rule.RuleId.Value == "NQ-M5-003").IsRequired);
        Assert.False(definition.Rules.Single(rule => rule.RuleId.Value == "NQ-M5-004").IsRequired);
    }

    [Fact]
    public void SessionLevelsAreOneRequiredMarkingRuleNotFourSweepRules()
    {
        var sessionRule = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-LIQ-001");
        var requiredSweepRules = definition.Rules.Where(rule =>
            rule.IsRequired && rule.Stage == "Sweep").ToArray();

        Assert.True(sessionRule.IsRequired);
        Assert.Single(requiredSweepRules);
        Assert.Equal("NQ-LIQ-003", requiredSweepRules[0].RuleId.Value);
    }

    private void AssertRule(string id, RuleDefinitionStatus status, bool required)
    {
        var rule = definition.Rules.Single(item => item.RuleId.Value == id);
        Assert.Equal(status, rule.DefinitionStatus);
        Assert.Equal(required, rule.IsRequired);
    }
}

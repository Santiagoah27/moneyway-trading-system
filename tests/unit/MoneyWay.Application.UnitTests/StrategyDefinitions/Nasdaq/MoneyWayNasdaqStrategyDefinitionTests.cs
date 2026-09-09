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
        Assert.Equal(32, definition.Rules.Count);
        Assert.Equal(definition.Rules.Count, definition.Rules.Select(rule => rule.RuleId).Distinct().Count());
        Assert.Equal(definition.Rules.Count, definition.Rules.Select(rule => rule.Sequence).Distinct().Count());
        Assert.Equal(definition.Rules.OrderBy(rule => rule.Sequence), definition.Rules);
        Assert.DoesNotContain(definition.Rules, rule => rule.DefinitionStatus == RuleDefinitionStatus.RejectedAiInference);
        Assert.DoesNotContain(definition.Rules, rule => rule.RuleId.Value.StartsWith("FX-", StringComparison.Ordinal));
    }

    [Fact]
    public void CriticalOpenVariablesAndEvidenceStatusesArePreserved()
    {
        AssertRule("NQ-SL-001", RuleDefinitionStatus.HumanValidationRequired, true);
        AssertRule("NQ-TP-001", RuleDefinitionStatus.HumanValidationRequired, false);
        AssertRule("NQ-TP-002", RuleDefinitionStatus.HumanValidationRequired, false);
        AssertRule("NQ-BE-001", RuleDefinitionStatus.Unresolved, false);
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

        Assert.Equal((5, false, RuleDefinitionStatus.Confirmed), (preparation.Sequence, preparation.IsRequired, preparation.DefinitionStatus));
        Assert.Contains("08:00 America/Bogota", preparation.Description, StringComparison.Ordinal);
        Assert.Contains("preparation and analysis", preparation.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not the trading-window start", preparation.Description, StringComparison.OrdinalIgnoreCase);

        Assert.Equal((65, true, RuleDefinitionStatus.Confirmed), (start.Sequence, start.IsRequired, start.DefinitionStatus));
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
    public void FourHourContextMetadataMatchesAuditedSpecification()
    {
        var context = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-H4-001");

        Assert.Equal("NQ-H4-001", context.RuleId.Value);
        Assert.Equal("4H-first context", context.Name);
        Assert.Equal("4H", context.Stage);
        Assert.Equal(10, context.Sequence);
        Assert.True(context.IsRequired);
        Assert.Equal(RuleDefinitionStatus.Confirmed, context.DefinitionStatus);
        Assert.Equal("docs/strategies/nasdaq/rule-catalog.md", context.SourceReference);
        Assert.Contains("08:00 America/Bogota", context.Description, StringComparison.Ordinal);
        Assert.Contains("closed 4H candle", context.Description, StringComparison.Ordinal);
        Assert.Contains("HH/HL bullish", context.Description, StringComparison.Ordinal);
        Assert.Contains("LL/LH bearish", context.Description, StringComparison.Ordinal);
        Assert.Contains("body close", context.Description, StringComparison.Ordinal);
        Assert.Contains("not a wick alone", context.Description, StringComparison.Ordinal);
        Assert.Contains("HL/LH confirmation is retrospective", context.Description, StringComparison.Ordinal);
        Assert.Contains("Breakout, Wickfill, or Fakeout using OR", context.Description, StringComparison.Ordinal);
        Assert.Contains("visual/contextual", context.Description, StringComparison.Ordinal);
        Assert.Contains("rather than a deterministic raw-candle algorithm", context.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("automated", context.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("implemented", context.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReverifiedConceptsRemainExplicitWithoutClaimingDeterministicGeometry()
    {

        var structuralLiquidity = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-LIQ-002");
        Assert.Contains("1H/4H structural liquidity references", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("Step-2 levels", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("coincidence with Asia or London highs/lows", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("without inferring a structural-point algorithm, tolerance, or priority", structuralLiquidity.Description, StringComparison.Ordinal);

        var sweep = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-LIQ-003");
        Assert.Equal("Liquidity take required", sweep.Name);
        Assert.Contains("08:30 operational start", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("exceed a selected relevant High", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("go below a selected relevant Low", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("before any Step-4 5M setup is valid", sweep.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("points", sweep.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ticks", sweep.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tolerance", sweep.Description, StringComparison.OrdinalIgnoreCase);

        var inversion = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-M5-001");
        Assert.Contains("Only after a valid liquidity take", inversion.Description, StringComparison.Ordinal);
        Assert.Contains("Structural Change or IFVG", inversion.Description, StringComparison.Ordinal);
        Assert.Contains("whichever occurs first", inversion.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("separate Step-5 FVG confirmation", inversion.Description, StringComparison.Ordinal);

        var buyChange = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-M5-002");
        Assert.Contains("After downside liquidity is taken", buyChange.Description, StringComparison.Ordinal);
        Assert.Contains("bullish direction", buyChange.Description, StringComparison.Ordinal);
        Assert.Contains("geometry remain unresolved", buyChange.Description, StringComparison.Ordinal);

        var sellChange = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-M5-003");
        Assert.Contains("After upside liquidity is taken", sellChange.Description, StringComparison.Ordinal);
        Assert.Contains("bearish direction", sellChange.Description, StringComparison.Ordinal);
        Assert.Contains("geometry remain unresolved", sellChange.Description, StringComparison.Ordinal);

        var ifvg = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-M5-004");
        Assert.Contains("After liquidity is taken", ifvg.Description, StringComparison.Ordinal);
        Assert.Contains("alternative to Structural Change", ifvg.Description, StringComparison.Ordinal);
        Assert.Contains("does not automatically satisfy Step 5", ifvg.Description, StringComparison.Ordinal);
        Assert.Contains("geometry remains unresolved", ifvg.Description, StringComparison.Ordinal);

        var continuation = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-FVG-001");
        Assert.Contains("After the Step-4 trigger", continuation.Description, StringComparison.Ordinal);
        Assert.Contains("separate directional 5M FVG confirmation", continuation.Description, StringComparison.Ordinal);
        Assert.Contains("before Step 6", continuation.Description, StringComparison.Ordinal);
        Assert.Contains("exact FVG geometry, quality, and lifecycle remain unresolved", continuation.Description, StringComparison.Ordinal);

        var pullback = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-M1-001");
        Assert.Contains("Only after the Step-5 FVG", pullback.Description, StringComparison.Ordinal);
        Assert.Contains("countertrend 1M pullback", pullback.Description, StringComparison.Ordinal);
        Assert.Contains("toward or into", pullback.Description, StringComparison.Ordinal);
        Assert.Contains("bearish for a buy or bullish for a sell", pullback.Description, StringComparison.Ordinal);

        var realignment = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-M1-002");
        Assert.Contains("relevant 5M FVG", realignment.Description, StringComparison.Ordinal);
        Assert.Contains("1M structural realignment", realignment.Description, StringComparison.Ordinal);
        Assert.Contains("intended direction", realignment.Description, StringComparison.Ordinal);
        Assert.Contains("bullish for a buy or bearish for a sell", realignment.Description, StringComparison.Ordinal);
        Assert.Contains("geometry remains unresolved", realignment.Description, StringComparison.Ordinal);

        var entry = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-M1-003");
        Assert.Contains("mandatory Steps 1 through 6", entry.Description, StringComparison.Ordinal);
        Assert.Contains("chronological order", entry.Description, StringComparison.Ordinal);
        Assert.Contains("runtime gate-status mapping", entry.Description, StringComparison.Ordinal);

        var stop = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-SL-001");
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, stop.DefinitionStatus);
        Assert.Contains("wick of the latest relevant structural 5M HL", stop.Description, StringComparison.Ordinal);
        Assert.Contains("wick of the latest relevant structural 5M LH", stop.Description, StringComparison.Ordinal);
        Assert.Contains("exact offset remain unresolved", stop.Description, StringComparison.OrdinalIgnoreCase);

        var target = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-TP-001");
        Assert.Equal("Important liquidity target", target.Name);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, target.DefinitionStatus);
        Assert.Contains("upside liquidity or highs for buy targets", target.Description, StringComparison.Ordinal);
        Assert.Contains("downside liquidity or lows for sell targets", target.Description, StringComparison.Ordinal);
        Assert.Contains("human-validated or unresolved", target.Description, StringComparison.Ordinal);

        var sessionTargets = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-TP-002");
        Assert.Equal("Asia/London liquidity targets", sessionTargets.Name);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, sessionTargets.DefinitionStatus);
        Assert.Contains("Asia High or London High", sessionTargets.Description, StringComparison.Ordinal);
        Assert.Contains("Asia Low or London Low", sessionTargets.Description, StringComparison.Ordinal);
        Assert.Contains("neither session has automatic priority", sessionTargets.Description, StringComparison.OrdinalIgnoreCase);

        var distinctSwing = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-BE-001");
        Assert.Equal(RuleDefinitionStatus.Unresolved, distinctSwing.DefinitionStatus);
        Assert.Contains("relationship", distinctSwing.Description, StringComparison.Ordinal);
        Assert.Contains("first-important-liquidity", distinctSwing.Description, StringComparison.Ordinal);
        Assert.Contains("remains unresolved", distinctSwing.Description, StringComparison.Ordinal);

        var breakEven = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-BE-002");
        Assert.Equal("First-liquidity touch to BE", breakEven.Name);
        Assert.Contains("first important favorable upside liquidity for a buy", breakEven.Description, StringComparison.Ordinal);
        Assert.Contains("downside liquidity for a sell", breakEven.Description, StringComparison.Ordinal);
        Assert.Contains("moves Stop Loss conceptually to entry", breakEven.Description, StringComparison.Ordinal);
        Assert.Contains("remain unresolved", breakEven.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void RuleIdentitiesRequiredFlagsSourcesAndCanonicalOrderRemainStable()
    {
        var expectedOrder = new[]
        {
            "NQ-TIME-003", "NQ-H4-001", "NQ-H4-002", "NQ-H4-003", "NQ-H4-004",
            "NQ-LIQ-001", "NQ-LIQ-002", "NQ-TIME-001", "NQ-LIQ-003", "NQ-LIQ-004",
            "NQ-M5-001", "NQ-M5-002", "NQ-M5-003", "NQ-M5-004", "NQ-FVG-001", "NQ-FVG-002",
            "NQ-M1-001", "NQ-M1-002", "NQ-M1-003", "NQ-SL-001", "NQ-TP-001", "NQ-TP-002",
            "NQ-BE-001", "NQ-BE-002", "NQ-BE-003", "NQ-TIME-002", "NQ-RISK-001", "NQ-RISK-002",
            "NQ-RISK-003", "NQ-NEWS-001", "NQ-NEWS-002", "NQ-REENTRY-001",
        };
        var expectedRequired = new[]
        {
            "NQ-H4-001", "NQ-LIQ-001", "NQ-LIQ-002", "NQ-TIME-001", "NQ-LIQ-003", "NQ-M5-001",
            "NQ-FVG-001", "NQ-FVG-002", "NQ-M1-001", "NQ-M1-002", "NQ-SL-001", "NQ-TIME-002", "NQ-RISK-001",
        };

        Assert.Equal(expectedOrder, definition.Rules.Select(rule => rule.RuleId.Value));
        Assert.Equal(expectedRequired, definition.Rules.Where(rule => rule.IsRequired).Select(rule => rule.RuleId.Value));
        Assert.All(definition.Rules, rule =>
            Assert.Equal("docs/strategies/nasdaq/rule-catalog.md", rule.SourceReference));
    }

    [Fact]
    public void SequenceReflectsPreparationGatesAndTargetDependentBreakEvenManagement()
    {
        var sequences = definition.Rules.ToDictionary(rule => rule.RuleId.Value, rule => rule.Sequence);

        Assert.True(sequences["NQ-TIME-003"] < sequences["NQ-H4-001"]);
        Assert.True(sequences["NQ-H4-001"] < sequences["NQ-LIQ-001"]);
        Assert.True(sequences["NQ-LIQ-002"] < sequences["NQ-TIME-001"]);
        Assert.True(sequences["NQ-TIME-001"] < sequences["NQ-LIQ-003"]);
        Assert.True(sequences["NQ-LIQ-003"] < sequences["NQ-M5-001"]);
        Assert.True(sequences["NQ-M5-001"] < sequences["NQ-FVG-001"]);
        Assert.True(sequences["NQ-FVG-001"] < sequences["NQ-M1-001"]);
        Assert.True(sequences["NQ-M1-002"] < sequences["NQ-M1-003"]);
        Assert.True(sequences["NQ-M1-003"] < sequences["NQ-SL-001"]);
        Assert.True(sequences["NQ-SL-001"] < sequences["NQ-TP-001"]);
        Assert.True(sequences["NQ-TP-002"] < sequences["NQ-BE-002"]);
        Assert.True(sequences["NQ-BE-002"] < sequences["NQ-TIME-002"]);
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
    public void SessionLiquidityMetadataMatchesAuditedSpecification()
    {
        var sessionRule = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-LIQ-001");
        var requiredSweepRules = definition.Rules.Where(rule =>
            rule.IsRequired && rule.Stage == "Sweep").ToArray();

        Assert.Equal("NQ-LIQ-001", sessionRule.RuleId.Value);
        Assert.Equal("Session liquidity levels", sessionRule.Name);
        Assert.Equal("Liquidity", sessionRule.Stage);
        Assert.Equal(50, sessionRule.Sequence);
        Assert.True(sessionRule.IsRequired);
        Assert.Equal(RuleDefinitionStatus.Confirmed, sessionRule.DefinitionStatus);
        Assert.Equal("docs/strategies/nasdaq/rule-catalog.md", sessionRule.SourceReference);
        Assert.Contains("exact 1H candles", sessionRule.Description, StringComparison.Ordinal);
        Assert.Contains("local America/Bogota OpenTime", sessionRule.Description, StringComparison.Ordinal);
        Assert.Contains("Asia [D-1 17:00, D 02:00)", sessionRule.Description, StringComparison.Ordinal);
        Assert.Contains("London [D 02:00, D 07:00)", sessionRule.Description, StringComparison.Ordinal);
        Assert.Contains("maximum Candle.High", sessionRule.Description, StringComparison.Ordinal);
        Assert.Contains("minimum Candle.Low", sessionRule.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("America/New_York", sessionRule.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("60m", sessionRule.Description, StringComparison.OrdinalIgnoreCase);
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

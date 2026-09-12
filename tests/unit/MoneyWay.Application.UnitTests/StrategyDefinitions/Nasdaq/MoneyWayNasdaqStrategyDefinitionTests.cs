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
        Assert.Contains("before 11:00 America/Bogota", end.Description, StringComparison.Ordinal);
        Assert.Contains("11:00 and later", end.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pre-entry operational period", end.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not require closing existing positions", end.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("post-entry management", end.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("11:30", end.Description, StringComparison.Ordinal);

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
        Assert.Contains("bullish candidate-HL correction window is partially defined", context.Description, StringComparison.Ordinal);
        Assert.Contains("prior HH remains its upper structural boundary", context.Description, StringComparison.Ordinal);
        Assert.Contains("after HH production stops, the first bearish candle starts the correction", context.Description, StringComparison.Ordinal);
        Assert.Contains("remains active through zigzags and intermediate lows", context.Description, StringComparison.Ordinal);
        Assert.Contains("formally closed candle has Close > prior HH", context.Description, StringComparison.Ordinal);
        Assert.Contains("A deeper low replaces a shallower candidate", context.Description, StringComparison.Ordinal);
        Assert.Contains("deepest low forming the base of the impulse", context.Description, StringComparison.Ordinal);
        Assert.Contains("one- or two-candle pauses inside that impulse do not create another HL", context.Description, StringComparison.Ordinal);
        Assert.Contains("structural price is the lowest Open or Close among its candle bodies", context.Description, StringComparison.Ordinal);
        Assert.Contains("not an isolated wick", context.Description, StringComparison.Ordinal);
        Assert.Contains("equal lowest coordinates preserve that price when only price is needed but do not choose a candle identity", context.Description, StringComparison.Ordinal);
        Assert.Contains("becomes a validated HL retrospectively only from that causal closed-body break AsOfUtc", context.Description, StringComparison.Ordinal);
        Assert.Contains("body close above the prior HH", context.Description, StringComparison.Ordinal);
        Assert.Contains("Direct bearish evidence shows the retracement originates from the prior LL area", context.Description, StringComparison.Ordinal);
        Assert.Contains("candidate high remains provisional", context.Description, StringComparison.Ordinal);
        Assert.Contains("formally closed candle has Close < prior LL", context.Description, StringComparison.Ordinal);
        Assert.Contains("Wick-only penetration and an open candle do not confirm it", context.Description, StringComparison.Ordinal);
        Assert.Contains("validates the preceding highest candidate high as LH only from that causal AsOfUtc", context.Description, StringComparison.Ordinal);
        Assert.Contains("a higher candidate high replaces a lower candidate", context.Description, StringComparison.Ordinal);
        Assert.Contains("superseded intermediate highs do not survive", context.Description, StringComparison.Ordinal);
        Assert.Contains("This confirms bearish swing identity, not the structural decimal coordinate", context.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("candidate symmetry hypotheses", context.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("demonstrated complex candidate-LH reconstruction", context.Description, StringComparison.Ordinal);
        Assert.Contains("formally closed candle body beyond the prior structural extreme", context.Description, StringComparison.Ordinal);
        Assert.Contains("Breakout, Wickfill, or Fakeout using OR", context.Description, StringComparison.Ordinal);
        Assert.Contains("doji/gap handling", context.Description, StringComparison.Ordinal);
        Assert.Contains("exact HH and confirming-candle inclusivity", context.Description, StringComparison.Ordinal);
        Assert.Contains("confirming-candle price scanning", context.Description, StringComparison.Ordinal);
        Assert.Contains("turn membership", context.Description, StringComparison.Ordinal);
        Assert.Contains("equal-coordinate body identity", context.Description, StringComparison.Ordinal);
        Assert.Contains("exact structural coordinates", context.Description, StringComparison.Ordinal);
        Assert.Contains("rather than a deterministic raw-candle algorithm", context.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("automated", context.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("implemented", context.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReverifiedConceptsRemainExplicitWithoutClaimingDeterministicGeometry()
    {

        var structuralLiquidity = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-LIQ-002");
        Assert.Contains("At Step 2", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("Asia and London highs/lows", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("structural 1H/4H liquidity references", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("structural fallback is partially defined", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("validated 1H/4H HL or LL", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("Previous Day Low", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("validated 1H/4H LH or HH", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("Previous Day High", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("bullish candidate-HL correction window is partially defined", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("prior HH remains its upper structural boundary", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("first bearish candle starts the correction", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("remains active through zigzags and intermediate lows", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("formally closed candle has Close > prior HH", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("A deeper low replaces a shallower candidate", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("deepest low forming the base of the impulse", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("one- or two-candle pauses inside that impulse do not create another HL", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("structural price is the lowest Open or Close among its candle bodies", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("not an isolated wick", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("equal lowest coordinates preserve that price when only price is needed but do not choose a candle identity", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("It becomes validated only from that causal close AsOfUtc", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("Direct bearish evidence shows the retracement originates from the prior LL area", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("formally closed candle has Close < prior LL", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("Wick-only penetration or an open candle does not confirm it", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("validates the preceding highest candidate high as LH only from that causal AsOfUtc", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("a higher candidate high replaces a lower candidate", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("superseded intermediate highs do not survive", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("This confirms validated LH identity only", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("New HH/LL likewise require that closed body", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("reviewed paths", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("4H is structurally stronger", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("doji/gap handling", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("exact HH and confirming-candle inclusivity", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("confirming-candle price scanning", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("turn membership", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("candle count", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("pivots/lookbacks", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("structural price/coordinate", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("OHLC mitigation semantics", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("equal-coordinate body identity", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("other exact structural coordinates", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("PDH/PDL ranking", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("any universal 1H/4H priority", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("remain unresolved", structuralLiquidity.Description, StringComparison.Ordinal);

        var sweep = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-LIQ-003");
        Assert.Equal("Liquidity take required", sweep.Name);
        Assert.Contains("08:30 operational start", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("before 11:00 America/Bogota", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("first valid event", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("exceeds a selected relevant High", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("goes below a selected relevant Low", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("effective Step 3", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("Low take may orient a possible buy progression", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("High take may orient a possible sell progression", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("Step-1 4H permitted direction", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("does not enable Step 4, trade, revival of the originally imagined scenario, or chase", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("Equality-only contact does not establish Step 3", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("subsequent same-side extension", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("single setup", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("rather than create a parallel setup", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("Target selection occurs outside this rule", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("separate lifecycle cancellation condition", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("neither selects a fallback nor detects target touch or cancellation", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("does not rescue an originally imagined scenario after a valid post-08:30 take", sweep.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("before Step-3 activation remains unresolved", sweep.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("points", sweep.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ticks", sweep.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tolerance", sweep.Description, StringComparison.OrdinalIgnoreCase);

        var inversion = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-M5-001");
        Assert.Contains("active, eligible pre-entry setup before 11:00 America/Bogota", inversion.Description, StringComparison.Ordinal);
        Assert.Contains("valid Step-3 liquidity take aligned with the Step-1 4H permitted direction", inversion.Description, StringComparison.Ordinal);
        Assert.Contains("Structural Change OR IFVG", inversion.Description, StringComparison.Ordinal);
        Assert.Contains("whichever occurs first", inversion.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("separate Step-5 FVG confirmation", inversion.Description, StringComparison.Ordinal);
        Assert.Contains("This rule owns the Step-4 gate only", inversion.Description, StringComparison.Ordinal);
        Assert.Contains("separate lifecycle cancellation condition", inversion.Description, StringComparison.Ordinal);
        Assert.Contains("later 5M signal cannot revive a cancelled setup", inversion.Description, StringComparison.Ordinal);
        Assert.Contains("same-side continuation may update the active structural reference", inversion.Description, StringComparison.Ordinal);
        Assert.Contains("Step 4 remains pending", inversion.Description, StringComparison.Ordinal);
        Assert.Contains("exact structural and IFVG geometry remain unresolved", inversion.Description, StringComparison.Ordinal);

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
        Assert.Contains("valid preceding Steps 1 through 5", entry.Description, StringComparison.Ordinal);
        Assert.Contains("Step-6 1M countertrend pullback and directional realignment", entry.Description, StringComparison.Ordinal);
        Assert.Contains("chronological order", entry.Description, StringComparison.Ordinal);
        Assert.Contains("before 11:00 America/Bogota", entry.Description, StringComparison.Ordinal);
        Assert.Contains("active, non-cancelled and non-expired setup", entry.Description, StringComparison.Ordinal);
        Assert.Contains("timeframe-independent quote touch", entry.Description, StringComparison.Ordinal);
        Assert.Contains("already-selected opposite-side target", entry.Description, StringComparison.Ordinal);
        Assert.Contains("surrounding lifecycle cancels the old setup", entry.Description, StringComparison.Ordinal);
        Assert.Contains("later NQ-M1-003 signal cannot revive it", entry.Description, StringComparison.Ordinal);
        Assert.Contains("does not select the target", entry.Description, StringComparison.Ordinal);
        Assert.Contains("Step 3 must already be a valid post-08:30 liquidity take aligned with the Step-1 4H permitted direction", entry.Description, StringComparison.Ordinal);
        Assert.Contains("does not select the target or interpret the first post-08:30 liquidity take", entry.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("before Step-3 activation", entry.Description, StringComparison.Ordinal);
        Assert.Contains("same-observation OHLC intrabar order remains a market-data resolution limitation", entry.Description, StringComparison.Ordinal);
        Assert.Contains("runtime gate-status mapping", entry.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("order mechanics remain unresolved", entry.Description, StringComparison.Ordinal);

        var stop = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-SL-001");
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, stop.DefinitionStatus);
        Assert.Contains("wick of the latest relevant structural 5M HL", stop.Description, StringComparison.Ordinal);
        Assert.Contains("highest wick/tail of the relevant structural 5M LH", stop.Description, StringComparison.Ordinal);
        Assert.Contains("separate from the structural price coordinate", stop.Description, StringComparison.Ordinal);
        Assert.Contains("exact offset remain unresolved", stop.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("buffer", stop.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("order", stop.Description, StringComparison.OrdinalIgnoreCase);

        var target = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-TP-001");
        Assert.Equal("Important liquidity target", target.Name);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, target.DefinitionStatus);
        Assert.Contains("London High and Asia High", target.Description, StringComparison.Ordinal);
        Assert.Contains("London Low and Asia Low", target.Description, StringComparison.Ordinal);
        Assert.Contains("first relevant level encountered in the expected price direction", target.Description, StringComparison.Ordinal);
        Assert.Contains("equal Asia/London prices merge into one unique target", target.Description, StringComparison.Ordinal);
        Assert.Contains("final Take Profit in the reviewed scenario", target.Description, StringComparison.Ordinal);
        Assert.Contains("without an intermediate Break-Even event", target.Description, StringComparison.Ordinal);
        Assert.Contains("structural fallback is partially defined", target.Description, StringComparison.Ordinal);
        Assert.Contains("support at validated 1H/4H HL or LL", target.Description, StringComparison.Ordinal);
        Assert.Contains("Previous Day Low", target.Description, StringComparison.Ordinal);
        Assert.Contains("resistance at validated 1H/4H LH or HH", target.Description, StringComparison.Ordinal);
        Assert.Contains("Previous Day High", target.Description, StringComparison.Ordinal);
        Assert.Contains("bullish candidate-HL correction window is partially defined", target.Description, StringComparison.Ordinal);
        Assert.Contains("prior HH remains its upper structural boundary", target.Description, StringComparison.Ordinal);
        Assert.Contains("first bearish candle starts the correction", target.Description, StringComparison.Ordinal);
        Assert.Contains("formally closed candle has Close > prior HH", target.Description, StringComparison.Ordinal);
        Assert.Contains("A deeper low replaces a shallower candidate", target.Description, StringComparison.Ordinal);
        Assert.Contains("deepest low forming the base of the impulse", target.Description, StringComparison.Ordinal);
        Assert.Contains("structural price is the lowest Open or Close among its candle bodies", target.Description, StringComparison.Ordinal);
        Assert.Contains("not an isolated wick", target.Description, StringComparison.Ordinal);
        Assert.Contains("equal lowest coordinates preserve that price when only price is needed but do not choose a candle identity", target.Description, StringComparison.Ordinal);
        Assert.Contains("reviewed paths", target.Description, StringComparison.Ordinal);
        Assert.Contains("not a universal hierarchy", target.Description, StringComparison.Ordinal);
        Assert.Contains("validated 1H/4H HL or LL", target.Description, StringComparison.Ordinal);
        Assert.Contains("validated 1H/4H LH or HH", target.Description, StringComparison.Ordinal);
        Assert.Contains("becomes validated only from that causal close AsOfUtc", target.Description, StringComparison.Ordinal);
        Assert.Contains("Direct bearish evidence shows the retracement originates from the prior LL area", target.Description, StringComparison.Ordinal);
        Assert.Contains("formally closed candle has Close < prior LL", target.Description, StringComparison.Ordinal);
        Assert.Contains("validates the preceding highest candidate high as LH only from that causal AsOfUtc", target.Description, StringComparison.Ordinal);
        Assert.Contains("Wick-only penetration or an open candle does not confirm it", target.Description, StringComparison.Ordinal);
        Assert.Contains("a higher candidate high replaces a lower candidate", target.Description, StringComparison.Ordinal);
        Assert.Contains("superseded intermediate highs do not survive", target.Description, StringComparison.Ordinal);
        Assert.Contains("This confirms candidate-LH identity, not a target structural coordinate", target.Description, StringComparison.Ordinal);
        Assert.Contains("highest-wick/tail anchor is a separate risk semantic", target.Description, StringComparison.Ordinal);
        Assert.Contains("doji/gap handling", target.Description, StringComparison.Ordinal);
        Assert.Contains("exact HH and confirming-candle inclusivity", target.Description, StringComparison.Ordinal);
        Assert.Contains("confirming-candle price scanning", target.Description, StringComparison.Ordinal);
        Assert.Contains("turn membership", target.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("Exact structural-point detector", target.Description, StringComparison.Ordinal);
        Assert.Contains("OHLC mitigation test", target.Description, StringComparison.Ordinal);
        Assert.Contains("structural price/coordinate", target.Description, StringComparison.Ordinal);
        Assert.Contains("equal-coordinate body identity", target.Description, StringComparison.Ordinal);
        Assert.Contains("PDH/PDL ranking", target.Description, StringComparison.Ordinal);
        Assert.Contains("ties", target.Description, StringComparison.Ordinal);
        Assert.Contains("does not rescue an originally imagined scenario after a valid post-08:30 liquidity take", target.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("after-08:30-before-Step-3 edge", target.Description, StringComparison.Ordinal);
        Assert.Contains("absolute price level", target.Description, StringComparison.Ordinal);
        Assert.Contains("timeframe-independent quote touch", target.Description, StringComparison.Ordinal);
        Assert.Contains("including wick contact in candle observations", target.Description, StringComparison.Ordinal);
        Assert.Contains("no body close, candle close, or tolerance", target.Description, StringComparison.Ordinal);
        Assert.Contains("universal full-exit behavior remain unresolved", target.Description, StringComparison.Ordinal);
        Assert.Contains("remain unresolved or human-validated", target.Description, StringComparison.Ordinal);

        var sessionTargets = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-TP-002");
        Assert.Equal("Asia/London liquidity targets", sessionTargets.Name);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, sessionTargets.DefinitionStatus);
        Assert.Contains("London High and Asia High", sessionTargets.Description, StringComparison.Ordinal);
        Assert.Contains("London Low and Asia Low", sessionTargets.Description, StringComparison.Ordinal);
        Assert.Contains("first relevant level encountered in the expected price direction", sessionTargets.Description, StringComparison.Ordinal);
        Assert.Contains("Equal Asia/London prices merge into one unique session target", sessionTargets.Description, StringComparison.Ordinal);
        Assert.Contains("without Asia or London tie priority", sessionTargets.Description, StringComparison.Ordinal);
        Assert.Contains("covers session-target selection only", sessionTargets.Description, StringComparison.Ordinal);
        Assert.Contains("does not define deterministic structural fallback selection", sessionTargets.Description, StringComparison.Ordinal);
        Assert.Contains("does not claim that the first distinct target is always final Take Profit", sessionTargets.Description, StringComparison.Ordinal);
        Assert.Contains("quote-level touch of the selected target is timeframe-independent", sessionTargets.Description, StringComparison.Ordinal);
        Assert.Contains("includes wick contact in candle observations", sessionTargets.Description, StringComparison.Ordinal);
        Assert.Contains("no body close, candle close, or tolerance", sessionTargets.Description, StringComparison.Ordinal);

        var distinctSwing = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-BE-001");
        Assert.Equal(RuleDefinitionStatus.Unresolved, distinctSwing.DefinitionStatus);
        Assert.Contains("relationship", distinctSwing.Description, StringComparison.Ordinal);
        Assert.Contains("first-important-liquidity", distinctSwing.Description, StringComparison.Ordinal);
        Assert.Contains("remains unresolved", distinctSwing.Description, StringComparison.Ordinal);

        var breakEven = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-BE-002");
        Assert.Equal("First-liquidity touch to BE", breakEven.Name);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, breakEven.DefinitionStatus);
        Assert.Contains("timeframe-independent quote touch", breakEven.Description, StringComparison.Ordinal);
        Assert.Contains("first favorable target encountered in the expected price direction", breakEven.Description, StringComparison.Ordinal);
        Assert.Contains("London High or Asia High for a buy", breakEven.Description, StringComparison.Ordinal);
        Assert.Contains("London Low or Asia Low for a sell", breakEven.Description, StringComparison.Ordinal);
        Assert.Contains("may move Stop Loss conceptually to entry", breakEven.Description, StringComparison.Ordinal);
        Assert.Contains("equal", breakEven.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("merge into one target treated as final Take Profit in the reviewed scenario", breakEven.Description, StringComparison.Ordinal);
        Assert.Contains("no intermediate Break-Even transition is created at that same price", breakEven.Description, StringComparison.Ordinal);
        Assert.Contains("including wick contact in candle observations", breakEven.Description, StringComparison.Ordinal);
        Assert.Contains("without body close, candle close, or tolerance", breakEven.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("target selection", breakEven.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("trigger geometry", breakEven.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Exact Break-Even cost basis", breakEven.Description, StringComparison.Ordinal);
        Assert.Contains("cost treatment", breakEven.Description, StringComparison.Ordinal);
        Assert.Contains("universal applicability", breakEven.Description, StringComparison.Ordinal);
        Assert.Contains("remain unresolved", breakEven.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void TargetManagementReconciliationPreservesFrozenRuleMetadata()
    {
        AssertFrozenMetadata(
            "NQ-LIQ-002",
            "Relevant liquidity",
            "Liquidity",
            60,
            true,
            RuleDefinitionStatus.HumanValidationRequired);
        AssertFrozenMetadata(
            "NQ-LIQ-003",
            "Liquidity take required",
            "Sweep",
            70,
            true,
            RuleDefinitionStatus.Confirmed);
        AssertFrozenMetadata(
            "NQ-M5-001",
            "Inversion alternatives",
            "5M inversion",
            110,
            true,
            RuleDefinitionStatus.Confirmed);
        AssertFrozenMetadata(
            "NQ-M1-003",
            "Entry mechanics",
            "Entry",
            190,
            false,
            RuleDefinitionStatus.Unresolved);
        AssertFrozenMetadata(
            "NQ-BE-002",
            "First-liquidity touch to BE",
            "Break Even",
            240,
            false,
            RuleDefinitionStatus.HumanValidationRequired);
        AssertFrozenMetadata(
            "NQ-TP-001",
            "Important liquidity target",
            "Take Profit",
            210,
            false,
            RuleDefinitionStatus.HumanValidationRequired);
        AssertFrozenMetadata(
            "NQ-TP-002",
            "Asia/London liquidity targets",
            "Take Profit",
            220,
            false,
            RuleDefinitionStatus.HumanValidationRequired);
    }

    [Fact]
    public void TargetFallbackMetadataDoesNotInventUnresolvedSelectionOrLifecycleSemantics()
    {
        var targetedRuleIds = new[]
        {
            "NQ-LIQ-002", "NQ-LIQ-003", "NQ-M1-003", "NQ-BE-002", "NQ-TP-001", "NQ-TP-002",
        };
        var text = string.Join(' ', definition.Rules
            .Where(rule => targetedRuleIds.Contains(rule.RuleId.Value))
            .Select(rule => rule.Description));

        Assert.DoesNotContain("equal session targets are two targets", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Asia always wins", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("London always wins", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1H always wins", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("4H always wins", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nearest structural point", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("structural fallback algorithm is deterministic", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("touch requires body close", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("after 08:30 but before Step-3 activation uses fallback", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("after 08:30 but before Step-3 activation cancels", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("implemented", text, StringComparison.OrdinalIgnoreCase);
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

    private void AssertFrozenMetadata(
        string ruleId,
        string name,
        string stage,
        int sequence,
        bool isRequired,
        RuleDefinitionStatus definitionStatus)
    {
        var rule = definition.Rules.Single(item => item.RuleId.Value == ruleId);

        Assert.Equal(ruleId, rule.RuleId.Value);
        Assert.Equal(name, rule.Name);
        Assert.Equal(stage, rule.Stage);
        Assert.Equal(sequence, rule.Sequence);
        Assert.Equal(isRequired, rule.IsRequired);
        Assert.Equal(definitionStatus, rule.DefinitionStatus);
        Assert.Equal("docs/strategies/nasdaq/rule-catalog.md", rule.SourceReference);
    }
}

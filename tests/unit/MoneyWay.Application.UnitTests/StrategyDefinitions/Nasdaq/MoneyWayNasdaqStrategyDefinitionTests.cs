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
        Assert.Contains("after HH production stops, the first bearish candle starts the correction", context.Description, StringComparison.Ordinal);
        Assert.Contains("all in-range bearish, bullish, and exact-doji candles remain in one correction turn", context.Description, StringComparison.Ordinal);
        Assert.Contains("formally closed candle has Close > prior HH", context.Description, StringComparison.Ordinal);
        Assert.Contains("a deeper low replaces a shallower candidate", context.Description, StringComparison.Ordinal);
        Assert.Contains("deepest low forming the base of the impulse", context.Description, StringComparison.Ordinal);
        Assert.Contains("one- or two-candle pauses inside that impulse do not create another HL", context.Description, StringComparison.Ordinal);
        Assert.Contains("structural price is the lowest Open or Close among its candle bodies", context.Description, StringComparison.Ordinal);
        Assert.Contains("not an isolated wick", context.Description, StringComparison.Ordinal);
        Assert.Contains("equal lowest coordinates preserve that price when only price is needed but do not choose a candle identity", context.Description, StringComparison.Ordinal);
        Assert.Contains("becomes a validated HL retrospectively only from that causal closed-body break AsOfUtc", context.Description, StringComparison.Ordinal);
        Assert.Contains("body close above the prior HH", context.Description, StringComparison.Ordinal);
        Assert.Contains("prior LL remains the structural reference", context.Description, StringComparison.Ordinal);
        Assert.Contains("bearish impulse stops producing new lows", context.Description, StringComparison.Ordinal);
        Assert.Contains("first bullish candle that begins upward displacement starts the correction", context.Description, StringComparison.Ordinal);
        Assert.Contains("new lower extreme takes precedence", context.Description, StringComparison.Ordinal);
        Assert.Contains("update the correction-origin floor to that Low", context.Description, StringComparison.Ordinal);
        Assert.Contains("only if the same candle closes bullish", context.Description, StringComparison.Ordinal);
        Assert.Contains("not an automatically validated structural LL", context.Description, StringComparison.Ordinal);
        Assert.Contains("formally closed body below the prior structural LL", context.Description, StringComparison.Ordinal);
        Assert.Contains("no synthetic intrabar chronology is inferred", context.Description, StringComparison.Ordinal);
        Assert.Contains("candidate high remains provisional", context.Description, StringComparison.Ordinal);
        Assert.Contains("formally closed candle has Close < prior LL", context.Description, StringComparison.Ordinal);
        Assert.Contains("ends the correction, confirms the new LL", context.Description, StringComparison.Ordinal);
        Assert.Contains("Wick-only penetration and an open candle do not confirm the structural swing", context.Description, StringComparison.Ordinal);
        Assert.Contains("validates the preceding highest candidate high as LH only from that causal AsOfUtc", context.Description, StringComparison.Ordinal);
        Assert.Contains("a higher candidate high replaces a lower candidate", context.Description, StringComparison.Ordinal);
        Assert.Contains("superseded intermediate highs do not survive", context.Description, StringComparison.Ordinal);
        Assert.Contains("For that selected LH turn, the structural coordinate is the highest Open or Close body edge", context.Description, StringComparison.Ordinal);
        Assert.Contains("candle color does not alter it", context.Description, StringComparison.Ordinal);
        Assert.Contains("a higher wick does not redefine it", context.Description, StringComparison.Ordinal);
        Assert.Contains("distinct from the separate sell Stop Loss anchor above the highest wick/tail", context.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("candidate symmetry hypotheses", context.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("demonstrated complex candidate-LH reconstruction", context.Description, StringComparison.Ordinal);
        Assert.Contains("formally closed candle body beyond the prior structural extreme", context.Description, StringComparison.Ordinal);
        Assert.Contains("Breakout, Wickfill, or Fakeout using OR", context.Description, StringComparison.Ordinal);
        Assert.Contains("new higher extreme takes precedence", context.Description, StringComparison.Ordinal);
        Assert.Contains("update the correction-origin ceiling to that High", context.Description, StringComparison.Ordinal);
        Assert.Contains("only if the same candle closes bearish", context.Description, StringComparison.Ordinal);
        Assert.Contains("not an automatically validated structural HH", context.Description, StringComparison.Ordinal);
        Assert.Contains("formally closed body above the prior structural HH", context.Description, StringComparison.Ordinal);
        Assert.Contains("no synthetic intrabar chronology is inferred", context.Description, StringComparison.Ordinal);
        Assert.Contains("floor and ceiling precedence cases are symmetric", context.Description, StringComparison.Ordinal);
        Assert.Contains("An exact doji with Close == Open is directionally neutral and starts neither correction", context.Description, StringComparison.Ordinal);
        Assert.Contains("update the applicable correction-origin ceiling or floor but leave correction not started", context.Description, StringComparison.Ordinal);
        Assert.Contains("neither turns the doji into an opposite-direction start candle nor validates a structural HH/LL", context.Description, StringComparison.Ordinal);
        Assert.Contains("At that doji AsOfUtc, later candles cannot retroactively create a correction start", context.Description, StringComparison.Ordinal);
        Assert.Contains("Near-doji/small-body thresholds remain unresolved; no epsilon, tick/pip/point tolerance, minimum body size, or percentage threshold is defined", context.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("doji/Close == Open, gaps, exact candle inclusivity, turn-segmentation cases", context.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("bullish-side bearish-body/new-high precedence", context.Description, StringComparison.Ordinal);
        Assert.Contains("causal confirming candle is excluded from the correction set and retrospective candidate HL/LH scan", context.Description, StringComparison.Ordinal);
        Assert.Contains("belongs conceptually to the impulsive leg", context.Description, StringComparison.Ordinal);
        Assert.Contains("validates the new HH/LL and preceding candidate only from that causal AsOfUtc", context.Description, StringComparison.Ordinal);
        Assert.Contains("single-expansive-candle case, its Open supplies the structural HL/LH origin coordinate", context.Description, StringComparison.Ordinal);
        Assert.Contains("its Close supplies causal validation", context.Description, StringComparison.Ordinal);
        Assert.Contains("without making it a correction candle or requiring an intrabar path", context.Description, StringComparison.Ordinal);
        Assert.Contains("This does not replace multi-candle body-edge rules", context.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("exact origin-price coordinate, intrabar chronology, and synthetic path remain unresolved", context.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("simultaneous bullish-body/new-low", context.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("confirming-candle scan participation", context.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("terminal/reset candle membership", context.Description, StringComparison.Ordinal);
        Assert.Contains("one price-based StructuralPrice level", context.Description, StringComparison.Ordinal);
        Assert.Contains("Near-equal coordinates remain unresolved", context.Description, StringComparison.Ordinal);
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
        Assert.Contains("first bearish candle starts the correction", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("all in-range bearish, bullish, and exact-doji candles remain in one correction turn", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("formally closed candle has Close > prior HH", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("a deeper low replaces a shallower candidate", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("deepest low forming the base of the impulse", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("one- or two-candle pauses inside that impulse do not create another HL", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("structural price is the lowest Open or Close among its candle bodies", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("not an isolated wick", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("equal lowest coordinates preserve that price when only price is needed but do not choose a candle identity", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("It becomes validated only from that causal close AsOfUtc", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("prior LL remains the structural reference", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("bearish impulse stops producing new lows", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("first bullish candle that begins upward displacement starts the correction", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("new lower extreme takes precedence", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("update the correction-origin floor to that Low", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("only if the same candle closes bullish", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("not an automatically validated structural LL", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("formally closed body below the prior structural LL", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("no synthetic intrabar chronology is inferred", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("formally closed candle has Close < prior LL", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("ends the correction, confirms the new LL", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("Wick-only penetration or an open candle does not confirm the structural swing", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("validates the preceding highest candidate high as LH only from that causal AsOfUtc", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("a higher candidate high replaces a lower candidate", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("superseded intermediate highs do not survive", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("For that selected LH turn, its structural coordinate is the highest Open or Close body edge", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("candle color does not alter it", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("a higher wick does not redefine it", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("distinct from the separate sell Stop Loss anchor above the highest wick/tail", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("New HH/LL likewise require that closed body", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("reviewed paths", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("4H is structurally stronger", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("new higher extreme takes precedence", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("update the correction-origin ceiling to that High", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("only if the same candle closes bearish", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("not an automatically validated structural HH", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("formally closed body above the prior structural HH", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("no synthetic intrabar chronology is inferred", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("floor and ceiling precedence cases are symmetric", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("An exact doji with Close == Open is directionally neutral and starts neither correction", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("update the applicable correction-origin ceiling or floor but leave correction not started", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("neither turns the doji into an opposite-direction start candle nor validates a structural HH/LL", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("At that doji AsOfUtc, later candles cannot retroactively create a correction start", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("Near-doji/small-body thresholds remain unresolved; no epsilon, tick/pip/point tolerance, minimum body size, or percentage threshold is defined", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("doji/Close == Open, gaps, exact candle inclusivity, turn-segmentation cases", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("bullish-side bearish-body/new-high precedence", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("causal confirming candle is excluded from the correction set and retrospective candidate HL/LH scan", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("belongs conceptually to the impulsive leg", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("validates the new HH/LL and preceding candidate only from that causal AsOfUtc", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("single-expansive-candle case, its Open supplies the structural HL/LH origin coordinate", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("its Close supplies causal validation", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("without making it a correction candle or requiring an intrabar path", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("wick does not become the structural fallback target coordinate", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("exact origin-price coordinate, intrabar chronology, and synthetic path remain unresolved", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("simultaneous bullish-body/new-low", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("confirming-candle scan participation", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("terminal/reset candle membership", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("No fixed candle/day/session count, historical time window, scan count, or numeric pivot/fractal lookback defines 4H bootstrap", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("Initial structural anchor selection from arbitrary raw 4H candles", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("termination/boundary of that initial historical scan remain human-reviewed", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("other structural coordinate/boundary edge cases", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("OHLC mitigation semantics", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("one price-based structural level", structuralLiquidity.Description, StringComparison.Ordinal);
        Assert.Contains("Near-equal coordinates remain unresolved", structuralLiquidity.Description, StringComparison.Ordinal);
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
        Assert.Contains("Low/lowest wick extreme of the latest relevant structural 5M HL", stop.Description, StringComparison.Ordinal);
        Assert.Contains("High/highest wick/tail of the relevant structural 5M LH", stop.Description, StringComparison.Ordinal);
        Assert.Contains("executable price and offset remain unresolved", stop.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not define a buffer, tick distance, spread/cost adjustment, or broker order semantics", stop.Description, StringComparison.Ordinal);
        Assert.Contains("separate from structural coordinates, including the single-candle origin Open", stop.Description, StringComparison.Ordinal);

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
        Assert.Contains("first bearish candle starts the correction", target.Description, StringComparison.Ordinal);
        Assert.Contains("formally closed candle has Close > prior HH", target.Description, StringComparison.Ordinal);
        Assert.Contains("a deeper low replaces a shallower candidate", target.Description, StringComparison.Ordinal);
        Assert.Contains("deepest low forming the base of the impulse", target.Description, StringComparison.Ordinal);
        Assert.Contains("structural price is the lowest Open or Close among its candle bodies", target.Description, StringComparison.Ordinal);
        Assert.Contains("not an isolated wick", target.Description, StringComparison.Ordinal);
        Assert.Contains("equal lowest coordinates preserve that price when only price is needed but do not choose a candle identity", target.Description, StringComparison.Ordinal);
        Assert.Contains("reviewed paths", target.Description, StringComparison.Ordinal);
        Assert.Contains("not a universal hierarchy", target.Description, StringComparison.Ordinal);
        Assert.Contains("validated 1H/4H HL or LL", target.Description, StringComparison.Ordinal);
        Assert.Contains("validated 1H/4H LH or HH", target.Description, StringComparison.Ordinal);
        Assert.Contains("becomes validated only from that causal close AsOfUtc", target.Description, StringComparison.Ordinal);
        Assert.Contains("prior LL remains the structural reference", target.Description, StringComparison.Ordinal);
        Assert.Contains("bearish impulse stops producing new lows", target.Description, StringComparison.Ordinal);
        Assert.Contains("first bullish candle that begins upward displacement starts the correction", target.Description, StringComparison.Ordinal);
        Assert.Contains("new lower extreme takes precedence", target.Description, StringComparison.Ordinal);
        Assert.Contains("update the correction-origin floor to that Low", target.Description, StringComparison.Ordinal);
        Assert.Contains("only if the same candle closes bullish", target.Description, StringComparison.Ordinal);
        Assert.Contains("not an automatically validated structural LL", target.Description, StringComparison.Ordinal);
        Assert.Contains("formally closed body below the prior structural LL", target.Description, StringComparison.Ordinal);
        Assert.Contains("no synthetic intrabar chronology is inferred", target.Description, StringComparison.Ordinal);
        Assert.Contains("formally closed candle has Close < prior LL", target.Description, StringComparison.Ordinal);
        Assert.Contains("ends the correction, confirms the new LL", target.Description, StringComparison.Ordinal);
        Assert.Contains("validates the preceding highest candidate high as LH only from that causal AsOfUtc", target.Description, StringComparison.Ordinal);
        Assert.Contains("Wick-only penetration or an open candle does not confirm the structural swing", target.Description, StringComparison.Ordinal);
        Assert.Contains("a higher candidate high replaces a lower candidate", target.Description, StringComparison.Ordinal);
        Assert.Contains("superseded intermediate highs do not survive", target.Description, StringComparison.Ordinal);
        Assert.Contains("structural target/comparison coordinate is the highest Open or Close body edge", target.Description, StringComparison.Ordinal);
        Assert.Contains("candle color does not alter it", target.Description, StringComparison.Ordinal);
        Assert.Contains("a higher wick does not redefine it", target.Description, StringComparison.Ordinal);
        Assert.Contains("distinct from separate buy/sell protection wick anchors", target.Description, StringComparison.Ordinal);
        Assert.Contains("neither substitutes for the structural target coordinate", target.Description, StringComparison.Ordinal);
        Assert.Contains("new higher extreme takes precedence", target.Description, StringComparison.Ordinal);
        Assert.Contains("update the correction-origin ceiling to that High", target.Description, StringComparison.Ordinal);
        Assert.Contains("only if the same candle closes bearish", target.Description, StringComparison.Ordinal);
        Assert.Contains("not an automatically validated structural HH", target.Description, StringComparison.Ordinal);
        Assert.Contains("formally closed body above the prior structural HH", target.Description, StringComparison.Ordinal);
        Assert.Contains("no synthetic intrabar chronology is inferred", target.Description, StringComparison.Ordinal);
        Assert.Contains("floor and ceiling precedence cases are symmetric", target.Description, StringComparison.Ordinal);
        Assert.Contains("An exact doji with Close == Open is directionally neutral and starts neither correction", target.Description, StringComparison.Ordinal);
        Assert.Contains("update the applicable correction-origin ceiling or floor but leave correction not started", target.Description, StringComparison.Ordinal);
        Assert.Contains("neither turns the doji into an opposite-direction start candle nor validates a structural HH/LL", target.Description, StringComparison.Ordinal);
        Assert.Contains("At that doji AsOfUtc, later candles cannot retroactively create a correction start", target.Description, StringComparison.Ordinal);
        Assert.Contains("Near-doji/small-body thresholds remain unresolved; no epsilon, tick/pip/point tolerance, minimum body size, or percentage threshold is defined", target.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("doji/Close == Open, gaps, exact candle inclusivity, turn-segmentation cases", target.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("bullish-side bearish-body/new-high precedence", target.Description, StringComparison.Ordinal);
        Assert.Contains("causal confirming candle is excluded from the correction set and retrospective candidate HL/LH scan", target.Description, StringComparison.Ordinal);
        Assert.Contains("belongs conceptually to the impulsive leg", target.Description, StringComparison.Ordinal);
        Assert.Contains("validates the new HH/LL and preceding candidate only from that causal AsOfUtc", target.Description, StringComparison.Ordinal);
        Assert.Contains("single-expansive-candle case, its Open supplies the structural HL/LH origin coordinate", target.Description, StringComparison.Ordinal);
        Assert.Contains("its Close supplies causal validation", target.Description, StringComparison.Ordinal);
        Assert.Contains("without making it a correction candle or requiring an intrabar path", target.Description, StringComparison.Ordinal);
        Assert.Contains("does not replace multi-candle body-edge rules or use a protection wick as a target coordinate", target.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("exact origin-price coordinate, intrabar chronology, and synthetic path remain unresolved", target.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("simultaneous bullish-body/new-low", target.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("confirming-candle scan participation", target.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("terminal/reset candle membership", target.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("Exact structural-point detector", target.Description, StringComparison.Ordinal);
        Assert.Contains("OHLC mitigation test", target.Description, StringComparison.Ordinal);
        Assert.Contains("other structural coordinate/boundary edge cases", target.Description, StringComparison.Ordinal);
        Assert.Contains("exact equal structural body coordinates within one selected turn form one candidate price level", target.Description, StringComparison.Ordinal);
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
    public void BullishTerminalCandleMetadataReflectsConfirmedMembershipAndPreservesOtherRuleMetadata()
    {
        var context = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-H4-001");
        var liquidity = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-LIQ-002");
        var target = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-TP-001");

        Assert.Equal(RuleDefinitionStatus.Confirmed, context.DefinitionStatus);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, liquidity.DefinitionStatus);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, target.DefinitionStatus);

        Assert.Contains("all in-range bearish, bullish, and exact-doji candles remain in one correction turn", context.Description, StringComparison.Ordinal);
        Assert.Contains("body-color alternation does not fragment it or create parallel HL candidates", context.Description, StringComparison.Ordinal);
        Assert.Contains("High > current correction-origin ceiling first resets the old correction", context.Description, StringComparison.Ordinal);
        Assert.Contains("discards the current candidate HL without validating a structural HH", context.Description, StringComparison.Ordinal);
        Assert.Contains("when Close < Open, it is excluded from the old turn and included as the first member of the new bullish-correction turn", context.Description, StringComparison.Ordinal);
        Assert.Contains("when Close == Open or Close > Open, the reset occurs but the new correction does not start", context.Description, StringComparison.Ordinal);
        Assert.Contains("Close < prior validated HL invalidates the bullish structure", context.Description, StringComparison.Ordinal);
        Assert.Contains("its candle is excluded from the destroyed turn and previous candidate-HL geometry", context.Description, StringComparison.Ordinal);
        Assert.Contains("cannot change the previous StructuralPrice or ProtectionAnchor", context.Description, StringComparison.Ordinal);
        Assert.Contains("supplies new bearish-impulse evidence without automatically validating a structural LL", context.Description, StringComparison.Ordinal);
        Assert.Contains("terminal memberships are fixed at the closed-candle AsOfUtc and future candles cannot reinterpret them or the destroyed candidate geometry", context.Description, StringComparison.Ordinal);
        Assert.Contains("Close == prior validated HL and wick-only Low < prior validated HL with Close >= prior validated HL do not invalidate", context.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("terminal/reset candle membership", context.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("bearish reset/invalidation symmetry", context.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("turn-segmentation cases", context.Description, StringComparison.Ordinal);

        Assert.Contains("all in-range bearish, bullish, and exact-doji candles remain in one correction turn", liquidity.Description, StringComparison.Ordinal);
        Assert.Contains("discards the current candidate HL without validating a structural HH", liquidity.Description, StringComparison.Ordinal);
        Assert.Contains("Close < prior validated HL invalidates the bullish structure", liquidity.Description, StringComparison.Ordinal);
        Assert.Contains("Reset or invalidation membership does not create a structural point by itself", liquidity.Description, StringComparison.Ordinal);
        Assert.Contains("an invalidated candidate HL cannot survive as a validated structural point", liquidity.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("terminal/reset candle membership", liquidity.Description, StringComparison.Ordinal);

        Assert.Contains("An invalidated bullish structure cannot yield a validated HL usable as a structural fallback target candidate", target.Description, StringComparison.Ordinal);
        Assert.Contains("the invalidation candle cannot retrospectively alter the destroyed candidate geometry", target.Description, StringComparison.Ordinal);
        Assert.Contains("when Close < Open, it is excluded from the old turn and included as the first member of the new bullish-correction turn", target.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("terminal/reset candle membership", target.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("bearish reset/invalidation symmetry", target.Description, StringComparison.Ordinal);

        AssertFrozenMetadata("NQ-H4-002", "Break classification", "4H", 20, false, RuleDefinitionStatus.HumanValidationRequired);
        AssertFrozenMetadata("NQ-TP-002", "Asia/London liquidity targets", "Take Profit", 220, false, RuleDefinitionStatus.HumanValidationRequired);
    }

    [Fact]
    public void BullishCollisionMetadataRepresentsInvalidationDominanceWithoutWorkflowInference()
    {
        var context = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-H4-001");
        var liquidity = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-LIQ-002");
        var target = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-TP-001");

        Assert.Equal(RuleDefinitionStatus.Confirmed, context.DefinitionStatus);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, liquidity.DefinitionStatus);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, target.DefinitionStatus);

        Assert.All(new[] { context, liquidity, target }, rule =>
        {
            Assert.Contains("High > current correction-origin ceiling and Close < prior validated HL", rule.Description, StringComparison.Ordinal);
            Assert.Contains("both observations remain true but", rule.Description, StringComparison.Ordinal);
            Assert.Contains("invalidation dominates the structural transition", rule.Description, StringComparison.Ordinal);
            Assert.Contains("excluded from", rule.Description, StringComparison.Ordinal);
            Assert.Contains("candidate geometry", rule.Description, StringComparison.Ordinal);
            Assert.Contains("new bearish impulse context", rule.Description, StringComparison.Ordinal);
            Assert.Contains("relevant upper price extreme and bearish-origin reference", rule.Description, StringComparison.Ordinal);
            Assert.Contains("validated LH", rule.Description, StringComparison.Ordinal);
            Assert.Contains("executable Stop Loss", rule.Description, StringComparison.Ordinal);
            Assert.Contains("snapshots are not reinterpreted", rule.Description, StringComparison.Ordinal);
        });

        Assert.Contains("candidate HL is destroyed", context.Description, StringComparison.Ordinal);
        Assert.Contains("starts no new bullish correction", context.Description, StringComparison.Ordinal);
        Assert.Contains("old StructuralPrice and ProtectionAnchor remain unchanged", context.Description, StringComparison.Ordinal);
        Assert.Contains("not an automatically validated LH", context.Description, StringComparison.Ordinal);
        Assert.Contains("no intrabar chronology", context.Description, StringComparison.Ordinal);
        Assert.Contains("Step-3/Step-4 mapping", context.Description, StringComparison.Ordinal);

        Assert.Contains("cannot survive as a structural point", liquidity.Description, StringComparison.Ordinal);
        Assert.Contains("does not automatically create a validated LH", liquidity.Description, StringComparison.Ordinal);
        Assert.Contains("does not infer a Step-3 liquidity take, Step-4 trigger, IFVG/FVG confirmation, entry gating, or same-frame propagation", liquidity.Description, StringComparison.Ordinal);

        Assert.Contains("destroyed candidate HL cannot enter structural fallback", target.Description, StringComparison.Ordinal);
        Assert.Contains("without changing that candidate's StructuralPrice or ProtectionAnchor", target.Description, StringComparison.Ordinal);
        Assert.Contains("Only causally validated structural points may enter the structural fallback", target.Description, StringComparison.Ordinal);
        Assert.Contains("does not automatically become a validated LH", target.Description, StringComparison.Ordinal);
        Assert.Contains("does not infer Step-3/Step-4, IFVG/FVG, entry-gating, or same-frame behavior", target.Description, StringComparison.Ordinal);

        AssertFrozenMetadata("NQ-LIQ-003", "Liquidity take required", "Sweep", 70, true, RuleDefinitionStatus.Confirmed);
        AssertFrozenMetadata("NQ-M5-001", "Inversion alternatives", "5M inversion", 110, true, RuleDefinitionStatus.Confirmed);
    }

    [Fact]
    public void OpeningGapMetadataHasNoStructuralRoleAndPreservesRemainingBlockers()
    {
        var context = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-H4-001");
        var liquidity = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-LIQ-002");
        var target = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-TP-001");

        Assert.Equal(RuleDefinitionStatus.Confirmed, context.DefinitionStatus);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, liquidity.DefinitionStatus);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, target.DefinitionStatus);

        Assert.All(new[] { context, liquidity, target }, rule =>
        {
            Assert.Contains("previous Close != current Open neither starts nor fragments a correction", rule.Description, StringComparison.Ordinal);
            Assert.Contains("Exact body direction still governs correction start", rule.Description, StringComparison.Ordinal);
            Assert.Contains("strict High > current correction-origin ceiling or Low < current correction-origin floor governs reset", rule.Description, StringComparison.Ordinal);
            Assert.Contains("formally closed Close beyond prior validated HL/LH governs invalidation", rule.Description, StringComparison.Ordinal);
            Assert.Contains("an Open beyond a level is not an independent event", rule.Description, StringComparison.Ordinal);
            Assert.Contains("A gap Open across a prior validated HL/LH without the qualifying Close does not invalidate", rule.Description, StringComparison.Ordinal);
            Assert.Contains("Equality and wick-only penetration remain non-invalidating", rule.Description, StringComparison.Ordinal);
            Assert.Contains("No synthetic interpolation, candles, or interbar path is inferred", rule.Description, StringComparison.Ordinal);
            Assert.Contains("Opening gaps are distinct from FVG/IFVG and do not resolve their geometry, size, fill, or 1M interaction", rule.Description, StringComparison.Ordinal);
            Assert.DoesNotContain("thresholds, gaps, remaining candle inclusivity", rule.Description, StringComparison.Ordinal);
        });

        Assert.Contains("creates or validates HH/HL/LH/LL", context.Description, StringComparison.Ordinal);
        Assert.Contains("within structural liquidity review", liquidity.Description, StringComparison.Ordinal);
        Assert.Contains("do not infer a liquidity-take evaluation or downstream workflow behavior", liquidity.Description, StringComparison.Ordinal);
        Assert.Contains("nor creates a structural target", target.Description, StringComparison.Ordinal);
        Assert.Contains("Only causally validated structural points may enter fallback", target.Description, StringComparison.Ordinal);
        Assert.Contains("initial structural anchor selection from arbitrary raw candles", target.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PDH/PDL ranking, ties, general final-target hierarchy, and universal full-exit behavior remain unresolved or human-validated", target.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void H4BootstrapBoundaryMetadataSeparatesConfirmedProgressionFromHumanInitialization()
    {
        var context = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-H4-001");
        var liquidity = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-LIQ-002");
        var target = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-TP-001");

        Assert.Equal(RuleDefinitionStatus.Confirmed, context.DefinitionStatus);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, liquidity.DefinitionStatus);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, target.DefinitionStatus);

        Assert.Contains("No fixed candle/day/session count, historical time window, scan count, or numeric pivot/fractal lookback defines 4H bootstrap", context.Description, StringComparison.Ordinal);
        Assert.Contains("Initial structural anchor selection from arbitrary raw 4H candles", context.Description, StringComparison.Ordinal);
        Assert.Contains("termination/boundary of that initial historical scan remain human-validation-required", context.Description, StringComparison.Ordinal);
        Assert.Contains("latest strict formally closed body break above an upper reference inherits bullish context", context.Description, StringComparison.Ordinal);
        Assert.Contains("latest strict formally closed body break below a lower reference inherits bearish context", context.Description, StringComparison.Ordinal);
        Assert.Contains("equality and wick-only penetration do not qualify", context.Description, StringComparison.Ordinal);
        Assert.Contains("future data cannot supply the break", context.Description, StringComparison.Ordinal);
        Assert.Contains("latest HH plus originating/validated HL or latest LL plus originating/validated LH becomes the operational active pair", context.Description, StringComparison.Ordinal);
        Assert.Contains("older structure remains audit evidence", context.Description, StringComparison.Ordinal);
        Assert.Contains("viewport, zoom, screen width, arbitrary loaded-history count", context.Description, StringComparison.Ordinal);
        Assert.Contains("weekly open, previous-day extrema, and Asia/London levels are not deterministic anchor selectors or mandatory seeds", context.Description, StringComparison.Ordinal);
        Assert.Contains("Post-anchor structural mechanics remain causal and future-safe", context.Description, StringComparison.Ordinal);

        Assert.Contains("Given a valid human-approved seeded structural context, structural points continue under the documented causal body-close and correction rules", liquidity.Description, StringComparison.Ordinal);
        Assert.Contains("Initial structural anchor selection from arbitrary raw 4H candles", liquidity.Description, StringComparison.Ordinal);
        Assert.Contains("termination/boundary of that initial historical scan remain human-reviewed", liquidity.Description, StringComparison.Ordinal);
        Assert.Contains("NQ-LIQ-001 session levels, weekly open, and previous-day extrema are liquidity/context references rather than automatic H4 structural seeds", liquidity.Description, StringComparison.Ordinal);

        Assert.Contains("Once structure is established, structural fallback may use only causally validated 1H/4H points", target.Description, StringComparison.Ordinal);
        Assert.Contains("remaining H4 bootstrap boundary is specifically initial structural anchor selection", target.Description, StringComparison.Ordinal);
        Assert.Contains("termination/boundary of the initial historical scan", target.Description, StringComparison.Ordinal);
        Assert.Contains("OHLC mitigation test", target.Description, StringComparison.Ordinal);
        Assert.Contains("PDH/PDL ranking, ties", target.Description, StringComparison.Ordinal);
        Assert.Contains("general final-target hierarchy, and universal full-exit behavior", target.Description, StringComparison.Ordinal);

        Assert.All(new[] { context, liquidity, target }, rule =>
        {
            Assert.DoesNotContain("bootstrap selection", rule.Description, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("bootstrap/lookback", rule.Description, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("thresholds, gaps", rule.Description, StringComparison.OrdinalIgnoreCase);
        });

        AssertFrozenMetadata("NQ-LIQ-001", "Session liquidity levels", "Liquidity", 50, true, RuleDefinitionStatus.Confirmed);
        AssertFrozenMetadata("NQ-TP-002", "Asia/London liquidity targets", "Take Profit", 220, false, RuleDefinitionStatus.HumanValidationRequired);
    }

    [Fact]
    public void EqualPriceStructuralIdentityMetadataKeepsPriceLevelsAndRemainingBoundariesSeparate()
    {
        var context = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-H4-001");
        var liquidity = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-LIQ-002");
        var target = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-TP-001");

        Assert.Equal(RuleDefinitionStatus.Confirmed, context.DefinitionStatus);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, liquidity.DefinitionStatus);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, target.DefinitionStatus);

        Assert.Contains("one price-based StructuralPrice level", context.Description, StringComparison.Ordinal);
        Assert.Contains("no candle owner or first/last/timestamp/sequence tie-break is required", context.Description, StringComparison.Ordinal);
        Assert.Contains("multiple supporting candles do not create duplicate HL/LH points", context.Description, StringComparison.Ordinal);
        Assert.Contains("wick-based ProtectionAnchor may come from another candle in that turn", context.Description, StringComparison.Ordinal);
        Assert.Contains("Near-equal coordinates remain unresolved", context.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("equal-coordinate body identity", context.Description, StringComparison.Ordinal);

        Assert.Contains("one price-based structural level", liquidity.Description, StringComparison.Ordinal);
        Assert.Contains("multiple supporting candles do not create duplicate structural points", liquidity.Description, StringComparison.Ordinal);
        Assert.Contains("A price extreme alone remains insufficient to validate a structural point", liquidity.Description, StringComparison.Ordinal);
        Assert.Contains("Near-equal coordinates remain unresolved", liquidity.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("equal-coordinate body identity", liquidity.Description, StringComparison.Ordinal);

        Assert.Contains("one candidate price level", target.Description, StringComparison.Ordinal);
        Assert.Contains("no candle owner or first/last/timestamp/sequence tie-break is required", target.Description, StringComparison.Ordinal);
        Assert.Contains("only causally validated structural points may be used", target.Description, StringComparison.Ordinal);
        Assert.Contains("Near-equal coordinates remain unresolved", target.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("equal-coordinate body identity", target.Description, StringComparison.Ordinal);

        Assert.All(new[] { context, liquidity, target }, rule =>
        {
            Assert.DoesNotContain("Opening gaps are unresolved", rule.Description, StringComparison.Ordinal);
            Assert.DoesNotContain("bearish reset/invalidation symmetry remain unresolved", rule.Description, StringComparison.Ordinal);
        });

        AssertFrozenMetadata("NQ-LIQ-001", "Session liquidity levels", "Liquidity", 50, true, RuleDefinitionStatus.Confirmed);
        AssertFrozenMetadata("NQ-TP-002", "Asia/London liquidity targets", "Take Profit", 220, false, RuleDefinitionStatus.HumanValidationRequired);
    }

    [Fact]
    public void DistinctExtremeCandleMetadataKeepsPreStartBoundarySeparateFromActiveTurnMembership()
    {
        var context = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-H4-001");
        var liquidity = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-LIQ-002");
        var target = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-TP-001");

        Assert.Equal(RuleDefinitionStatus.Confirmed, context.DefinitionStatus);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, liquidity.DefinitionStatus);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, target.DefinitionStatus);

        Assert.All(new[] { context, liquidity, target }, rule =>
        {
            Assert.Contains("HH-producing candle is distinct, it remains in the prior bullish impulse and outside the new correction", rule.Description, StringComparison.Ordinal);
            Assert.Contains("exact-doji HH candle before that first bearish body is likewise excluded", rule.Description, StringComparison.Ordinal);
            Assert.Contains("LL-producing candle is distinct, it remains in the prior bearish impulse and outside the new correction", rule.Description, StringComparison.Ordinal);
            Assert.Contains("exact-doji LL candle before that first bullish body is likewise excluded", rule.Description, StringComparison.Ordinal);
            Assert.Contains("Open, Close, High, and Low cannot alter the new candidate turn StructuralPrice or ProtectionAnchor", rule.Description, StringComparison.Ordinal);
            Assert.Contains("first member of the new bullish-correction turn", rule.Description, StringComparison.Ordinal);
            Assert.Contains("in-range bearish, bullish, and exact-doji candles remain in one correction turn", rule.Description, StringComparison.Ordinal);
            Assert.Contains("Near-doji/small-body thresholds remain unresolved", rule.Description, StringComparison.Ordinal);
            Assert.DoesNotContain("distinct extreme candle membership remains unresolved", rule.Description, StringComparison.OrdinalIgnoreCase);
        });

        Assert.Contains("first bullish candle that begins upward displacement starts the correction", context.Description, StringComparison.Ordinal);
        Assert.Contains("A price extreme alone remains insufficient to validate a structural point", liquidity.Description, StringComparison.Ordinal);
        Assert.Contains("Only causally validated structural points may enter fallback", target.Description, StringComparison.Ordinal);
        Assert.Contains("PDH/PDL ranking, ties, general final-target hierarchy, and universal full-exit behavior remain unresolved or human-validated", target.Description, StringComparison.Ordinal);

        AssertFrozenMetadata("NQ-LIQ-001", "Session liquidity levels", "Liquidity", 50, true, RuleDefinitionStatus.Confirmed);
        AssertFrozenMetadata("NQ-TP-002", "Asia/London liquidity targets", "Take Profit", 220, false, RuleDefinitionStatus.HumanValidationRequired);
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

    [Fact]
    public void BearishMirrorMetadataReconcilesTerminalSemanticsWithoutChangingRuleStatesOrGates()
    {
        var context = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-H4-001");
        var liquidity = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-LIQ-002");
        var target = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-TP-001");
        var sweep = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-LIQ-003");
        var inversion = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-M5-001");

        Assert.Equal(RuleDefinitionStatus.Confirmed, context.DefinitionStatus);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, liquidity.DefinitionStatus);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, target.DefinitionStatus);

        Assert.DoesNotContain("bearish reset/invalidation symmetry", context.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("bearish reset/invalidation symmetry", liquidity.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("bearish reset/invalidation symmetry", target.Description, StringComparison.Ordinal);

        Assert.Contains("in-range bullish, bearish, and exact-doji candles remain in one turn", context.Description, StringComparison.Ordinal);
        Assert.Contains("Low < current correction-origin floor resets the old correction", context.Description, StringComparison.Ordinal);
        Assert.Contains("only when Close > Open", context.Description, StringComparison.Ordinal);
        Assert.Contains("Close == Open or Close < Open resets but starts no new correction", context.Description, StringComparison.Ordinal);
        Assert.Contains("Close > prior validated LH invalidates bearish structure", context.Description, StringComparison.Ordinal);
        Assert.Contains("equality and wick-only High > prior LH with Close <= prior LH do not", context.Description, StringComparison.Ordinal);
        Assert.Contains("cannot alter its prior StructuralPrice or ProtectionAnchor", context.Description, StringComparison.Ordinal);
        Assert.Contains("invalidation dominates: no new bearish correction starts", context.Description, StringComparison.Ordinal);
        Assert.Contains("not a validated HL, Structural Low, or executable Stop Loss", context.Description, StringComparison.Ordinal);
        Assert.Contains("context-specific and does not automatically satisfy NQ-LIQ-003 or NQ-M5-001", context.Description, StringComparison.Ordinal);
        Assert.Contains("or enable same-frame propagation", context.Description, StringComparison.Ordinal);

        Assert.Contains("updates a price floor rather than validating an LL", liquidity.Description, StringComparison.Ordinal);
        Assert.Contains("The destroyed candidate LH cannot survive as a structural point", liquidity.Description, StringComparison.Ordinal);
        Assert.Contains("collision Low is not an automatically validated HL usable as fallback", target.Description, StringComparison.Ordinal);
        Assert.Contains("The destroyed candidate LH cannot become a usable structural fallback point", target.Description, StringComparison.Ordinal);
        Assert.Contains("Only causally validated structural points may enter the structural fallback", target.Description, StringComparison.Ordinal);

        Assert.Contains("first valid event", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("selected relevant High", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("downstream progression requires alignment with the Step-1 4H permitted direction", sweep.Description, StringComparison.Ordinal);
        Assert.Contains("only after its valid Step-3 liquidity take aligned with the Step-1 4H permitted direction", inversion.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("same-frame", sweep.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("same-frame", inversion.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PriorStructuralBoundaryMetadataReconcilesOnlyConfirmedBoundaries()
    {
        var context = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-H4-001");
        var liquidity = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-LIQ-002");
        var target = definition.Rules.Single(rule => rule.RuleId.Value == "NQ-TP-001");

        Assert.Equal(RuleDefinitionStatus.Confirmed, context.DefinitionStatus);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, liquidity.DefinitionStatus);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, target.DefinitionStatus);

        Assert.All(new[] { context, liquidity, target }, rule =>
        {
            Assert.DoesNotContain("remaining prior-HH/LL boundary inclusivity", rule.Description, StringComparison.Ordinal);
            Assert.DoesNotContain("remaining candle inclusivity", rule.Description, StringComparison.Ordinal);
            Assert.Contains("prior HH/LL", rule.Description, StringComparison.Ordinal);
            Assert.Contains("wick-only penetration", rule.Description, StringComparison.Ordinal);
            Assert.Contains("Close > prior HH or Close < prior LL", rule.Description, StringComparison.Ordinal);
            Assert.Contains("AwaitingCorrectionStart", rule.Description, StringComparison.Ordinal);
            Assert.Contains("StructureInvalidated", rule.Description, StringComparison.Ordinal);
            Assert.Contains("empty/inactive", rule.Description, StringComparison.Ordinal);
        });

        Assert.Contains("distinct from the current correction-origin ceiling/floor", context.Description, StringComparison.Ordinal);
        Assert.Contains("that candle remains in the turn", context.Description, StringComparison.Ordinal);
        Assert.Contains("eligible for StructuralPrice and ProtectionAnchor", context.Description, StringComparison.Ordinal);
        Assert.Contains("The confirming candle is excluded", context.Description, StringComparison.Ordinal);
        Assert.Contains("Strict High > current correction-origin ceiling or Low < current correction-origin floor", context.Description, StringComparison.Ordinal);

        Assert.Contains("active candidate turn remains open", liquidity.Description, StringComparison.Ordinal);
        Assert.Contains("A raw price extreme alone is not a validated structural point", liquidity.Description, StringComparison.Ordinal);
        Assert.Contains("confirming candle is excluded from its geometry", liquidity.Description, StringComparison.Ordinal);

        Assert.Contains("active candidate candle stays eligible for StructuralPrice and ProtectionAnchor", target.Description, StringComparison.Ordinal);
        Assert.Contains("confirming candle is excluded from the candidate geometry", target.Description, StringComparison.Ordinal);
        Assert.Contains("Only causally validated structural points may later serve as structural fallback", target.Description, StringComparison.Ordinal);

        Assert.Contains("Exact bearish bodies in bullish context and exact bullish bodies in bearish context start corrections", liquidity.Description, StringComparison.Ordinal);
        Assert.Contains("exact dojis do not", liquidity.Description, StringComparison.Ordinal);
        Assert.Contains("strict new-extreme waiting candles remain outside the future correction turn", context.Description, StringComparison.Ordinal);
        Assert.Contains("updates the extreme and starts the correction as its first member", context.Description, StringComparison.Ordinal);
        Assert.Contains("cannot alter a fallback candidate turn's StructuralPrice or ProtectionAnchor", target.Description, StringComparison.Ordinal);
        Assert.Contains("cannot be reused as waiting", target.Description, StringComparison.Ordinal);
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

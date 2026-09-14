using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Forex;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Capabilities;
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
    public void LegacyEvaluatorDoesNotCountAsCanonicalImplementation()
    {
        var required = Forex.Rules.First(x => x.IsRequired);
        ISingleTimeframeReplayRuleEvaluator legacy = new LegacyFake(Forex.StrategyId, Forex.Version, required.RuleId);
        Assert.NotNull(legacy);
        var item = Catalog([], []).Find(Forex.StrategyId, Forex.Version)!.Rules.Single(x => x.RuleId == required.RuleId);
        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.NotImplemented, item.CapabilityStatus);
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

    [Fact]
    public void NasdaqTimingRulesUseNotImplementedFallbackAfterTimezoneReconciliation()
    {
        var declarations = MoneyWayReplayEvaluationCapabilityDeclarations.GetAll();
        var report = Catalog([], declarations).Find(Nasdaq.StrategyId, Nasdaq.Version)!;
        var timingRuleIds = new[] { new RuleId("NQ-TIME-001"), new RuleId("NQ-TIME-002") };

        Assert.DoesNotContain(declarations, declaration => timingRuleIds.Contains(declaration.RuleId));
        foreach (var ruleId in timingRuleIds)
        {
            var capability = report.Rules.Single(rule => rule.RuleId == ruleId);
            Assert.Equal(ReplayRuleEvaluationCapabilityStatus.NotImplemented, capability.CapabilityStatus);
            Assert.Equal(StrategyReplayEvaluationCapabilityCatalog.DefaultNotImplementedReason, capability.CapabilityReason);
            Assert.Null(capability.CapabilitySourceReference);
        }

        Assert.Equal(
            [new RuleId("NQ-H4-001"), new RuleId("NQ-M5-004"), new RuleId("NQ-SL-001"), new RuleId("NQ-TP-001")],
            report.Rules
                .Where(rule => rule.CapabilityStatus == ReplayRuleEvaluationCapabilityStatus.BlockedByUnresolvedSpecification)
                .Select(rule => rule.RuleId));

        Assert.Equal(32, report.TotalRuleCount);
        Assert.Equal(13, report.RequiredRuleCount);
        Assert.Equal(0, report.ImplementedCount);
        Assert.Equal(0, report.HumanOnlyCount);
        Assert.Equal(28, report.NotImplementedCount);
        Assert.Equal(4, report.BlockedByUnresolvedSpecificationCount);
        Assert.Equal(0, report.RequiredImplementedCount);
        Assert.Equal(13, report.RequiredEvaluatorGapCount);
        Assert.False(report.HasFullRequiredEvaluatorRegistration);

        var forex = Catalog([], declarations).Find(Forex.StrategyId, Forex.Version)!;
        Assert.Equal((17, 15, 0, 0, 13, 4, 0, 15, false),
            (forex.TotalRuleCount, forex.RequiredRuleCount, forex.ImplementedCount, forex.HumanOnlyCount,
                forex.NotImplementedCount, forex.BlockedByUnresolvedSpecificationCount,
                forex.RequiredImplementedCount, forex.RequiredEvaluatorGapCount,
            forex.HasFullRequiredEvaluatorRegistration));
    }

    [Fact]
    public void NasdaqRelevantLiquidityHasNoExplicitCapabilityDeclaration()
    {
        var declarations = MoneyWayReplayEvaluationCapabilityDeclarations.GetAll();
        var report = Catalog(MoneyWayReplayRuleEvaluators.GetAll(), declarations)
            .Find(Nasdaq.StrategyId, Nasdaq.Version)!;
        var capability = report.Rules.Single(item => item.RuleId == new RuleId("NQ-LIQ-002"));

        Assert.DoesNotContain(declarations, declaration =>
            declaration.StrategyId == Nasdaq.StrategyId &&
            declaration.StrategyVersion == Nasdaq.Version &&
            declaration.RuleId == capability.RuleId);
        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.NotImplemented, capability.CapabilityStatus);
        Assert.Equal(StrategyReplayEvaluationCapabilityCatalog.DefaultNotImplementedReason, capability.CapabilityReason);
        Assert.Null(capability.CapabilitySourceReference);
        Assert.Equal((32, 13, 3, 0, 25, 4, 3, 10, false), Counts(report));
    }

    [Fact]
    public void NasdaqStopLossCapabilityReasonRecognizesBothProtectionAnchorsWithoutExecutionSemantics()
    {
        var evaluators = MoneyWayReplayRuleEvaluators.GetAll();
        var declarations = MoneyWayReplayEvaluationCapabilityDeclarations.GetAll();
        var declaration = Assert.Single(declarations, item =>
            item.StrategyId == Nasdaq.StrategyId &&
            item.StrategyVersion == Nasdaq.Version &&
            item.RuleId == new RuleId("NQ-SL-001"));
        var report = Catalog(evaluators, declarations).Find(Nasdaq.StrategyId, Nasdaq.Version)!;

        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.BlockedByUnresolvedSpecification, declaration.Status);
        Assert.DoesNotContain(evaluators, evaluator =>
            evaluator.StrategyId == Nasdaq.StrategyId &&
            evaluator.StrategyVersion == Nasdaq.Version &&
            evaluator.RuleId == declaration.RuleId);
        Assert.Contains("Low/lowest wick extreme of the relevant structural 5M HL", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("High/highest wick/tail of the relevant structural 5M LH", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("Deterministic identification", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("executable Stop Loss price/offset remain unresolved", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("separate from structural coordinates, including the single-candle origin Open", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("does not define an executable Stop Loss price, buffer, tick distance, spread/cost adjustment, broker order, or execution semantics", declaration.Reason, StringComparison.Ordinal);
        Assert.Equal((32, 13, 3, 0, 25, 4, 3, 10, false), Counts(report));
    }

    [Fact]
    public void NasdaqTakeProfitCapabilityReasonNamesOnlyCurrentSpecificationBlockers()
    {
        var evaluators = MoneyWayReplayRuleEvaluators.GetAll();
        var declaration = Assert.Single(
            MoneyWayReplayEvaluationCapabilityDeclarations.GetAll(),
            item => item.StrategyId == Nasdaq.StrategyId &&
                item.StrategyVersion == Nasdaq.Version &&
                item.RuleId == new RuleId("NQ-TP-001"));

        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.BlockedByUnresolvedSpecification, declaration.Status);
        Assert.DoesNotContain(evaluators, evaluator =>
            evaluator.StrategyId == Nasdaq.StrategyId &&
            evaluator.StrategyVersion == Nasdaq.Version &&
            evaluator.RuleId == declaration.RuleId);
        Assert.Contains("Session target candidates", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("distinct first-encountered priority", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("equal-target merge are source-defined", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("Structural fallback is partially defined", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("1H/4H HL/LL support", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("Previous Day Low", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("1H/4H LH/HH resistance", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("Previous Day High", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("reviewed 1H-first/4H-extended relationship", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("not universal", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("Bullish candidate-HL reconstruction is partially defined", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("prior HH is the upper boundary", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("first bearish candle after HH production stops starts the correction", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("deeper lows replace shallower candidates", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("formally closed Close > prior HH", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("candidate usable from its AsOfUtc", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("lowest Open or Close body price", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("an isolated wick is insufficient", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("prior LL remains the structural reference", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("bearish impulse stops producing new lows", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("first bullish candle that begins upward displacement starts the correction", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("new lower extreme takes precedence", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("update the correction-origin floor to that Low", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("only if the same candle closes bullish", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("not an automatically validated structural LL", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("formally closed body below the prior structural LL", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("no synthetic intrabar chronology is inferred", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("new higher extreme takes precedence", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("update the correction-origin ceiling to that High", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("only if the same candle closes bearish", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("not an automatically validated structural HH", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("formally closed body above the prior structural HH", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("floor and ceiling precedence cases are symmetric", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("An exact doji with Close == Open is directionally neutral and starts neither correction", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("update the applicable correction-origin ceiling or floor but leave correction not started", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("neither turns the doji into an opposite-direction start candle nor validates a structural HH/LL", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("At that doji AsOfUtc, later candles cannot retroactively create a correction start", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("Near-doji/small-body thresholds remain unresolved; no epsilon, tick/pip/point tolerance, minimum body size, or percentage threshold is defined", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("doji/Close == Open, gaps, exact candle inclusivity and exact turn membership/segmentation cases", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("bullish-side bearish-body/new-high precedence", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("simultaneously produces a new low", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("causal confirming candle is excluded from the correction set and retrospective candidate HL/LH scan", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("belongs conceptually to the impulsive leg", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("validates the new HH/LL and preceding candidate only from that causal AsOfUtc", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("single-expansive-candle case, its Open supplies the structural HL/LH origin coordinate", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("its Close supplies causal validation", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("without making it a correction candle or requiring an intrabar path", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("does not replace multi-candle body-edge rules or use a protection wick as a target coordinate", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("exact origin-price coordinate, intrabar chronology, and synthetic path remain unresolved", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("Reproducible bearish correction boundaries", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("confirming-candle scan participation", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("Equal-coordinate candle identity", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("Direct bearish evidence confirms the correction start for the demonstrated case", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("candidate high remains provisional", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("formally closed Close < prior LL", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("validates the preceding highest candidate high as LH only from that causal AsOfUtc", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("wick-only penetration and an open candle do not confirm it", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("a higher candidate high replaces a lower candidate", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("superseded intermediate highs do not survive as LH", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("This confirms candidate-LH identity and its selected-turn target structural coordinate", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("highest Open or Close body edge", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("candle color does not alter it", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("a higher wick does not redefine it", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("distinct from buy/sell protection wick anchors", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("neither substitutes for the other", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("candidate-LH structural price selection", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("bearish body anchoring", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("candidate-LH selection, bearish symmetry", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("other structural-point selection and coordinates", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("exact OHLC mitigation test", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("PDH/PDL cross-class ranking", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("ties or coincidences remain unresolved", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("General final Take Profit hierarchy outside the reviewed cases", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("universal full-exit behavior", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("Deterministic structural-point identification", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("structural validation remains unresolved", declaration.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exact correction and turn boundaries", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("Candidate-HL selection and its body anchor", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("Target consumption after 08:30 but before Step-3 activation", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("Structural 1H/4H fallback is source-confirmed", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("1H-versus-4H priority", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("candidate ranking", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("equal, already-crossed, unavailable", declaration.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("equal-target behavior remains unresolved", declaration.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Asia/London selection remains unresolved", declaration.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("session target priority remains unresolved", declaration.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("important-high/important-low selection", declaration.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("touch", declaration.Reason, StringComparison.OrdinalIgnoreCase);

        var report = Catalog(evaluators, MoneyWayReplayEvaluationCapabilityDeclarations.GetAll())
            .Find(Nasdaq.StrategyId, Nasdaq.Version)!;
        var capability = report.Rules.Single(item => item.RuleId == declaration.RuleId);

        var priorWording = new ReplayRuleEvaluationCapabilityDeclaration(
            declaration.StrategyId,
            declaration.StrategyVersion,
            declaration.RuleId,
            declaration.Status,
            "Prior audited wording.",
            declaration.SourceReference);
        var reportWithPriorWording = Catalog(
            evaluators,
            MoneyWayReplayEvaluationCapabilityDeclarations.GetAll()
                .Select(item =>
                    item.StrategyId == declaration.StrategyId &&
                    item.StrategyVersion == declaration.StrategyVersion &&
                    item.RuleId == declaration.RuleId
                        ? priorWording
                        : item))
            .Find(Nasdaq.StrategyId, Nasdaq.Version)!;

        Assert.Equal(declaration.Status, capability.CapabilityStatus);
        Assert.Equal((32, 13, 3, 0, 25, 4, 3, 10, false), Counts(report));
        Assert.Equal(Counts(reportWithPriorWording), Counts(report));
    }

    [Fact]
    public void NasdaqBullishCorrectionMembershipCapabilityReasonsReflectOnlyCurrentBlockers()
    {
        var evaluators = MoneyWayReplayRuleEvaluators.GetAll();
        var declarations = MoneyWayReplayEvaluationCapabilityDeclarations.GetAll();
        var report = Catalog(evaluators, declarations).Find(Nasdaq.StrategyId, Nasdaq.Version)!;
        var context = Assert.Single(declarations, item => item.RuleId == new RuleId("NQ-H4-001"));
        var target = Assert.Single(declarations, item => item.RuleId == new RuleId("NQ-TP-001"));
        var liquidity = report.Rules.Single(item => item.RuleId == new RuleId("NQ-LIQ-002"));

        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.BlockedByUnresolvedSpecification, context.Status);
        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.BlockedByUnresolvedSpecification, target.Status);
        Assert.Contains("all in-range bearish, bullish, and exact-doji candles remain in one correction turn", context.Reason, StringComparison.Ordinal);
        Assert.Contains("body-color alternation does not fragment it or create parallel HL candidates", context.Reason, StringComparison.Ordinal);
        Assert.Contains("High > current correction-origin ceiling resets the correction", context.Reason, StringComparison.Ordinal);
        Assert.Contains("discards the current candidate HL without validating a structural HH", context.Reason, StringComparison.Ordinal);
        Assert.Contains("Close < prior validated HL invalidates the bullish structure", context.Reason, StringComparison.Ordinal);
        Assert.Contains("Close == prior validated HL and wick-only Low < prior validated HL with Close >= prior validated HL do not invalidate", context.Reason, StringComparison.Ordinal);
        Assert.Contains("terminal/reset candle membership or geometry", context.Reason, StringComparison.Ordinal);
        Assert.Contains("bearish reset/invalidation symmetry", context.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("exact turn membership/segmentation cases", context.Reason, StringComparison.Ordinal);

        Assert.Contains("all in-range bearish, bullish, and exact-doji candles remain in one correction turn", target.Reason, StringComparison.Ordinal);
        Assert.Contains("An invalidated bullish structure cannot yield a validated HL usable as a structural fallback target candidate", target.Reason, StringComparison.Ordinal);
        Assert.Contains("exact OHLC mitigation test", target.Reason, StringComparison.Ordinal);
        Assert.Contains("PDH/PDL cross-class ranking", target.Reason, StringComparison.Ordinal);
        Assert.Contains("universal full-exit behavior", target.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("exact turn membership/segmentation cases", target.Reason, StringComparison.Ordinal);

        Assert.DoesNotContain(declarations, item => item.RuleId == liquidity.RuleId);
        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.NotImplemented, liquidity.CapabilityStatus);
        Assert.Equal(StrategyReplayEvaluationCapabilityCatalog.DefaultNotImplementedReason, liquidity.CapabilityReason);
        Assert.DoesNotContain(evaluators, evaluator =>
            evaluator.StrategyId == Nasdaq.StrategyId &&
            evaluator.StrategyVersion == Nasdaq.Version &&
            (evaluator.RuleId == context.RuleId || evaluator.RuleId == target.RuleId));
        Assert.Equal((32, 13, 3, 0, 25, 4, 3, 10, false), Counts(report));
    }

    [Fact]
    public void NasdaqFourHourContextRemainsConfirmedWhileCapabilityIsExplicitlyBlocked()
    {
        var definition = Nasdaq;
        var rule = definition.Rules.Single(item => item.RuleId.Value == "NQ-H4-001");
        var evaluators = MoneyWayReplayRuleEvaluators.GetAll();
        var declarations = MoneyWayReplayEvaluationCapabilityDeclarations.GetAll();
        var declaration = Assert.Single(declarations, item =>
            item.StrategyId == definition.StrategyId &&
            item.StrategyVersion == definition.Version &&
            item.RuleId == rule.RuleId);
        var beforeCatalog = Catalog(evaluators, declarations.Where(item => item != declaration));
        var afterCatalog = Catalog(evaluators, declarations);
        var before = beforeCatalog.Find(definition.StrategyId, definition.Version)!;
        var after = afterCatalog.Find(definition.StrategyId, definition.Version)!;
        var beforeByRule = before.Rules.ToDictionary(item => item.RuleId);
        var afterByRule = after.Rules.ToDictionary(item => item.RuleId);

        Assert.Equal(RuleDefinitionStatus.Confirmed, rule.DefinitionStatus);
        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.NotImplemented, beforeByRule[rule.RuleId].CapabilityStatus);
        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.BlockedByUnresolvedSpecification, afterByRule[rule.RuleId].CapabilityStatus);
        Assert.NotEqual(ReplayRuleEvaluationCapabilityStatus.Implemented, afterByRule[rule.RuleId].CapabilityStatus);
        Assert.NotEqual(ReplayRuleEvaluationCapabilityStatus.HumanOnly, afterByRule[rule.RuleId].CapabilityStatus);
        Assert.DoesNotContain(evaluators, evaluator =>
            evaluator.StrategyId == definition.StrategyId &&
            evaluator.StrategyVersion == definition.Version &&
            evaluator.RuleId == rule.RuleId);

        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.BlockedByUnresolvedSpecification, declaration.Status);
        Assert.False(string.IsNullOrWhiteSpace(declaration.Reason));
        Assert.Contains("bullish candidate-HL reconstruction is partially defined", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("prior HH is the upper boundary", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("first bearish candle after HH production stops starts the correction", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("deeper lows replace shallower candidates", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("formally closed Close > prior HH", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("causal AsOfUtc", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("lowest Open or Close body price", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("prior LL remains the structural reference", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("bearish impulse stops producing new lows", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("first bullish candle that begins upward displacement starts the correction", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("new lower extreme takes precedence", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("update the correction-origin floor to that Low", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("only if the same candle closes bullish", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("not an automatically validated structural LL", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("formally closed body below the prior structural LL", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("no synthetic intrabar chronology is inferred", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("new higher extreme takes precedence", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("update the correction-origin ceiling to that High", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("only if the same candle closes bearish", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("not an automatically validated structural HH", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("formally closed body above the prior structural HH", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("floor and ceiling precedence cases are symmetric", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("An exact doji with Close == Open is directionally neutral and starts neither correction", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("update the applicable correction-origin ceiling or floor but leave correction not started", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("neither turns the doji into an opposite-direction start candle nor validates a structural HH/LL", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("At that doji AsOfUtc, later candles cannot retroactively create a correction start", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("Near-doji/small-body thresholds remain unresolved; no epsilon, tick/pip/point tolerance, minimum body size, or percentage threshold is defined", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("doji/Close == Open, gaps, exact candle inclusivity and exact turn membership/segmentation cases", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("bullish-side bearish-body/new-high precedence", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("simultaneously produces a new low", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("causal confirming candle is excluded from the correction set and retrospective candidate HL/LH scan", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("belongs conceptually to the impulsive leg", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("validates the new HH/LL and preceding candidate only from that causal AsOfUtc", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("single-expansive-candle case, its Open supplies the structural HL/LH origin coordinate", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("its Close supplies causal validation", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("without making it a correction candle or requiring an intrabar path", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("This does not replace multi-candle body-edge rules", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("exact origin-price coordinate, intrabar chronology, and synthetic path remain unresolved", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("Reproducible bearish correction boundaries", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("confirming-candle scan participation", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("terminal/reset candle membership", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("equal-coordinate candle identity", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("Direct bearish evidence confirms the correction start for the demonstrated case", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("candidate high remains provisional", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("formally closed Close < prior LL", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("validates the preceding highest candidate high as LH only from that causal AsOfUtc", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("wick-only penetration and an open candle do not confirm it", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("a higher candidate high replaces a lower candidate", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("superseded intermediate highs do not survive as LH", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("This confirms LH structural identity and its selected-turn structural coordinate", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("highest Open or Close body edge", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("candle color does not alter it", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("a higher wick does not redefine it", declaration.Reason, StringComparison.Ordinal);
        Assert.Contains("distinct from the buy HL Low/lowest-wick and sell LH High/highest-wick protection anchors", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("candidate-LH structural price selection", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("bearish body anchoring", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("candidate-LH selection, bearish symmetry", declaration.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("retracement pivots from closed candles", declaration.Reason, StringComparison.Ordinal);
        Assert.Equal("docs/strategies/nasdaq/rule-catalog.md", declaration.SourceReference);
        Assert.Equal(declarations.Count, declarations
            .Select(item => (item.StrategyId, item.StrategyVersion, item.RuleId))
            .Distinct()
            .Count());

        Assert.Equal((32, 13, 3, 0, 26, 3, 3, 10, false), Counts(before));
        Assert.Equal((32, 13, 3, 0, 25, 4, 3, 10, false), Counts(after));
        Assert.All(after.Rules.Where(item => item.RuleId != rule.RuleId), item =>
            Assert.Equal(beforeByRule[item.RuleId].CapabilityStatus, item.CapabilityStatus));

        var forexDefinition = Forex;
        var forexBefore = beforeCatalog.Find(forexDefinition.StrategyId, forexDefinition.Version)!;
        var forexAfter = afterCatalog.Find(forexDefinition.StrategyId, forexDefinition.Version)!;
        Assert.Equal((17, 15, 0, 0, 13, 4, 0, 15, false), Counts(forexBefore));
        Assert.Equal(Counts(forexBefore), Counts(forexAfter));
    }

    private static StrategyDefinition Forex => MoneyWayForexStrategyDefinition.Instance;
    private static StrategyDefinition Nasdaq => MoneyWayNasdaqStrategyDefinition.Instance;
    private static StrategyReplayEvaluationCapabilityCatalog Catalog(IEnumerable<IReplayRuleEvaluator> evaluators, IEnumerable<ReplayRuleEvaluationCapabilityDeclaration> declarations) => new(new StrategyDefinitionCatalog(), evaluators, declarations);
    private static (int, int, int, int, int, int, int, int, bool) Counts(StrategyReplayEvaluationCapabilityReport report) =>
        (report.TotalRuleCount, report.RequiredRuleCount, report.ImplementedCount, report.HumanOnlyCount,
            report.NotImplementedCount, report.BlockedByUnresolvedSpecificationCount,
            report.RequiredImplementedCount, report.RequiredEvaluatorGapCount,
            report.HasFullRequiredEvaluatorRegistration);
    private sealed class Fake(StrategyId strategyId, StrategyVersion version, RuleId ruleId) : IReplayRuleEvaluator
    {
        public StrategyId StrategyId { get; } = strategyId; public StrategyVersion StrategyVersion { get; } = version; public RuleId RuleId { get; } = ruleId;
        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context) => new(RuleEvaluationResult.Passed, "Synthetic.", null);
    }
    private sealed class LegacyFake(StrategyId strategyId, StrategyVersion version, RuleId ruleId) : ISingleTimeframeReplayRuleEvaluator
    {
        public StrategyId StrategyId { get; } = strategyId; public StrategyVersion StrategyVersion { get; } = version; public RuleId RuleId { get; } = ruleId;
        public ReplayRuleEvaluationDecision Evaluate(MoneyWay.Domain.MarketData.Replay.ReplayFrame frame) => new(RuleEvaluationResult.Passed, "Synthetic.", null);
    }
}

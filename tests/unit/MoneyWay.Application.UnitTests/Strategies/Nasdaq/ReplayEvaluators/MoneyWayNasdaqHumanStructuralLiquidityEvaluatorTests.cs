using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Forex;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Capabilities;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayEvaluators;

public sealed class MoneyWayNasdaqHumanStructuralLiquidityEvaluatorTests
{
    private readonly MoneyWayNasdaqHumanStructuralLiquidityEvaluator evaluator = new();

    [Fact]
    public void MissingSelectionAndUnrelatedPreparationRequireHumanValidation()
    {
        var preparation = new NasdaqPreparationCompletionObservation(LiquidityFixture.Definition.StrategyId,
            LiquidityFixture.Definition.Version, LiquidityFixture.Provider, LiquidityFixture.Symbol,
            new(2026, 10, 3), LiquidityFixture.At(13), "preparation");
        foreach (var inputs in new IStrategyReplayInputObservation[][] { [], [preparation] })
        {
            var decision = evaluator.Evaluate(Context([], inputs));
            Assert.Equal(RuleEvaluationResult.HumanValidationRequired, decision.Result);
            Assert.Contains("No usable same-session", decision.Reason);
            Assert.Null(decision.EvidenceReference);
        }
        Assert.Throws<ArgumentException>(() => LiquidityFixture.Observation([]));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 4)]
    [InlineData(true, 4)]
    public void MixedHumanAndFormulaReferencesPassWithoutChangingPriceOwnership(bool low, int hours)
    {
        var candle = LiquidityFixture.Candle(8, hours: hours);
        var formula = NasdaqStructuralLiquidityReference.OrdinaryTurn(
            low ? NasdaqHumanH4StructuralRole.HigherLow : NasdaqHumanH4StructuralRole.LowerHigh, [candle], "formula");
        var human = LiquidityFixture.Human(candle, low, 21500);
        var decision = evaluator.Evaluate(Context([candle], [LiquidityFixture.Observation([formula, human])]));
        Assert.Equal(RuleEvaluationResult.Passed, decision.Result);
        Assert.Contains("no liquidity take is implied", decision.Reason);
        using var json = JsonDocument.Parse(decision.EvidenceReference!);
        var references = json.RootElement[0].GetProperty("References").EnumerateArray().ToArray();
        Assert.Equal(2, references.Length);
        Assert.Contains(references, item => item.GetProperty("StructuralPrice").GetDecimal() == 21500
            && item.GetProperty("PriceOwnership").GetInt32() == (int)NasdaqStructuralLiquidityPriceOwnership.HumanDocumented);
        Assert.Contains(references, item => item.GetProperty("StructuralPrice").GetDecimal() == 100
            && item.GetProperty("PriceOwnership").GetInt32() == (int)NasdaqStructuralLiquidityPriceOwnership.FormulaBacked);
    }

    [Fact]
    public void ConflictRetainsAllAlternativesAndDoesNotUseMajorityOrLatestReview()
    {
        var candle = LiquidityFixture.Candle(8);
        var first = LiquidityFixture.Observation([LiquidityFixture.Human(candle)], source: "a");
        var other = LiquidityFixture.Observation([LiquidityFixture.Human(candle, price: 200)], source: "b");
        var majority = LiquidityFixture.Observation([LiquidityFixture.Human(candle)], observed: 14, source: "c");
        var decision = evaluator.Evaluate(Context([candle], [majority, other, first], 14));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, decision.Result);
        Assert.Contains("conflict", decision.Reason);
        using var json = JsonDocument.Parse(decision.EvidenceReference!);
        Assert.Equal(new[] { "a", "b", "c" }, json.RootElement.EnumerateArray().Select(item => item.GetProperty("SourceReference").GetString()));
        Assert.Equal(200, json.RootElement[1].GetProperty("References")[0].GetProperty("StructuralPrice").GetDecimal());
    }

    [Fact]
    public void CompatibleSupportIsLosslessStableAndDoesNotMutateContext()
    {
        var candle = LiquidityFixture.Candle(8);
        var first = LiquidityFixture.Observation([LiquidityFixture.Human(candle)], source: "a");
        var second = LiquidityFixture.Observation([LiquidityFixture.Human(candle)], observed: 14, source: "b");
        var context = Context([candle], [second, first], 14);
        var decision = evaluator.Evaluate(context);
        Assert.Equal(RuleEvaluationResult.Passed, decision.Result);
        Assert.Equal(decision.EvidenceReference, evaluator.Evaluate(Context([candle], [first, second], 14)).EvidenceReference);
        Assert.Equal(decision.EvidenceReference, evaluator.Evaluate(context).EvidenceReference);
        Assert.Equal(2, context.InputObservations.Count);
        using var json = JsonDocument.Parse(decision.EvidenceReference!);
        Assert.Equal(2, json.RootElement.GetArrayLength());
        Assert.Equal(first.EffectiveAtUtc, json.RootElement[0].GetProperty("EffectiveAtUtc").GetDateTimeOffset());
        Assert.Equal(second.ObservedAtUtc, json.RootElement[1].GetProperty("ObservedAtUtc").GetDateTimeOffset());
        Assert.Equal(first.References[0].SourceReference, json.RootElement[0].GetProperty("References")[0].GetProperty("Provenance").GetProperty("SourceReference").GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void FutureMissingSourceAndWrongSessionSelectionsCannotPass(int scenario)
    {
        var candle = LiquidityFixture.Candle(scenario == 2 ? 14 : 8, hours: 4);
        var observation = LiquidityFixture.Observation([LiquidityFixture.Human(candle)],
            effective: scenario == 2 ? 18 : 12, observed: scenario == 2 ? 18 : scenario == 0 ? 14 : 13,
            session: scenario == 3 ? LiquidityFixture.Session(40) : LiquidityFixture.Session(13));
        var decision = evaluator.Evaluate(Context(scenario == 1 ? [] : [candle], [observation]));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, decision.Result);
        Assert.Null(decision.EvidenceReference);
    }

    [Fact]
    public void FutureConflictingReviewCannotChangeEarlierDecision()
    {
        var candle = LiquidityFixture.Candle(8);
        var early = LiquidityFixture.Observation([LiquidityFixture.Human(candle)]);
        var late = LiquidityFixture.Observation([LiquidityFixture.Human(candle, price: 200)], observed: 15);
        var baseline = evaluator.Evaluate(Context([candle], [early]));
        var before = evaluator.Evaluate(Context([candle], [early, late]));
        Assert.Equal(baseline.EvidenceReference, before.EvidenceReference);
        Assert.Equal(baseline.Result, before.Result);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, evaluator.Evaluate(Context([candle], [early, late], 15)).Result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmbeddedHumanSupportIsPreservedWithoutSerializingRuntimeGraphs(bool cluster)
    {
        NasdaqStructuralLiquidityReference reference;
        Candle[] source;
        if (cluster)
        {
            var fixture = LiquidityFixture.Extreme(false);
            source = fixture.Source;
            reference = NasdaqStructuralLiquidityReference.PostCompletionExtreme(fixture.Geometry, "selected cluster");
        }
        else
        {
            var fixture = LiquidityFixture.HumanCompletion(false);
            source = fixture.Source;
            reference = NasdaqStructuralLiquidityReference.HumanCollision(fixture.Completed, "selected collision");
        }
        var observation = LiquidityFixture.Observation([reference], effective: 40, observed: 41);
        var decision = evaluator.Evaluate(Context(source, [observation], 41));
        Assert.Equal(RuleEvaluationResult.Passed, decision.Result);
        Assert.Contains(cluster ? "review:cluster first" : "review:008 first", decision.EvidenceReference);
        Assert.Contains(cluster ? "review:cluster second" : "review:008 second", decision.EvidenceReference);
        using var json = JsonDocument.Parse(decision.EvidenceReference!);
        var record = json.RootElement[0].GetProperty("References")[0];
        Assert.NotEqual(JsonValueKind.Null, record.GetProperty("Episode").ValueKind);
        Assert.NotEmpty(record.GetProperty("Sources").EnumerateArray());
        Assert.False(record.TryGetProperty("HumanCollisionCompletion", out _));
        Assert.False(record.TryGetProperty("ActiveExtremeGeometry", out _));
    }

    [Fact]
    public void SessionLiquidityCoverageAndSubsequentPriceActionAreIndependent()
    {
        var source = Enumerable.Range(-2, 14).Select(hour => LiquidityFixture.Candle(hour)).ToArray();
        var sessionEvaluator = new MoneyWayNasdaqSessionLiquidityEvaluator(new NasdaqSessionLiquidityCalculator());
        var baseline = Context(source, []);
        Assert.Equal(RuleEvaluationResult.Passed, sessionEvaluator.Evaluate(baseline).Result);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, evaluator.Evaluate(baseline).Result);
        var evidence = LiquidityFixture.Observation([LiquidityFixture.Human(source[0], price: 105)]);
        var context = Context(source, [evidence]);
        Assert.Equal(sessionEvaluator.Evaluate(baseline).EvidenceReference, sessionEvaluator.Evaluate(context).EvidenceReference);
        var decision = evaluator.Evaluate(context);
        var moved = evaluator.Evaluate(Context(source.Append(LiquidityFixture.Candle(12, 100, 300, 50, 250)).ToArray(), [evidence]));
        Assert.Equal(decision.Result, moved.Result);
        Assert.Equal(decision.EvidenceReference, moved.EvidenceReference);
    }

    [Fact]
    public void CanonicalUseCaseOwnsMetadataAndEvaluationTime()
    {
        var candle = LiquidityFixture.Candle(8);
        var context = Context([candle], [LiquidityFixture.Observation([LiquidityFixture.Human(candle)])], 14);
        var rule = LiquidityFixture.Definition.Rules.Single(item => item.RuleId == evaluator.RuleId);
        var evaluation = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll())
            .Execute(LiquidityFixture.Definition, context).Evaluations.Single(item => item.RuleId == evaluator.RuleId);
        Assert.Equal("NQ-LIQ-002", evaluator.RuleId.Value);
        Assert.Equal(LiquidityFixture.Definition.StrategyId, evaluator.StrategyId);
        Assert.Equal(LiquidityFixture.Definition.Version, evaluator.StrategyVersion);
        Assert.Equal(RuleDefinitionStatus.HumanValidationRequired, evaluation.DefinitionStatus);
        Assert.Equal(rule.Sequence, evaluation.Sequence);
        Assert.True(evaluation.IsRequired);
        Assert.Equal(context.AsOfUtc, evaluation.EvaluatedAtUtc);
        Assert.Equal(RuleEvaluationResult.Passed, evaluation.Result);
    }

    [Fact]
    public void RegistrationAloneChangesOnlyStructuralLiquidityCoverage()
    {
        var registry = MoneyWayReplayRuleEvaluators.GetAll().Where(item => item.RuleId.Value != "NQ-LIQ-003").ToArray();
        Assert.Single(registry, item => item.RuleId == evaluator.RuleId);
        var declarations = MoneyWayReplayEvaluationCapabilityDeclarations.GetAll();
        Assert.DoesNotContain(declarations, item => item.RuleId == evaluator.RuleId);
        var beforeCatalog = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), registry.Where(item => item.RuleId != evaluator.RuleId), declarations);
        var afterCatalog = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), registry, declarations);
        var definition = LiquidityFixture.Definition;
        var before = beforeCatalog.Find(definition.StrategyId, definition.Version)!;
        var after = afterCatalog.Find(definition.StrategyId, definition.Version)!;
        Assert.Equal((12, 2, false), (before.RequiredImplementedCount, before.RequiredEvaluatorGapCount, before.HasFullRequiredEvaluatorRegistration));
        Assert.Equal((13, 1, false), (after.RequiredImplementedCount, after.RequiredEvaluatorGapCount, after.HasFullRequiredEvaluatorRegistration));
        Assert.All(after.Rules.Where(item => item.RuleId != evaluator.RuleId), item =>
            Assert.Equal(before.Rules.Single(old => old.RuleId == item.RuleId).CapabilityStatus, item.CapabilityStatus));
        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.NotImplemented, after.Rules.Single(item => item.RuleId.Value == "NQ-LIQ-003").CapabilityStatus);
        var forex = MoneyWayForexStrategyDefinition.Instance;
        Assert.Equal(beforeCatalog.Find(forex.StrategyId, forex.Version)!.RequiredEvaluatorGapCount,
            afterCatalog.Find(forex.StrategyId, forex.Version)!.RequiredEvaluatorGapCount);
    }

    [Fact]
    public void NullAndForeignIdentityAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate(null!));
        var candle = LiquidityFixture.Candle(8);
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(candle.ProviderId, candle.Symbol, candle.Timeframe, [candle])]);
        Assert.True(cursor.TryAdvance(out var frame));
        var foreign = new CreateStrategyReplayContextUseCase().Execute(MoneyWayForexStrategyDefinition.Instance, frame!);
        Assert.Throws<InvalidOperationException>(() => evaluator.Evaluate(foreign));
        var definition = LiquidityFixture.Definition;
        var otherVersion = new StrategyDefinition(definition.StrategyId, new("other-version"), definition.DisplayName,
            definition.SpecificationReference, definition.Rules);
        Assert.Throws<InvalidOperationException>(() => evaluator.Evaluate(new CreateStrategyReplayContextUseCase().Execute(otherVersion, frame!)));
    }

    [Fact]
    public void AlteredSourceCandleCannotValidateSelectedGeometry()
    {
        var candle = LiquidityFixture.Candle(8);
        var observation = LiquidityFixture.Observation([LiquidityFixture.Human(candle)]);
        var altered = LiquidityFixture.Candle(8, close: 101);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, evaluator.Evaluate(Context([altered], [observation])).Result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanonicalDirectionalCollisionSelectionPassesWithFormulaOwnership(bool upper)
    {
        var fixture = LiquidityFixture.Breakout(upper, false);
        var completion = new NasdaqDirectionalMigrationBreakoutCompletionCalculator().Evaluate(fixture.Breakout);
        var reference = NasdaqStructuralLiquidityReference.DirectionalCollision(completion, "007 source");
        var observation = LiquidityFixture.Observation([reference], effective: 32, observed: 33);
        var decision = evaluator.Evaluate(Context(fixture.Source, [observation], 33));
        Assert.Equal(RuleEvaluationResult.Passed, decision.Result);
        using var json = JsonDocument.Parse(decision.EvidenceReference!);
        Assert.Equal(reference.StructuralPrice, json.RootElement[0].GetProperty("References")[0].GetProperty("StructuralPrice").GetDecimal());
        Assert.Equal((int)NasdaqStructuralLiquidityPriceOwnership.FormulaBacked,
            json.RootElement[0].GetProperty("References")[0].GetProperty("PriceOwnership").GetInt32());
    }

    private static StrategyReplayContext Context(Candle[] source, IStrategyReplayInputObservation[] inputs, int hour = 13) =>
        LiquidityFixture.Context(source, inputs, hour);
}

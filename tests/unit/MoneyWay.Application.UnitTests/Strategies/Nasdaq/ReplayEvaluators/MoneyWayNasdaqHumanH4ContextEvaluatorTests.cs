using System.Text.Json;
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

public sealed class MoneyWayNasdaqHumanH4ContextEvaluatorTests
{
    private readonly MoneyWayNasdaqHumanH4ContextEvaluator evaluator = new();

    [Fact]
    public void ExactRequiredRuleIdentityAndMissingMappingAreSourceBacked()
    {
        var rule = H4ContextFixture.Definition.Rules.Single(item => item.RuleId.Value == "NQ-H4-001");
        Assert.Equal(rule.RuleId, evaluator.RuleId);
        Assert.Equal(H4ContextFixture.Definition.StrategyId, evaluator.StrategyId);
        Assert.Equal(H4ContextFixture.Definition.Version, evaluator.StrategyVersion);
        Assert.True(rule.IsRequired);
        Assert.Equal(RuleDefinitionStatus.Confirmed, rule.DefinitionStatus);
        var decision = evaluator.Evaluate(Context(13));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, decision.Result);
        Assert.Contains("No usable same-session", decision.Reason);
        Assert.Null(decision.EvidenceReference);
    }

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy, NasdaqHumanH4ContextKind.Breakout)]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy, NasdaqHumanH4ContextKind.Wickfill)]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy, NasdaqHumanH4ContextKind.Fakeout)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell, NasdaqHumanH4ContextKind.Breakout)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell, NasdaqHumanH4ContextKind.Wickfill)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell, NasdaqHumanH4ContextKind.Fakeout)]
    public void ResolvedHumanReviewPassesWithoutReconstructingStructure(NasdaqHumanH4PermittedDirection direction,
        NasdaqHumanH4ContextKind kind)
    {
        var observation = H4ContextFixture.Observation(fact: H4ContextFixture.Fact(direction: direction, kind: kind));
        var decision = evaluator.Evaluate(Context(13, observation));
        Assert.Equal(RuleEvaluationResult.Passed, decision.Result);
        Assert.Contains("no autonomous H4 algorithm", decision.Reason);
        using var evidence = JsonDocument.Parse(decision.EvidenceReference!);
        var record = evidence.RootElement[0];
        Assert.Equal(observation.SourceReference, record.GetProperty("SourceReference").GetString());
        Assert.Equal(observation.EffectiveAtUtc, record.GetProperty("EffectiveAtUtc").GetDateTimeOffset());
        Assert.Equal(observation.ObservedAtUtc, record.GetProperty("ObservedAtUtc").GetDateTimeOffset());
        Assert.Equal("2026-10-03", record.GetProperty("Session").GetProperty("TradingDay").GetString());
        Assert.Equal(observation.Session.ProviderId.Value, record.GetProperty("ProviderId").GetProperty("Value").GetString());
        Assert.Equal(101, record.GetProperty("Fact").GetProperty("Anchors")[0].GetProperty("StructuralPrice").GetDecimal());
    }

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Unresolved, NasdaqHumanH4ContextKind.Breakout)]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy, NasdaqHumanH4ContextKind.Unresolved)]
    [InlineData(NasdaqHumanH4PermittedDirection.Unresolved, NasdaqHumanH4ContextKind.Unresolved)]
    public void EitherUnresolvedSemanticValueRequiresHumanValidation(NasdaqHumanH4PermittedDirection direction,
        NasdaqHumanH4ContextKind kind)
    {
        var decision = evaluator.Evaluate(Context(13,
            H4ContextFixture.Observation(fact: H4ContextFixture.Fact(direction: direction, kind: kind))));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, decision.Result);
        Assert.Contains("unresolved", decision.Reason);
        Assert.NotNull(decision.EvidenceReference);
    }

    [Fact]
    public void ConflictRetainsEveryAssertionWithoutPriorityOrSupersession()
    {
        var first = H4ContextFixture.Observation(source: "a");
        var other = H4ContextFixture.Observation(source: "b", fact: H4ContextFixture.Fact(direction: NasdaqHumanH4PermittedDirection.Sell));
        var majority = H4ContextFixture.Observation(observed: 14, source: "c");
        var decision = evaluator.Evaluate(Context(14, majority, other, first));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, decision.Result);
        Assert.Contains("conflict", decision.Reason);
        using var evidence = JsonDocument.Parse(decision.EvidenceReference!);
        Assert.Equal(3, evidence.RootElement.GetArrayLength());
        Assert.Equal(new[] { "a", "b", "c" }, evidence.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("SourceReference").GetString()));
    }

    [Fact]
    public void CompatibleReviewsPassWithEquivalentOutcomeAndLosslessSupport()
    {
        var first = H4ContextFixture.Observation(source: "a");
        var second = H4ContextFixture.Observation(observed: 14, source: "b");
        var single = evaluator.Evaluate(Context(14, first));
        var multiple = evaluator.Evaluate(Context(14, second, first));
        Assert.Equal(single.Result, multiple.Result);
        Assert.Equal(single.Reason, multiple.Reason);
        using var evidence = JsonDocument.Parse(multiple.EvidenceReference!);
        Assert.Equal(2, evidence.RootElement.GetArrayLength());
        Assert.Equal(first.ObservedAtUtc, evidence.RootElement[0].GetProperty("ObservedAtUtc").GetDateTimeOffset());
        Assert.Equal(second.ObservedAtUtc, evidence.RootElement[1].GetProperty("ObservedAtUtc").GetDateTimeOffset());
    }

    [Fact]
    public void FutureAssertionCannotChangeEarlierEvaluationAndLateConflictIsVisibleOnlyLater()
    {
        var early = H4ContextFixture.Observation();
        var late = H4ContextFixture.Observation(observed: 15, fact: H4ContextFixture.Fact(kind: NasdaqHumanH4ContextKind.Fakeout));
        var baseline = evaluator.Evaluate(Context(13, early));
        var before = evaluator.Evaluate(Context(13, early, late));
        Assert.Equal(baseline.Result, before.Result);
        Assert.Equal(baseline.EvidenceReference, before.EvidenceReference);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, evaluator.Evaluate(Context(13, late)).Result);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, evaluator.Evaluate(Context(15, early, late)).Result);
    }

    [Fact]
    public void UnobservableSourceAndForeignSessionCannotPass()
    {
        var futureSource = H4ContextFixture.Observation(fact: H4ContextFixture.Fact(contextOpen: H4ContextFixture.At(12)));
        var otherDay = H4ContextFixture.Observation(session: H4ContextFixture.Session(day: new(2026, 10, 4)));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, evaluator.Evaluate(Context(13, futureSource)).Result);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, evaluator.Evaluate(Context(13, otherDay)).Result);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, evaluator.Evaluate(Context(29, H4ContextFixture.Observation())).Result);
        Assert.Throws<ArgumentException>(() => Context(13, H4ContextFixture.Observation(session: H4ContextFixture.Session(provider: new("other")))));
        Assert.Throws<ArgumentException>(() => Context(13, H4ContextFixture.Observation(session: H4ContextFixture.Session(symbol: new("OTHER")))));
        Assert.Throws<ArgumentException>(() => Context(13, H4ContextFixture.Observation(session: H4ContextFixture.Session(version: new("other")))));
    }

    [Fact]
    public void RegistryUseCasePreservesDefinitionOrderRequiredFlagTimestampAndEvidence()
    {
        var context = Context(14, H4ContextFixture.Observation());
        var observation = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll())
            .Execute(H4ContextFixture.Definition, context);
        var definition = H4ContextFixture.Definition.Rules.Single(item => item.RuleId == evaluator.RuleId);
        var actual = Assert.Single(observation.Evaluations, item => item.RuleId == evaluator.RuleId);
        Assert.Equal(16, observation.Evaluations.Count);
        Assert.Equal(definition.DefinitionStatus, actual.DefinitionStatus);
        Assert.Equal(definition.IsRequired, actual.IsRequired);
        Assert.Equal(definition.Sequence, actual.Sequence);
        Assert.Equal(context.AsOfUtc, actual.EvaluatedAtUtc);
        Assert.Equal(RuleEvaluationResult.Passed, actual.Result);
        Assert.Equal(evaluator.Evaluate(context).EvidenceReference, actual.EvidenceReference);
        Assert.Equal(observation.Evaluations.Select(item => item.Sequence).OrderBy(item => item), observation.Evaluations.Select(item => item.Sequence));
    }

    [Fact]
    public void RegisteredHumanAdapterChangesOnlyH4CoverageNotDefinitionOrOtherCapabilities()
    {
        var evaluators = MoneyWayReplayRuleEvaluators.GetAll().Where(item => item.RuleId.Value != "NQ-LIQ-003").ToArray();
        Assert.IsType<MoneyWayNasdaqHumanH4ContextEvaluator>(Assert.Single(evaluators, item => item.RuleId == evaluator.RuleId));
        var declarations = MoneyWayReplayEvaluationCapabilityDeclarations.GetAll();
        Assert.DoesNotContain(declarations, item => item.RuleId == evaluator.RuleId);
        var before = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(),
            evaluators.Where(item => item.RuleId != evaluator.RuleId), declarations).Find(evaluator.StrategyId, evaluator.StrategyVersion)!;
        var after = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), evaluators, declarations)
            .Find(evaluator.StrategyId, evaluator.StrategyVersion)!;
        Assert.Equal(12, before.RequiredImplementedCount);
        Assert.Equal(2, before.RequiredEvaluatorGapCount);
        Assert.Equal(13, after.RequiredImplementedCount);
        Assert.Equal(1, after.RequiredEvaluatorGapCount);
        Assert.Equal(32, after.TotalRuleCount);
        Assert.Equal(14, after.RequiredRuleCount);
        Assert.False(after.HasFullRequiredEvaluatorRegistration);
        var h4 = after.Rules.Single(item => item.RuleId == evaluator.RuleId);
        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.Implemented, h4.CapabilityStatus);
        Assert.Equal(RuleDefinitionStatus.Confirmed, h4.DefinitionStatus);
        Assert.All(after.Rules.Where(item => item.RuleId != evaluator.RuleId), item =>
            Assert.Equal(before.Rules.Single(old => old.RuleId == item.RuleId).CapabilityStatus, item.CapabilityStatus));
    }

    [Fact]
    public void SameContextAndReorderedSupportProduceIdenticalDecisionsWithoutMutation()
    {
        var first = H4ContextFixture.Observation(source: "a");
        var second = H4ContextFixture.Observation(source: "b");
        var context = Context(13, second, first);
        var one = evaluator.Evaluate(context);
        var two = evaluator.Evaluate(context);
        var reversed = evaluator.Evaluate(Context(13, first, second));
        Assert.Equal(one.Result, two.Result);
        Assert.Equal(one.Reason, two.Reason);
        Assert.Equal(one.EvidenceReference, two.EvidenceReference);
        Assert.Equal(one.EvidenceReference, reversed.EvidenceReference);
        Assert.Same(second, context.InputObservations[0]);
        Assert.Same(first, context.InputObservations[1]);
    }

    [Fact]
    public void NullAndForeignStrategyContextsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate(null!));
        Assert.Throws<InvalidOperationException>(() => evaluator.Evaluate(Context(13, definition: MoneyWayForexStrategyDefinition.Instance)));
    }

    private static StrategyReplayContext Context(int hour, params IStrategyReplayInputObservation[] observations) => Context(hour, observations, H4ContextFixture.Definition);

    private static StrategyReplayContext Context(int hour, StrategyDefinition definition) => Context(hour, [], definition);

    private static StrategyReplayContext Context(int hour, IStrategyReplayInputObservation[] observations, StrategyDefinition definition)
    {
        var h4 = NasdaqHumanH4ContextObservation.H4;
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var cursor = new MultiTimeframeCandleReplayCursor([
            new CandleSeries(H4ContextFixture.Provider, H4ContextFixture.Symbol, h4,
                new[] { 0, 4, 8, 12 }.Select(open => new Candle(H4ContextFixture.Provider, H4ContextFixture.Symbol, h4,
                    H4ContextFixture.At(open), H4ContextFixture.At(open + 4), 100, 101, 99, 100, null))),
            new CandleSeries(H4ContextFixture.Provider, H4ContextFixture.Symbol, minute,
                [new(H4ContextFixture.Provider, H4ContextFixture.Symbol, minute, H4ContextFixture.At(hour).AddMinutes(-1),
                    H4ContextFixture.At(hour), 100, 101, 99, 100, null)]),
        ]);
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == H4ContextFixture.At(hour))
                return new CreateStrategyReplayContextUseCase().Execute(definition, frame, observations);
        throw new InvalidOperationException("Fixture frame missing.");
    }
}

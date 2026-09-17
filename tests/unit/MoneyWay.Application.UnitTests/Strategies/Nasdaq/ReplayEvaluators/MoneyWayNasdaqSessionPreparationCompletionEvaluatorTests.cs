using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayEvaluators;

public sealed class MoneyWayNasdaqSessionPreparationCompletionEvaluatorTests
{
    private static readonly StrategyDefinition Definition = MoneyWayNasdaqStrategyDefinition.Instance;
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NQ");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateOnly Day = new(2026, 9, 17);
    private static readonly DateTimeOffset Start = new(2026, 9, 17, 13, 0, 0, TimeSpan.Zero);
    private readonly MoneyWayNasdaqSessionPreparationCompletionEvaluator evaluator = new();

    [Fact]
    public void IdentityMatchesOnlyTheRequiredConfirmedPreparationRule()
    {
        var rule = Definition.Rules.Single(item => item.RuleId.Value == "NQ-TIME-003");
        Assert.Equal(Definition.StrategyId, evaluator.StrategyId);
        Assert.Equal(Definition.Version, evaluator.StrategyVersion);
        Assert.Equal(rule.RuleId, evaluator.RuleId);
        Assert.True(rule.IsRequired);
        Assert.Equal(RuleDefinitionStatus.Confirmed, rule.DefinitionStatus);
        Assert.Equal(5, rule.Sequence);
    }

    [Theory]
    [InlineData(-1, 0, RuleEvaluationResult.NotApplicable)]
    [InlineData(0, 0, RuleEvaluationResult.Waiting)]
    [InlineData(10, 0, RuleEvaluationResult.Waiting)]
    [InlineData(29, 59999, RuleEvaluationResult.Waiting)]
    [InlineData(30, 0, RuleEvaluationResult.Failed)]
    [InlineData(60, 0, RuleEvaluationResult.Failed)]
    public void MissingCompletionFollowsExactBogotaBoundaries(int minutes, int milliseconds, RuleEvaluationResult expected)
    {
        var decision = evaluator.Evaluate(ContextAt(Start.AddMinutes(minutes).AddMilliseconds(milliseconds)));
        Assert.Equal(expected, decision.Result);
        Assert.Null(decision.EvidenceReference);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, RuleEvaluationResult.Passed)]
    [InlineData(10, 0, 20, 0, RuleEvaluationResult.Waiting)]
    [InlineData(20, 0, 20, 0, RuleEvaluationResult.Passed)]
    [InlineData(29, 59999, 30, 0, RuleEvaluationResult.Waiting)]
    [InlineData(30, 0, 29, 59999, RuleEvaluationResult.Passed)]
    [InlineData(30, 0, 30, 0, RuleEvaluationResult.Failed)]
    [InlineData(60, 0, 20, 0, RuleEvaluationResult.Passed)]
    [InlineData(60, 0, 31, 0, RuleEvaluationResult.Failed)]
    public void VisibleCompletionPassesOnlyWhenItsTimestampIsInsideTheWindow(
        int asOfMinutes, int asOfMilliseconds, int completionMinutes, int completionMilliseconds, RuleEvaluationResult expected)
    {
        var completion = Observation(Day, Start.AddMinutes(completionMinutes).AddMilliseconds(completionMilliseconds));
        var decision = evaluator.Evaluate(ContextAt(Start.AddMinutes(asOfMinutes).AddMilliseconds(asOfMilliseconds), completion));
        Assert.Equal(expected, decision.Result);
        Assert.Equal(completion.ObservedAtUtc <= Start.AddMinutes(asOfMinutes).AddMilliseconds(asOfMilliseconds)
            ? completion.SourceReference : null, decision.EvidenceReference);
    }

    [Fact]
    public void PreWindowEvidenceNeverQualifiesAndLaterEvidenceCannotRecoverOrRewriteAnEarlierFrame()
    {
        var early = Observation(Day, Start.AddMinutes(-1));
        Assert.Equal(RuleEvaluationResult.NotApplicable, evaluator.Evaluate(ContextAt(Start.AddSeconds(-1), early)).Result);
        Assert.Equal(RuleEvaluationResult.Waiting, evaluator.Evaluate(ContextAt(Start.AddMinutes(10), early)).Result);
        Assert.Equal(RuleEvaluationResult.Failed, evaluator.Evaluate(ContextAt(Start.AddMinutes(30), early)).Result);

        var late = Observation(Day, Start.AddMinutes(31));
        var atDeadline = evaluator.Evaluate(ContextAt(Start.AddMinutes(30), late));
        Assert.Equal(RuleEvaluationResult.Failed, atDeadline.Result);
        Assert.Equal(RuleEvaluationResult.Failed, evaluator.Evaluate(ContextAt(Start.AddMinutes(35), late)).Result);
        Assert.Equal(atDeadline.Result, evaluator.Evaluate(ContextAt(Start.AddMinutes(30), late)).Result);
    }

    [Fact]
    public void SameSessionLookupRejectsOtherDaysAndContextRejectsOtherReplayIdentity()
    {
        var yesterday = Observation(Day.AddDays(-1), Start.AddDays(-1).AddMinutes(20));
        var tomorrow = Observation(Day.AddDays(1), Start.AddDays(1).AddMinutes(20));
        Assert.Equal(RuleEvaluationResult.Waiting, evaluator.Evaluate(ContextAt(Start.AddMinutes(10), yesterday, tomorrow)).Result);
        Assert.Equal(RuleEvaluationResult.Failed, evaluator.Evaluate(ContextAt(Start.AddMinutes(30), yesterday, tomorrow)).Result);

        var matching = Observation(Day, Start.AddMinutes(20));
        Assert.Equal(RuleEvaluationResult.Passed, evaluator.Evaluate(ContextAt(Start.AddMinutes(20), yesterday, matching)).Result);
        var otherSymbol = new NasdaqPreparationCompletionObservation(Definition.StrategyId, Definition.Version,
            Provider, new("OTHER"), Day, matching.ObservedAtUtc, "other:symbol");
        Assert.Throws<ArgumentException>(() => ContextAt(Start.AddMinutes(30), otherSymbol));
        var otherVersion = new NasdaqPreparationCompletionObservation(Definition.StrategyId, new("other-version"),
            Provider, Symbol, Day, matching.ObservedAtUtc, "other:version");
        Assert.Throws<ArgumentException>(() => ContextAt(Start.AddMinutes(30), otherVersion));
        Assert.Throws<ArgumentException>(() => new NasdaqPreparationCompletionObservation(
            new("moneyway-forex"), Definition.Version, Provider, Symbol, Day, matching.ObservedAtUtc, "other:strategy"));
    }

    [Fact]
    public void FutureEvidenceDoesNotChangeEarlierResultAndEvaluationIsRepeatable()
    {
        var completion = Observation(Day, Start.AddMinutes(20));
        var earlier = ContextAt(Start.AddMinutes(10), completion);
        var later = ContextAt(Start.AddMinutes(20), completion);
        var noEvidence = evaluator.Evaluate(ContextAt(Start.AddMinutes(10)));
        var before = evaluator.Evaluate(earlier);
        var at = evaluator.Evaluate(later);

        Assert.Empty(earlier.InputObservations);
        Assert.Equal(noEvidence.Result, before.Result);
        Assert.Equal(noEvidence.Reason, before.Reason);
        Assert.Equal(RuleEvaluationResult.Waiting, before.Result);
        Assert.Equal(RuleEvaluationResult.Passed, at.Result);
        Assert.Equal(completion.SourceReference, at.EvidenceReference);
        Assert.Equal(before.Result, evaluator.Evaluate(earlier).Result);
        Assert.Equal(at.Reason, evaluator.Evaluate(later).Reason);
    }

    [Fact]
    public void BogotaBoundaryRemainsEightOClockInJuly()
    {
        Assert.Equal(RuleEvaluationResult.NotApplicable,
            evaluator.Evaluate(ContextAt(new(2026, 7, 15, 12, 59, 59, TimeSpan.Zero))).Result);
        Assert.Equal(RuleEvaluationResult.Waiting,
            evaluator.Evaluate(ContextAt(new(2026, 7, 15, 13, 0, 0, TimeSpan.Zero))).Result);
    }

    [Fact]
    public void CanonicalOrchestrationPreservesRuleMetadataAndEvaluationTimestamp()
    {
        var at = Start.AddMinutes(20);
        var context = ContextAt(at, Observation(Day, at));
        var observation = new EvaluateStrategyReplayContextUseCase([evaluator]).Execute(Definition, context);
        var result = Assert.Single(observation.Evaluations);
        Assert.Equal(evaluator.RuleId, result.RuleId);
        Assert.Equal(RuleDefinitionStatus.Confirmed, result.DefinitionStatus);
        Assert.True(result.IsRequired);
        Assert.Equal(5, result.Sequence);
        Assert.Equal(at, result.EvaluatedAtUtc);
        Assert.Equal(RuleEvaluationResult.Passed, result.Result);
        Assert.Null(observation.WorkflowProgression);
    }

    private static StrategyReplayContext ContextAt(DateTimeOffset at, params NasdaqPreparationCompletionObservation[] observations)
    {
        var series = new CandleSeries(Provider, Symbol, Minute,
            [new Candle(Provider, Symbol, Minute, at.AddMinutes(-1), at, 100, 101, 99, 100, null)]);
        var cursor = new MultiTimeframeCandleReplayCursor([series]);
        Assert.True(cursor.TryAdvance(out var frame));
        var preparation = new NasdaqPreparationCompletionObservationSeries(
            Definition.StrategyId, Definition.Version, Provider, Symbol, observations);
        return new CreateStrategyReplayContextUseCase().Execute(Definition, frame!, preparation);
    }

    private static NasdaqPreparationCompletionObservation Observation(DateOnly day, DateTimeOffset at) =>
        new(Definition.StrategyId, Definition.Version, Provider, Symbol, day, at, "review:prepared");
}

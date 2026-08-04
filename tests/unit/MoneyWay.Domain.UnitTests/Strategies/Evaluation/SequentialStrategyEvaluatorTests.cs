using MoneyWay.Domain.Strategies;
using MoneyWay.Domain.Strategies.Evaluation;

namespace MoneyWay.Domain.UnitTests.Strategies.Evaluation;

public sealed class SequentialStrategyEvaluatorTests
{
    private static readonly DateTimeOffset EvaluatedAtUtc = new(2026, 8, 3, 15, 30, 0, TimeSpan.Zero);
    private readonly SequentialStrategyEvaluator evaluator = new();

    [Fact]
    public void NullCollectionIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate(null!));
    }

    [Fact]
    public void NullElementIsRejected()
    {
        Assert.Throws<ArgumentException>(() => evaluator.Evaluate([null!]));
    }

    [Fact]
    public void DuplicateSequenceIsRejected()
    {
        var evaluations = new[]
        {
            CreateEvaluation("RULE-001", 10),
            CreateEvaluation("RULE-002", 10),
        };

        Assert.Throws<ArgumentException>(() => evaluator.Evaluate(evaluations));
    }

    [Fact]
    public void EmptyCollectionReturnsDataUnavailable()
    {
        var outcome = evaluator.Evaluate([]);

        Assert.Equal(StrategyVerdict.DataUnavailable, outcome.Verdict);
        Assert.Null(outcome.BlockingRuleId);
        Assert.Null(outcome.BlockingSequence);
        Assert.Null(outcome.BlockingResult);
        Assert.Equal("No rule evaluations were supplied.", outcome.Reason);
        Assert.Equal(0, outcome.EvaluationCount);
        Assert.Equal(0, outcome.RequiredEvaluationCount);
    }

    [Fact]
    public void SequenceGapsAreAccepted()
    {
        var outcome = evaluator.Evaluate(
            [CreateEvaluation("RULE-001", 1), CreateEvaluation("RULE-005", 5), CreateEvaluation("RULE-050", 50)]);

        Assert.Equal(StrategyVerdict.Ready, outcome.Verdict);
    }

    [Theory]
    [InlineData(RuleEvaluationResult.Passed)]
    [InlineData(RuleEvaluationResult.NotApplicable)]
    public void NonBlockingRequiredResultReturnsReady(RuleEvaluationResult result)
    {
        var outcome = evaluator.Evaluate([CreateEvaluation("RULE-001", 1, result)]);

        Assert.Equal(StrategyVerdict.Ready, outcome.Verdict);
        Assert.Null(outcome.BlockingRuleId);
        Assert.Equal("All required rule evaluations passed or were not applicable.", outcome.Reason);
    }

    [Fact]
    public void SeveralPassedRequiredRulesReturnReady()
    {
        var outcome = evaluator.Evaluate(
            [CreateEvaluation("RULE-001", 1), CreateEvaluation("RULE-002", 2), CreateEvaluation("RULE-003", 3)]);

        Assert.Equal(StrategyVerdict.Ready, outcome.Verdict);
    }

    [Fact]
    public void PassedAndNotApplicableRequiredRulesReturnReady()
    {
        var outcome = evaluator.Evaluate(
            [CreateEvaluation("RULE-001", 1), CreateEvaluation("RULE-002", 2, RuleEvaluationResult.NotApplicable)]);

        Assert.Equal(StrategyVerdict.Ready, outcome.Verdict);
    }

    [Fact]
    public void OnlyOptionalRulesReturnReady()
    {
        var outcome = evaluator.Evaluate(
            [CreateEvaluation("RULE-001", 1, RuleEvaluationResult.Failed, false), CreateEvaluation("RULE-002", 2, RuleEvaluationResult.Waiting, false)]);

        Assert.Equal(StrategyVerdict.Ready, outcome.Verdict);
        Assert.Equal(0, outcome.RequiredEvaluationCount);
    }

    [Theory]
    [InlineData(RuleEvaluationResult.Waiting, StrategyVerdict.Wait)]
    [InlineData(RuleEvaluationResult.Failed, StrategyVerdict.NoTrade)]
    [InlineData(RuleEvaluationResult.HumanValidationRequired, StrategyVerdict.HumanValidationRequired)]
    [InlineData(RuleEvaluationResult.DataUnavailable, StrategyVerdict.DataUnavailable)]
    public void RequiredBlockingResultMapsToExactVerdict(
        RuleEvaluationResult result,
        StrategyVerdict expectedVerdict)
    {
        var outcome = evaluator.Evaluate([CreateEvaluation("RULE-001", 1, result)]);

        Assert.Equal(expectedVerdict, outcome.Verdict);
        Assert.Equal(result, outcome.BlockingResult);
    }

    [Theory]
    [InlineData(RuleEvaluationResult.Failed)]
    [InlineData(RuleEvaluationResult.Waiting)]
    [InlineData(RuleEvaluationResult.HumanValidationRequired)]
    [InlineData(RuleEvaluationResult.DataUnavailable)]
    public void OptionalBlockingResultDoesNotBlock(RuleEvaluationResult result)
    {
        var outcome = evaluator.Evaluate([CreateEvaluation("RULE-001", 1, result, false)]);

        Assert.Equal(StrategyVerdict.Ready, outcome.Verdict);
    }

    [Fact]
    public void OptionalBlockerBeforeRequiredPassedReturnsReady()
    {
        var outcome = evaluator.Evaluate(
            [CreateEvaluation("OPTIONAL", 1, RuleEvaluationResult.Failed, false), CreateEvaluation("REQUIRED", 2)]);

        Assert.Equal(StrategyVerdict.Ready, outcome.Verdict);
    }

    [Fact]
    public void OptionalBlockerBeforeRequiredFailedUsesRequiredRule()
    {
        var outcome = evaluator.Evaluate(
            [CreateEvaluation("OPTIONAL", 1, RuleEvaluationResult.Waiting, false), CreateEvaluation("REQUIRED", 2, RuleEvaluationResult.Failed)]);

        Assert.Equal(StrategyVerdict.NoTrade, outcome.Verdict);
        Assert.Equal(new RuleId("REQUIRED"), outcome.BlockingRuleId);
    }

    [Fact]
    public void InputIsEvaluatedBySequenceRatherThanReceivedOrder()
    {
        var outcome = evaluator.Evaluate(
            [CreateEvaluation("THIRTY", 30, RuleEvaluationResult.Failed), CreateEvaluation("TEN", 10), CreateEvaluation("TWENTY", 20, RuleEvaluationResult.Waiting)]);

        Assert.Equal(StrategyVerdict.Wait, outcome.Verdict);
        Assert.Equal(new RuleId("TWENTY"), outcome.BlockingRuleId);
        Assert.Equal(20, outcome.BlockingSequence);
    }

    [Theory]
    [InlineData(RuleEvaluationResult.Waiting, RuleEvaluationResult.Failed, StrategyVerdict.Wait)]
    [InlineData(RuleEvaluationResult.Failed, RuleEvaluationResult.Waiting, StrategyVerdict.NoTrade)]
    [InlineData(RuleEvaluationResult.HumanValidationRequired, RuleEvaluationResult.DataUnavailable, StrategyVerdict.HumanValidationRequired)]
    [InlineData(RuleEvaluationResult.DataUnavailable, RuleEvaluationResult.HumanValidationRequired, StrategyVerdict.DataUnavailable)]
    public void FirstRequiredBlockerDeterminesVerdict(
        RuleEvaluationResult firstResult,
        RuleEvaluationResult secondResult,
        StrategyVerdict expectedVerdict)
    {
        var outcome = evaluator.Evaluate(
            [CreateEvaluation("SECOND", 20, secondResult), CreateEvaluation("FIRST", 10, firstResult)]);

        Assert.Equal(expectedVerdict, outcome.Verdict);
        Assert.Equal(new RuleId("FIRST"), outcome.BlockingRuleId);
        Assert.Equal(10, outcome.BlockingSequence);
        Assert.Equal(firstResult, outcome.BlockingResult);
    }

    [Fact]
    public void BlockingReasonIsPreservedExactly()
    {
        const string reason = "Original audited explanation.";

        var outcome = evaluator.Evaluate(
            [CreateEvaluation("RULE-001", 1, RuleEvaluationResult.HumanValidationRequired, reason: reason)]);

        Assert.Equal(reason, outcome.Reason);
    }

    [Fact]
    public void CountsIncludeRequiredAndOptionalRules()
    {
        var outcome = evaluator.Evaluate(
            [CreateEvaluation("OPTIONAL", 1, isRequired: false), CreateEvaluation("REQUIRED-1", 2), CreateEvaluation("REQUIRED-2", 3)]);

        Assert.Equal(3, outcome.EvaluationCount);
        Assert.Equal(2, outcome.RequiredEvaluationCount);
    }

    [Fact]
    public void CountsIncludeRulesAfterEarlyBlocker()
    {
        var outcome = evaluator.Evaluate(
            [CreateEvaluation("BLOCKER", 1, RuleEvaluationResult.Waiting), CreateEvaluation("LATER-REQUIRED", 2), CreateEvaluation("LATER-OPTIONAL", 3, isRequired: false)]);

        Assert.Equal(3, outcome.EvaluationCount);
        Assert.Equal(2, outcome.RequiredEvaluationCount);
    }

    [Fact]
    public void DifferentInputOrdersProduceEquivalentOutcome()
    {
        var first = CreateEvaluation("FIRST", 10);
        var blocker = CreateEvaluation("BLOCKER", 20, RuleEvaluationResult.Failed);
        var last = CreateEvaluation("LAST", 30, RuleEvaluationResult.Waiting);

        var ordered = evaluator.Evaluate([first, blocker, last]);
        var shuffled = evaluator.Evaluate([last, first, blocker]);

        Assert.Equal(ordered, shuffled);
    }

    [Fact]
    public void RepeatedEvaluationProducesEquivalentOutcome()
    {
        var evaluations = new[]
        {
            CreateEvaluation("RULE-001", 1),
            CreateEvaluation("RULE-002", 2, RuleEvaluationResult.DataUnavailable),
        };

        Assert.Equal(evaluator.Evaluate(evaluations), evaluator.Evaluate(evaluations));
    }

    [Fact]
    public void DefinitionStatusDoesNotAlterResultMapping()
    {
        var evaluation = CreateEvaluation(
            "RULE-001",
            1,
            RuleEvaluationResult.Passed,
            definitionStatus: RuleDefinitionStatus.Unresolved);

        Assert.Equal(StrategyVerdict.Ready, evaluator.Evaluate([evaluation]).Verdict);
    }

    [Fact]
    public void InputEnumerableIsMaterializedOnce()
    {
        var enumerationCount = 0;

        IEnumerable<RuleEvaluation> EnumerateOnce()
        {
            enumerationCount++;
            yield return CreateEvaluation("RULE-001", 1);
            yield return CreateEvaluation("RULE-002", 2);
        }

        evaluator.Evaluate(EnumerateOnce());

        Assert.Equal(1, enumerationCount);
    }

    private static RuleEvaluation CreateEvaluation(
        string ruleId,
        int sequence,
        RuleEvaluationResult result = RuleEvaluationResult.Passed,
        bool isRequired = true,
        string? reason = null,
        RuleDefinitionStatus definitionStatus = RuleDefinitionStatus.Confirmed)
    {
        return new RuleEvaluation(
            new RuleId(ruleId),
            definitionStatus,
            result,
            sequence,
            isRequired,
            reason ?? $"Reason for {ruleId}.",
            EvaluatedAtUtc,
            null);
    }
}

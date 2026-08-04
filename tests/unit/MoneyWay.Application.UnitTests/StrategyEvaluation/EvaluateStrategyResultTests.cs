using MoneyWay.Application.StrategyEvaluation;
using MoneyWay.Domain.Strategies;
using MoneyWay.Domain.Strategies.Evaluation;

namespace MoneyWay.Application.UnitTests.StrategyEvaluation;

public sealed class EvaluateStrategyResultTests
{
    private static readonly StrategyId StrategyId = new("strategy-one");
    private static readonly StrategyVersion StrategyVersion = new("strategy-one-0.1-draft");

    [Fact]
    public void ReadyResultPreservesValuesAndOrderedEvaluations()
    {
        var evaluation = CreateEvaluation("RULE-001", 1);
        var outcome = CreateReadyOutcome(1, 1);

        var result = new EvaluateStrategyResult(StrategyId, StrategyVersion, outcome, [evaluation]);

        Assert.Same(StrategyId, result.StrategyId);
        Assert.Same(StrategyVersion, result.StrategyVersion);
        Assert.Same(outcome, result.Outcome);
        Assert.Equal([evaluation], result.Evaluations);
    }

    [Fact]
    public void EmptyDataUnavailableResultIsValid()
    {
        var result = new EvaluateStrategyResult(
            StrategyId,
            StrategyVersion,
            new StrategyEvaluationOutcome(
                StrategyVerdict.DataUnavailable, null, null, null, "No rule evaluations were supplied.", 0, 0),
            []);

        Assert.Empty(result.Evaluations);
        Assert.Equal(StrategyVerdict.DataUnavailable, result.Outcome.Verdict);
    }

    [Fact]
    public void BlockingResultIsValid()
    {
        var blocker = CreateEvaluation("RULE-002", 2, RuleEvaluationResult.Waiting, reason: "Evidence is pending.");
        var result = new EvaluateStrategyResult(
            StrategyId, StrategyVersion, CreateBlockingOutcome(blocker, 1, 1), [blocker]);

        Assert.Equal(blocker.RuleId, result.Outcome.BlockingRuleId);
    }

    [Fact]
    public void NullStrategyIdIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new EvaluateStrategyResult(
            null!, StrategyVersion, CreateReadyOutcome(1, 1), [CreateEvaluation("RULE", 1)]));
    }

    [Fact]
    public void NullStrategyVersionIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new EvaluateStrategyResult(
            StrategyId, null!, CreateReadyOutcome(1, 1), [CreateEvaluation("RULE", 1)]));
    }

    [Fact]
    public void NullOutcomeIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new EvaluateStrategyResult(StrategyId, StrategyVersion, null!, []));
    }

    [Fact]
    public void NullEvaluationsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new EvaluateStrategyResult(
            StrategyId, StrategyVersion, CreateReadyOutcome(1, 1), null!));
    }

    [Fact]
    public void NullEvaluationElementIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new EvaluateStrategyResult(
            StrategyId, StrategyVersion, CreateReadyOutcome(1, 1), [null!]));
    }

    [Fact]
    public void OutOfOrderEvaluationsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new EvaluateStrategyResult(
            StrategyId,
            StrategyVersion,
            CreateReadyOutcome(2, 2),
            [CreateEvaluation("LATER", 20), CreateEvaluation("EARLIER", 10)]));
    }

    [Fact]
    public void DuplicateSequenceIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new EvaluateStrategyResult(
            StrategyId,
            StrategyVersion,
            CreateReadyOutcome(2, 2),
            [CreateEvaluation("FIRST", 10), CreateEvaluation("SECOND", 10)]));
    }

    [Fact]
    public void InconsistentEvaluationCountIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new EvaluateStrategyResult(
            StrategyId, StrategyVersion, CreateReadyOutcome(2, 1), [CreateEvaluation("RULE", 1)]));
    }

    [Fact]
    public void InconsistentRequiredEvaluationCountIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new EvaluateStrategyResult(
            StrategyId,
            StrategyVersion,
            CreateReadyOutcome(1, 0),
            [CreateEvaluation("RULE", 1, isRequired: true)]));
    }

    [Fact]
    public void MissingBlockingRuleIsRejected()
    {
        var evaluation = CreateEvaluation("RULE-001", 1, RuleEvaluationResult.Waiting);
        var otherBlocker = CreateEvaluation("RULE-OTHER", 1, RuleEvaluationResult.Waiting);

        Assert.Throws<ArgumentException>(() => new EvaluateStrategyResult(
            StrategyId, StrategyVersion, CreateBlockingOutcome(otherBlocker, 1, 1), [evaluation]));
    }

    [Fact]
    public void MismatchedBlockingSequenceIsRejected()
    {
        var evaluation = CreateEvaluation("RULE-001", 1, RuleEvaluationResult.Waiting);
        var wrongSequence = CreateEvaluation("RULE-001", 2, RuleEvaluationResult.Waiting);

        Assert.Throws<ArgumentException>(() => new EvaluateStrategyResult(
            StrategyId, StrategyVersion, CreateBlockingOutcome(wrongSequence, 1, 1), [evaluation]));
    }

    [Fact]
    public void MismatchedBlockingResultIsRejected()
    {
        var evaluation = CreateEvaluation("RULE-001", 1, RuleEvaluationResult.Failed, reason: "Blocked.");
        var outcomeSource = CreateEvaluation("RULE-001", 1, RuleEvaluationResult.Waiting, reason: "Blocked.");

        Assert.Throws<ArgumentException>(() => new EvaluateStrategyResult(
            StrategyId, StrategyVersion, CreateBlockingOutcome(outcomeSource, 1, 1), [evaluation]));
    }

    [Fact]
    public void OptionalBlockingRuleIsRejected()
    {
        var optionalBlocker = CreateEvaluation("RULE-001", 1, RuleEvaluationResult.Waiting, false);
        var requiredEvaluation = CreateEvaluation("RULE-002", 2);

        Assert.Throws<ArgumentException>(() => new EvaluateStrategyResult(
            StrategyId,
            StrategyVersion,
            CreateBlockingOutcome(optionalBlocker, 2, 1),
            [optionalBlocker, requiredEvaluation]));
    }

    [Fact]
    public void MismatchedBlockingReasonIsRejected()
    {
        var evaluation = CreateEvaluation("RULE-001", 1, RuleEvaluationResult.Waiting, reason: "Actual reason.");
        var outcomeSource = CreateEvaluation("RULE-001", 1, RuleEvaluationResult.Waiting, reason: "Different reason.");

        Assert.Throws<ArgumentException>(() => new EvaluateStrategyResult(
            StrategyId, StrategyVersion, CreateBlockingOutcome(outcomeSource, 1, 1), [evaluation]));
    }

    [Fact]
    public void OriginalListChangesDoNotAlterSnapshot()
    {
        var evaluation = CreateEvaluation("RULE-001", 1);
        var source = new List<RuleEvaluation> { evaluation };
        var result = new EvaluateStrategyResult(
            StrategyId, StrategyVersion, CreateReadyOutcome(1, 1), source);

        source.Clear();

        Assert.Equal([evaluation], result.Evaluations);
    }

    [Fact]
    public void ExposedCollectionCannotBeModified()
    {
        var evaluation = CreateEvaluation("RULE-001", 1);
        var result = new EvaluateStrategyResult(
            StrategyId, StrategyVersion, CreateReadyOutcome(1, 1), [evaluation]);
        var collection = Assert.IsAssignableFrom<ICollection<RuleEvaluation>>(result.Evaluations);

        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Clear());
    }

    private static StrategyEvaluationOutcome CreateReadyOutcome(int evaluationCount, int requiredCount)
    {
        return new StrategyEvaluationOutcome(
            StrategyVerdict.Ready,
            null,
            null,
            null,
            "All required rule evaluations passed or were not applicable.",
            evaluationCount,
            requiredCount);
    }

    private static StrategyEvaluationOutcome CreateBlockingOutcome(
        RuleEvaluation evaluation,
        int evaluationCount,
        int requiredCount)
    {
        return new StrategyEvaluationOutcome(
            evaluation.Result == RuleEvaluationResult.Waiting ? StrategyVerdict.Wait : StrategyVerdict.NoTrade,
            evaluation.RuleId,
            evaluation.Sequence,
            evaluation.Result,
            evaluation.Reason,
            evaluationCount,
            requiredCount);
    }

    private static RuleEvaluation CreateEvaluation(
        string ruleId,
        int sequence,
        RuleEvaluationResult result = RuleEvaluationResult.Passed,
        bool isRequired = true,
        string? reason = null)
    {
        return new RuleEvaluation(
            new RuleId(ruleId),
            RuleDefinitionStatus.Confirmed,
            result,
            sequence,
            isRequired,
            reason ?? $"Reason for {ruleId}.",
            new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero),
            "evidence-reference");
    }
}

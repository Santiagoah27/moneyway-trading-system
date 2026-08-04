using MoneyWay.Domain.Strategies;
using MoneyWay.Domain.Strategies.Evaluation;

namespace MoneyWay.Domain.UnitTests.Strategies.Evaluation;

public sealed class StrategyEvaluationOutcomeTests
{
    private static readonly RuleId BlockingRuleId = new("RULE-002");

    [Fact]
    public void ReadyOutcomePreservesValidValues()
    {
        var outcome = CreateReady(evaluationCount: 3, requiredEvaluationCount: 2);

        Assert.Equal(StrategyVerdict.Ready, outcome.Verdict);
        Assert.Null(outcome.BlockingRuleId);
        Assert.Null(outcome.BlockingSequence);
        Assert.Null(outcome.BlockingResult);
        Assert.Equal("All required rule evaluations passed or were not applicable.", outcome.Reason);
        Assert.Equal(3, outcome.EvaluationCount);
        Assert.Equal(2, outcome.RequiredEvaluationCount);
    }

    [Fact]
    public void EmptyDataUnavailableOutcomeIsValid()
    {
        var outcome = new StrategyEvaluationOutcome(
            StrategyVerdict.DataUnavailable, null, null, null, "No rule evaluations were supplied.", 0, 0);

        Assert.Equal(StrategyVerdict.DataUnavailable, outcome.Verdict);
        Assert.Null(outcome.BlockingRuleId);
        Assert.Equal(0, outcome.EvaluationCount);
        Assert.Equal(0, outcome.RequiredEvaluationCount);
    }

    [Fact]
    public void BlockingOutcomePreservesValidValues()
    {
        var outcome = CreateBlocking();

        Assert.Equal(StrategyVerdict.Wait, outcome.Verdict);
        Assert.Equal(BlockingRuleId, outcome.BlockingRuleId);
        Assert.Equal(2, outcome.BlockingSequence);
        Assert.Equal(RuleEvaluationResult.Waiting, outcome.BlockingResult);
        Assert.Equal("Required evidence is pending.", outcome.Reason);
        Assert.Equal(4, outcome.EvaluationCount);
        Assert.Equal(3, outcome.RequiredEvaluationCount);
    }

    [Fact]
    public void ReadyRejectsBlockingRuleId()
    {
        Assert.Throws<ArgumentException>(() => CreateReady(blockingRuleId: BlockingRuleId));
    }

    [Fact]
    public void ReadyRejectsBlockingSequence()
    {
        Assert.Throws<ArgumentException>(() => CreateReady(blockingSequence: 1));
    }

    [Fact]
    public void ReadyRejectsBlockingResult()
    {
        Assert.Throws<ArgumentException>(() => CreateReady(blockingResult: RuleEvaluationResult.Failed));
    }

    [Fact]
    public void ReadyRejectsZeroEvaluationCount()
    {
        Assert.Throws<ArgumentException>(() => CreateReady(evaluationCount: 0));
    }

    [Fact]
    public void NegativeEvaluationCountIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateReady(evaluationCount: -1));
    }

    [Fact]
    public void NegativeRequiredEvaluationCountIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateReady(requiredEvaluationCount: -1));
    }

    [Fact]
    public void RequiredCountAboveTotalIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateReady(evaluationCount: 1, requiredEvaluationCount: 2));
    }

    [Fact]
    public void BlockingOutcomeRequiresRuleId()
    {
        Assert.Throws<ArgumentException>(() => new StrategyEvaluationOutcome(
            StrategyVerdict.Wait,
            null,
            2,
            RuleEvaluationResult.Waiting,
            "Required evidence is pending.",
            4,
            3));
    }

    [Fact]
    public void BlockingOutcomeRequiresSequence()
    {
        Assert.Throws<ArgumentException>(() => CreateBlocking(blockingSequence: null));
    }

    [Fact]
    public void BlockingOutcomeRequiresResult()
    {
        Assert.Throws<ArgumentException>(() => CreateBlocking(blockingResult: null));
    }

    [Fact]
    public void BlockingOutcomeRequiresRequiredEvaluation()
    {
        Assert.Throws<ArgumentException>(() => CreateBlocking(requiredEvaluationCount: 0));
    }

    [Fact]
    public void ZeroBlockingSequenceIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateBlocking(blockingSequence: 0));
    }

    [Fact]
    public void NullReasonIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => CreateReady(reason: null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" reason")]
    [InlineData("reason ")]
    public void InvalidReasonIsRejected(string reason)
    {
        Assert.Throws<ArgumentException>(() => CreateReady(reason: reason));
    }

    private static StrategyEvaluationOutcome CreateReady(
        RuleId? blockingRuleId = null,
        int? blockingSequence = null,
        RuleEvaluationResult? blockingResult = null,
        string reason = "All required rule evaluations passed or were not applicable.",
        int evaluationCount = 1,
        int requiredEvaluationCount = 0)
    {
        return new StrategyEvaluationOutcome(
            StrategyVerdict.Ready,
            blockingRuleId,
            blockingSequence,
            blockingResult,
            reason,
            evaluationCount,
            requiredEvaluationCount);
    }

    private static StrategyEvaluationOutcome CreateBlocking(
        RuleId? blockingRuleId = null,
        int? blockingSequence = 2,
        RuleEvaluationResult? blockingResult = RuleEvaluationResult.Waiting,
        int requiredEvaluationCount = 3)
    {
        return new StrategyEvaluationOutcome(
            StrategyVerdict.Wait,
            blockingRuleId ?? BlockingRuleId,
            blockingSequence,
            blockingResult,
            "Required evidence is pending.",
            4,
            requiredEvaluationCount);
    }
}

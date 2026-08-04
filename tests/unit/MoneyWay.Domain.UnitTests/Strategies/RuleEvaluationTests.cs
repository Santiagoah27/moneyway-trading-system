using MoneyWay.Domain.Strategies;

namespace MoneyWay.Domain.UnitTests.Strategies;

public sealed class RuleEvaluationTests
{
    private static readonly DateTimeOffset EvaluatedAtUtc = new(2026, 8, 3, 15, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ValidValuesArePreserved()
    {
        var ruleId = new RuleId("FX-W-001");

        var evaluation = CreateEvaluation(
            ruleId: ruleId,
            definitionStatus: RuleDefinitionStatus.Confirmed,
            result: RuleEvaluationResult.Passed,
            sequence: 2,
            isRequired: true,
            reason: "Condition was satisfied.",
            evaluatedAtUtc: EvaluatedAtUtc,
            evidenceReference: "video-01@50:15");

        Assert.Same(ruleId, evaluation.RuleId);
        Assert.Equal(RuleDefinitionStatus.Confirmed, evaluation.DefinitionStatus);
        Assert.Equal(RuleEvaluationResult.Passed, evaluation.Result);
        Assert.Equal(2, evaluation.Sequence);
        Assert.True(evaluation.IsRequired);
        Assert.Equal("Condition was satisfied.", evaluation.Reason);
        Assert.Equal(EvaluatedAtUtc, evaluation.EvaluatedAtUtc);
        Assert.Equal("video-01@50:15", evaluation.EvidenceReference);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveSequenceIsRejected(int sequence)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateEvaluation(sequence: sequence));
    }

    [Fact]
    public void NullReasonIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => CreateEvaluation(reason: null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" explanation")]
    [InlineData("explanation ")]
    public void InvalidReasonIsRejected(string reason)
    {
        Assert.Throws<ArgumentException>(() => CreateEvaluation(reason: reason));
    }

    [Fact]
    public void UtcTimestampIsAccepted()
    {
        var evaluation = CreateEvaluation(evaluatedAtUtc: EvaluatedAtUtc);

        Assert.Equal(TimeSpan.Zero, evaluation.EvaluatedAtUtc.Offset);
    }

    [Fact]
    public void NonUtcTimestampIsRejected()
    {
        var nonUtcTimestamp = new DateTimeOffset(2026, 8, 3, 10, 30, 0, TimeSpan.FromHours(-5));

        Assert.Throws<ArgumentException>(() => CreateEvaluation(evaluatedAtUtc: nonUtcTimestamp));
    }

    [Fact]
    public void NullEvidenceReferenceIsAccepted()
    {
        var evaluation = CreateEvaluation(evidenceReference: null);

        Assert.Null(evaluation.EvidenceReference);
    }

    [Fact]
    public void ValidEvidenceReferenceIsPreserved()
    {
        var evaluation = CreateEvaluation(evidenceReference: "candle-set-2026-08-03");

        Assert.Equal("candle-set-2026-08-03", evaluation.EvidenceReference);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" evidence")]
    [InlineData("evidence ")]
    public void InvalidEvidenceReferenceIsRejected(string evidenceReference)
    {
        Assert.Throws<ArgumentException>(() => CreateEvaluation(evidenceReference: evidenceReference));
    }

    private static RuleEvaluation CreateEvaluation(
        RuleId? ruleId = null,
        RuleDefinitionStatus definitionStatus = RuleDefinitionStatus.Candidate,
        RuleEvaluationResult result = RuleEvaluationResult.Waiting,
        int sequence = 1,
        bool isRequired = false,
        string reason = "Evaluation is pending.",
        DateTimeOffset? evaluatedAtUtc = null,
        string? evidenceReference = null)
    {
        return new RuleEvaluation(
            ruleId ?? new RuleId("NQ-LIQ-001"),
            definitionStatus,
            result,
            sequence,
            isRequired,
            reason,
            evaluatedAtUtc ?? EvaluatedAtUtc,
            evidenceReference);
    }
}

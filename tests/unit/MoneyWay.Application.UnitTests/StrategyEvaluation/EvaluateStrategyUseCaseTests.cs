using MoneyWay.Application.StrategyEvaluation;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.StrategyEvaluation;

public sealed class EvaluateStrategyUseCaseTests
{
    private static readonly StrategyId StrategyId = new("strategy-one");
    private static readonly StrategyVersion StrategyVersion = new("strategy-one-0.1-draft");
    private static readonly DateTimeOffset EvaluatedAtUtc = new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);
    private readonly EvaluateStrategyUseCase useCase = new();

    [Fact]
    public void NullRequestIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => useCase.Execute(null!));
    }

    [Fact]
    public void EmptyRequestProducesDataUnavailable()
    {
        var result = useCase.Execute(CreateRequest([]));

        Assert.Equal(StrategyVerdict.DataUnavailable, result.Outcome.Verdict);
        Assert.Empty(result.Evaluations);
    }

    [Fact]
    public void DuplicateSequenceIsRejected()
    {
        var request = CreateRequest(
            [CreateEvaluation("RULE-001", 1), CreateEvaluation("RULE-002", 1)]);

        Assert.Throws<ArgumentException>(() => useCase.Execute(request));
    }

    [Fact]
    public void IdentityAndVersionArePreserved()
    {
        var result = useCase.Execute(CreateRequest([CreateEvaluation("RULE-001", 1)]));

        Assert.Same(StrategyId, result.StrategyId);
        Assert.Same(StrategyVersion, result.StrategyVersion);
    }

    [Theory]
    [InlineData(RuleEvaluationResult.Passed)]
    [InlineData(RuleEvaluationResult.NotApplicable)]
    public void NonBlockingRequiredEvaluationProducesReady(RuleEvaluationResult evaluationResult)
    {
        var result = useCase.Execute(CreateRequest([CreateEvaluation("RULE-001", 1, evaluationResult)]));

        Assert.Equal(StrategyVerdict.Ready, result.Outcome.Verdict);
    }

    [Fact]
    public void OnlyOptionalRulesProduceReady()
    {
        var result = useCase.Execute(CreateRequest(
            [CreateEvaluation("RULE-001", 1, RuleEvaluationResult.Failed, false)]));

        Assert.Equal(StrategyVerdict.Ready, result.Outcome.Verdict);
    }

    [Theory]
    [InlineData(RuleEvaluationResult.Waiting, StrategyVerdict.Wait)]
    [InlineData(RuleEvaluationResult.Failed, StrategyVerdict.NoTrade)]
    [InlineData(RuleEvaluationResult.HumanValidationRequired, StrategyVerdict.HumanValidationRequired)]
    [InlineData(RuleEvaluationResult.DataUnavailable, StrategyVerdict.DataUnavailable)]
    public void RequiredBlockingResultUsesDomainMapping(
        RuleEvaluationResult evaluationResult,
        StrategyVerdict expectedVerdict)
    {
        var result = useCase.Execute(CreateRequest([CreateEvaluation("RULE-001", 1, evaluationResult)]));

        Assert.Equal(expectedVerdict, result.Outcome.Verdict);
    }

    [Fact]
    public void UnorderedInputProducesOrderedCompleteSnapshot()
    {
        var later = CreateEvaluation("LATER", 30, RuleEvaluationResult.Failed);
        var earlier = CreateEvaluation("EARLIER", 10);
        var blocker = CreateEvaluation("BLOCKER", 20, RuleEvaluationResult.Waiting);

        var result = useCase.Execute(CreateRequest([later, earlier, blocker]));

        Assert.Equal([10, 20, 30], result.Evaluations.Select(evaluation => evaluation.Sequence));
        Assert.Equal([earlier, blocker, later], result.Evaluations);
        Assert.Equal(StrategyVerdict.Wait, result.Outcome.Verdict);
        Assert.Equal(blocker.RuleId, result.Outcome.BlockingRuleId);
        Assert.Equal(20, result.Outcome.BlockingSequence);
        Assert.Equal(RuleEvaluationResult.Waiting, result.Outcome.BlockingResult);
    }

    [Fact]
    public void OptionalBlockerDoesNotChangeRequiredVerdict()
    {
        var result = useCase.Execute(CreateRequest(
            [
                CreateEvaluation("OPTIONAL", 1, RuleEvaluationResult.DataUnavailable, false),
                CreateEvaluation("REQUIRED", 2, RuleEvaluationResult.Failed),
            ]));

        Assert.Equal(StrategyVerdict.NoTrade, result.Outcome.Verdict);
        Assert.Equal(new RuleId("REQUIRED"), result.Outcome.BlockingRuleId);
    }

    [Fact]
    public void BlockingReasonIsPreservedExactly()
    {
        const string reason = "Original audited reason.";
        var result = useCase.Execute(CreateRequest(
            [CreateEvaluation("RULE-001", 1, RuleEvaluationResult.Waiting, reason: reason)]));

        Assert.Equal(reason, result.Outcome.Reason);
    }

    [Fact]
    public void AuditSnapshotPreservesEveryEvaluationAndCount()
    {
        var before = CreateEvaluation("BEFORE", 1);
        var blocker = CreateEvaluation("BLOCKER", 2, RuleEvaluationResult.Failed);
        var afterRequired = CreateEvaluation("AFTER-REQUIRED", 3);
        var afterOptional = CreateEvaluation("AFTER-OPTIONAL", 4, isRequired: false);

        var result = useCase.Execute(CreateRequest([before, blocker, afterRequired, afterOptional]));

        Assert.Equal([before, blocker, afterRequired, afterOptional], result.Evaluations);
        Assert.Equal(4, result.Outcome.EvaluationCount);
        Assert.Equal(3, result.Outcome.RequiredEvaluationCount);
    }

    [Fact]
    public void RuleEvaluationDetailsArePreserved()
    {
        var evaluation = CreateEvaluation(
            "RULE-001",
            1,
            definitionStatus: RuleDefinitionStatus.VisualOnly,
            evidenceReference: "video-01@50:15");

        var preserved = Assert.Single(useCase.Execute(CreateRequest([evaluation])).Evaluations);

        Assert.Same(evaluation, preserved);
        Assert.Equal(evaluation.RuleId, preserved.RuleId);
        Assert.Equal(RuleDefinitionStatus.VisualOnly, preserved.DefinitionStatus);
        Assert.Equal("video-01@50:15", preserved.EvidenceReference);
        Assert.Equal(EvaluatedAtUtc, preserved.EvaluatedAtUtc);
    }

    [Fact]
    public void OriginalListChangesAfterRequestConstructionDoNotAlterResult()
    {
        var evaluation = CreateEvaluation("RULE-001", 1);
        var source = new List<RuleEvaluation> { evaluation };
        var request = CreateRequest(source);
        source.Clear();

        var result = useCase.Execute(request);

        Assert.Equal([evaluation], result.Evaluations);
    }

    [Fact]
    public void ResultCollectionCannotBeModified()
    {
        var result = useCase.Execute(CreateRequest([CreateEvaluation("RULE-001", 1)]));
        var collection = Assert.IsAssignableFrom<ICollection<RuleEvaluation>>(result.Evaluations);

        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Clear());
    }

    [Fact]
    public void DifferentInputOrdersProduceEquivalentResults()
    {
        var first = CreateEvaluation("FIRST", 10);
        var blocker = CreateEvaluation("BLOCKER", 20, RuleEvaluationResult.Waiting);
        var last = CreateEvaluation("LAST", 30, RuleEvaluationResult.Failed);

        var ordered = useCase.Execute(CreateRequest([first, blocker, last]));
        var shuffled = useCase.Execute(CreateRequest([last, first, blocker]));

        Assert.Equal(ordered.StrategyId, shuffled.StrategyId);
        Assert.Equal(ordered.StrategyVersion, shuffled.StrategyVersion);
        Assert.Equal(ordered.Outcome, shuffled.Outcome);
        Assert.Equal(ordered.Evaluations, shuffled.Evaluations);
    }

    [Fact]
    public void RepeatedExecutionProducesEquivalentResults()
    {
        var request = CreateRequest(
            [CreateEvaluation("RULE-001", 1), CreateEvaluation("RULE-002", 2, RuleEvaluationResult.Failed)]);

        var first = useCase.Execute(request);
        var second = useCase.Execute(request);

        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Equal(first.Evaluations, second.Evaluations);
    }

    private static EvaluateStrategyRequest CreateRequest(IEnumerable<RuleEvaluation> evaluations)
    {
        return new EvaluateStrategyRequest(StrategyId, StrategyVersion, evaluations);
    }

    private static RuleEvaluation CreateEvaluation(
        string ruleId,
        int sequence,
        RuleEvaluationResult result = RuleEvaluationResult.Passed,
        bool isRequired = true,
        string? reason = null,
        RuleDefinitionStatus definitionStatus = RuleDefinitionStatus.Confirmed,
        string? evidenceReference = null)
    {
        return new RuleEvaluation(
            new RuleId(ruleId),
            definitionStatus,
            result,
            sequence,
            isRequired,
            reason ?? $"Reason for {ruleId}.",
            EvaluatedAtUtc,
            evidenceReference);
    }
}

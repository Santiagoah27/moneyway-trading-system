using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.StrategyReplay.Workflow;

public sealed class AdvanceStrategyReplayProgressionUseCaseTests
{
    private static readonly StrategyId Strategy = new("synthetic");
    private static readonly StrategyVersion Version = new("v1");
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private readonly AdvanceStrategyReplayProgressionUseCase useCase = new();

    [Fact]
    public void MissingPrerequisiteBlocksEligibilityWithoutChangingRawEvaluation()
    {
        var workflow = Workflow(new StrategyReplayRulePrerequisite(new("B"), [new("A")]));
        var observation = Observation(1, Evaluation("A", 10, RuleEvaluationResult.Failed), Evaluation("B", 20, RuleEvaluationResult.Passed), Evaluation("D", 40, RuleEvaluationResult.Passed));

        var snapshot = useCase.Execute(workflow, observation, null);

        Assert.Equal(RuleEvaluationResult.Passed, observation.Evaluations.Single(item => item.RuleId == new RuleId("B")).Result);
        var blocked = snapshot.RuleEligibility.Single(item => item.RuleId == new RuleId("B"));
        Assert.False(blocked.IsEligible);
        Assert.Equal([new RuleId("A")], blocked.PrerequisiteRuleIds);
        Assert.Equal([new RuleId("A")], blocked.MissingPrerequisiteRuleIds);
        Assert.False(blocked.EstablishesProgression);
        var independent = snapshot.RuleEligibility.Single(item => item.RuleId == new RuleId("D"));
        Assert.True(independent.IsEligible);
        Assert.True(independent.EstablishesProgression);
        Assert.Equal([new RuleId("D")], snapshot.EstablishedRuleIds);
    }

    [Fact]
    public void AndPrerequisitesMustAllBePreviouslyEstablished()
    {
        var workflow = Workflow(new StrategyReplayRulePrerequisite(new("C"), [new("A"), new("B")]));
        var first = useCase.Execute(workflow, Observation(1, Evaluation("A", 10, RuleEvaluationResult.Passed)), null);
        var second = useCase.Execute(workflow, Observation(2, Evaluation("C", 30, RuleEvaluationResult.Passed, 2)), first);

        var blocked = Assert.Single(second.RuleEligibility);
        Assert.False(blocked.IsEligible);
        Assert.Equal([new RuleId("B")], blocked.MissingPrerequisiteRuleIds);

        var third = useCase.Execute(workflow, Observation(3, Evaluation("B", 20, RuleEvaluationResult.Passed, 3)), second);
        var fourth = useCase.Execute(workflow, Observation(4, Evaluation("C", 30, RuleEvaluationResult.Passed, 4)), third);
        Assert.True(Assert.Single(fourth.RuleEligibility).IsEligible);
        Assert.Contains(new RuleId("C"), fourth.EstablishedRuleIds);
    }

    [Fact]
    public void EstablishedEventPersistsWhenItsLaterRawConditionIsNotPassed()
    {
        var workflow = Workflow(new StrategyReplayRulePrerequisite(new("B"), [new("A")]));
        var first = useCase.Execute(workflow, Observation(1, Evaluation("A", 10, RuleEvaluationResult.Passed)), null);
        var second = useCase.Execute(workflow, Observation(2,
            Evaluation("A", 10, RuleEvaluationResult.Failed, 2),
            Evaluation("B", 20, RuleEvaluationResult.Passed, 2)), first);

        Assert.Contains(new RuleId("A"), second.EstablishedRuleIds);
        var downstream = second.RuleEligibility.Single(item => item.RuleId == new RuleId("B"));
        Assert.True(downstream.IsEligible);
        Assert.True(downstream.EstablishesProgression);
    }

    [Fact]
    public void IneligiblePassedPatternIsNeverPromotedRetroactively()
    {
        var workflow = Workflow(
            new StrategyReplayRulePrerequisite(new("B"), [new("A")]),
            new StrategyReplayRulePrerequisite(new("C"), [new("B")]));
        var first = useCase.Execute(workflow, Observation(1, Evaluation("B", 20, RuleEvaluationResult.Passed)), null);
        var second = useCase.Execute(workflow, Observation(2, Evaluation("A", 10, RuleEvaluationResult.Passed, 2)), first);
        var third = useCase.Execute(workflow, Observation(3, Evaluation("C", 30, RuleEvaluationResult.Passed, 3)), second);

        Assert.False(Assert.Single(first.RuleEligibility).IsEligible);
        Assert.DoesNotContain(new RuleId("B"), second.EstablishedRuleIds);
        Assert.False(Assert.Single(third.RuleEligibility).IsEligible);
        Assert.Equal([new RuleId("B")], Assert.Single(third.RuleEligibility).MissingPrerequisiteRuleIds);

        var fourth = useCase.Execute(workflow, Observation(4, Evaluation("B", 20, RuleEvaluationResult.Passed, 4)), third);
        var fifth = useCase.Execute(workflow, Observation(5, Evaluation("C", 30, RuleEvaluationResult.Passed, 5)), fourth);
        Assert.True(Assert.Single(fourth.RuleEligibility).IsEligible);
        Assert.True(Assert.Single(fifth.RuleEligibility).IsEligible);
    }

    [Fact]
    public void SameFramePrerequisiteDoesNotCreateOrderDependentTransitiveProgression()
    {
        var workflow = Workflow(
            new StrategyReplayRulePrerequisite(new("B"), [new("A")]),
            new StrategyReplayRulePrerequisite(new("C"), [new("B")]));
        var first = useCase.Execute(workflow, Observation(1,
            Evaluation("B", 10, RuleEvaluationResult.Passed),
            Evaluation("C", 20, RuleEvaluationResult.Passed),
            Evaluation("A", 30, RuleEvaluationResult.Passed)), null);

        Assert.True(first.RuleEligibility.Single(item => item.RuleId == new RuleId("A")).IsEligible);
        Assert.False(first.RuleEligibility.Single(item => item.RuleId == new RuleId("B")).IsEligible);
        Assert.False(first.RuleEligibility.Single(item => item.RuleId == new RuleId("C")).IsEligible);
        Assert.Equal([new RuleId("A")], first.EstablishedRuleIds);
    }

    [Fact]
    public void FutureObservationsCannotMutateEarlierSnapshotAndEquivalentFoldsAreDeterministic()
    {
        var workflow = Workflow(new StrategyReplayRulePrerequisite(new("B"), [new("A")]));
        var shortFirst = useCase.Execute(workflow, Observation(1, Evaluation("A", 10, RuleEvaluationResult.Passed)), null);
        var shortAtT = useCase.Execute(workflow, Observation(2, Evaluation("B", 20, RuleEvaluationResult.Passed, 2)), shortFirst);
        var longFirst = useCase.Execute(workflow, Observation(1, Evaluation("A", 10, RuleEvaluationResult.Passed)), null);
        var longAtT = useCase.Execute(workflow, Observation(2, Evaluation("B", 20, RuleEvaluationResult.Passed, 2)), longFirst);
        _ = useCase.Execute(workflow, Observation(3, Evaluation("A", 10, RuleEvaluationResult.Failed, 3)), longAtT);

        Assert.Equal(Signature(shortAtT), Signature(longAtT));
        Assert.Equal([new RuleId("A"), new RuleId("B")], shortAtT.EstablishedRuleIds);
        Assert.Equal([new RuleId("A"), new RuleId("B")], longAtT.EstablishedRuleIds);
    }

    [Theory]
    [InlineData("strategy")]
    [InlineData("version")]
    [InlineData("provider")]
    [InlineData("symbol")]
    [InlineData("step")]
    [InlineData("timestamp")]
    public void ProgressionRejectsCrossIdentityAndNonChronologicalContinuation(string mismatch)
    {
        var workflow = Workflow(new StrategyReplayRulePrerequisite(new("B"), [new("A")]));
        var first = useCase.Execute(workflow, Observation(1, Evaluation("A", 10, RuleEvaluationResult.Passed)), null);
        var observation = mismatch switch
        {
            "strategy" => Observation(2, [Evaluation("B", 20, RuleEvaluationResult.Passed, 2)], strategyId: new("other")),
            "version" => Observation(2, [Evaluation("B", 20, RuleEvaluationResult.Passed, 2)], version: new("v2")),
            "provider" => Observation(2, [Evaluation("B", 20, RuleEvaluationResult.Passed, 2)], provider: new("other")),
            "symbol" => Observation(2, [Evaluation("B", 20, RuleEvaluationResult.Passed, 2)], symbol: new("OTHER")),
            "step" => Observation(3, Evaluation("B", 20, RuleEvaluationResult.Passed, 3)),
            "timestamp" => Observation(2, [Evaluation("B", 20, RuleEvaluationResult.Passed, 1)], asOfUtc: Start.AddMinutes(1)),
            _ => throw new InvalidOperationException(),
        };

        Assert.Throws<InvalidOperationException>(() => useCase.Execute(workflow, observation, first));
    }

    private static string Signature(StrategyReplayProgressionSnapshot snapshot) => string.Join('|',
        snapshot.StrategyId,
        snapshot.StrategyVersion,
        snapshot.ProviderId,
        snapshot.Symbol,
        snapshot.Step,
        snapshot.AsOfUtc,
        string.Join(';', snapshot.RuleEligibility.Select(item =>
            $"{item.RuleId}:{item.IsEligible}:{string.Join(',', item.PrerequisiteRuleIds)}:{string.Join(',', item.MissingPrerequisiteRuleIds)}:{item.EstablishesProgression}")),
        string.Join(',', snapshot.EstablishedRuleIds));

    private static StrategyReplayWorkflowDefinition Workflow(params StrategyReplayRulePrerequisite[] declarations) =>
        new(Strategy, Version, declarations);

    private static StrategyReplayContextObservation Observation(int step, params RuleEvaluation[] evaluations) =>
        Observation(step, evaluations, Strategy, Version, Provider, Symbol, Start.AddMinutes(step));

    private static StrategyReplayContextObservation Observation(
        int step,
        IEnumerable<RuleEvaluation> evaluations,
        StrategyId? strategyId = null,
        StrategyVersion? version = null,
        MarketDataProviderId? provider = null,
        MarketSymbol? symbol = null,
        DateTimeOffset? asOfUtc = null) => new(
            strategyId ?? Strategy,
            version ?? Version,
            provider ?? Provider,
            symbol ?? Symbol,
            step,
            asOfUtc ?? Start.AddMinutes(step),
            evaluations);

    private static RuleEvaluation Evaluation(
        string ruleId,
        int sequence,
        RuleEvaluationResult result,
        int step = 1) => new(
            new(ruleId),
            RuleDefinitionStatus.Confirmed,
            result,
            sequence,
            true,
            "Synthetic.",
            Start.AddMinutes(step),
            null);
}

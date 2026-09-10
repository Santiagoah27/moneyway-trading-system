using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.StrategyReplay.Workflow;

public sealed class AdvanceStrategyReplayLifecycleUseCaseTests
{
    private static readonly StrategyId Strategy = new("synthetic-lifecycle");
    private static readonly StrategyVersion Version = new("v1");
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly StrategyReplayWorkflowDefinition Workflow = new(
        Strategy,
        Version,
        [new(new("B"), [new("A")]), new(new("C"), [new("B")])]);
    private readonly AdvanceStrategyReplayLifecycleUseCase useCase = new(new());

    [Fact]
    public void StartCreatesDeterministicReplayLocalInstanceAndAuditRecord()
    {
        var policy = Policy((1, StrategyReplayLifecycleTransition.Start(new("R1"))));

        var result = Advance(policy, Observation(1, Evaluation("A", RuleEvaluationResult.Passed)));

        var active = Assert.IsType<StrategyReplayProgressionInstanceSnapshot>(result.LifecycleProgression.ActiveInstance);
        Assert.Equal(new StrategyReplayProgressionInstanceId(1), active.InstanceId);
        Assert.Equal(Start.AddMinutes(1), active.StartedAtUtc);
        Assert.Equal(new StrategyReplayProgressionStateReference("R1"), active.StateReference);
        Assert.Contains(new RuleId("A"), active.WorkflowProgression.EstablishedRuleIds);
        var record = Assert.Single(result.LifecycleProgression.TransitionHistory);
        Assert.Equal(StrategyReplayLifecycleTransitionKind.Start, record.Kind);
        Assert.Null(record.PriorActiveInstanceId);
        Assert.Equal(active.InstanceId, record.ResultingActiveInstanceId);
    }

    [Fact]
    public void NoTransitionPreservesOneActiveInstanceWithoutDuplicateHistory()
    {
        var policy = Policy((1, StrategyReplayLifecycleTransition.Start(new("R1"))));
        var first = Advance(policy, Observation(1, Evaluation("A", RuleEvaluationResult.Passed)));

        var second = Advance(policy, Observation(2, Evaluation("B", RuleEvaluationResult.Passed, 2)), first.LifecycleProgression);

        Assert.Equal(first.LifecycleProgression.ActiveInstance!.InstanceId, second.LifecycleProgression.ActiveInstance!.InstanceId);
        Assert.Single(second.LifecycleProgression.Instances);
        Assert.Single(second.LifecycleProgression.TransitionHistory);
        Assert.Contains(new RuleId("B"), second.LifecycleProgression.ActiveInstance.WorkflowProgression.EstablishedRuleIds);
    }

    [Fact]
    public void ReplacementKeepsIdentityAndEarlierSnapshotsImmutableAcrossMultipleUpdates()
    {
        var policy = Policy(
            (1, StrategyReplayLifecycleTransition.Start(new("R1"))),
            (2, StrategyReplayLifecycleTransition.ReplaceActiveState(new("R2"))),
            (3, StrategyReplayLifecycleTransition.ReplaceActiveState(new("R3"))));
        var first = Advance(policy, Observation(1, Evaluation("A", RuleEvaluationResult.Passed)));
        var firstSignature = Signature(first.LifecycleProgression);
        var second = Advance(policy, Observation(2, Evaluation("B", RuleEvaluationResult.Passed, 2)), first.LifecycleProgression);
        var third = Advance(policy, Observation(3, Evaluation("C", RuleEvaluationResult.Passed, 3)), second.LifecycleProgression);

        Assert.Equal("R1", first.LifecycleProgression.ActiveInstance!.StateReference.Value);
        Assert.Equal("R2", second.LifecycleProgression.ActiveInstance!.StateReference.Value);
        Assert.Equal("R3", third.LifecycleProgression.ActiveInstance!.StateReference.Value);
        Assert.Equal(first.LifecycleProgression.ActiveInstance.InstanceId, third.LifecycleProgression.ActiveInstance.InstanceId);
        Assert.Equal(firstSignature, Signature(first.LifecycleProgression));
        Assert.Equal([StrategyReplayLifecycleTransitionKind.Start, StrategyReplayLifecycleTransitionKind.ReplaceActiveState, StrategyReplayLifecycleTransitionKind.ReplaceActiveState],
            third.LifecycleProgression.TransitionHistory.Select(item => item.Kind));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TerminationIsTerminalAndLaterObservationsCannotMutateOrReviveInstance(bool expires)
    {
        var termination = expires ? StrategyReplayLifecycleTransition.Expire() : StrategyReplayLifecycleTransition.Cancel();
        var policy = Policy(
            (1, StrategyReplayLifecycleTransition.Start(new("R1"))),
            (2, termination));
        var first = Advance(policy, Observation(1, Evaluation("A", RuleEvaluationResult.Passed)));
        var second = Advance(policy, Observation(2, Evaluation("B", RuleEvaluationResult.Passed, 2)), first.LifecycleProgression);
        var third = Advance(policy, Observation(3, Evaluation("C", RuleEvaluationResult.Passed, 3)), second.LifecycleProgression);

        Assert.Null(second.LifecycleProgression.ActiveInstance);
        Assert.Null(third.LifecycleProgression.ActiveInstance);
        var terminal = Assert.Single(third.LifecycleProgression.Instances);
        Assert.False(terminal.IsActive);
        Assert.Equal(expires ? StrategyReplayProgressionTerminationKind.Expired : StrategyReplayProgressionTerminationKind.Cancelled, terminal.TerminationKind);
        Assert.Equal([new RuleId("A")], terminal.WorkflowProgression.EstablishedRuleIds);
        Assert.Equal(2, third.LifecycleProgression.TransitionHistory.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NewProgressionAfterTerminationUsesDistinctIdentityAndCleanPrerequisites(bool expires)
    {
        var policy = Policy(
            (1, StrategyReplayLifecycleTransition.Start(new("R1"))),
            (2, expires ? StrategyReplayLifecycleTransition.Expire() : StrategyReplayLifecycleTransition.Cancel()),
            (3, StrategyReplayLifecycleTransition.Start(new("R2"))));
        var first = Advance(policy, Observation(1, Evaluation("A", RuleEvaluationResult.Passed)));
        var second = Advance(policy, Observation(2, Evaluation("B", RuleEvaluationResult.Passed, 2)), first.LifecycleProgression);
        var third = Advance(policy, Observation(3, Evaluation("B", RuleEvaluationResult.Passed, 3)), second.LifecycleProgression);

        Assert.Equal(2, third.LifecycleProgression.Instances.Count);
        var old = third.LifecycleProgression.Instances.Single(item => item.InstanceId.Ordinal == 1);
        var active = Assert.IsType<StrategyReplayProgressionInstanceSnapshot>(third.LifecycleProgression.ActiveInstance);
        Assert.Equal(2, active.InstanceId.Ordinal);
        Assert.False(old.IsActive);
        Assert.Empty(active.WorkflowProgression.EstablishedRuleIds);
        Assert.False(active.WorkflowProgression.RuleEligibility.Single().IsEligible);
    }

    [Fact]
    public void IneligibleObservationNeverPromotesAcrossAReplacementInstanceOrWithinStartFrame()
    {
        var policy = Policy(
            (1, StrategyReplayLifecycleTransition.Start(new("R1"))),
            (2, StrategyReplayLifecycleTransition.Cancel()),
            (3, StrategyReplayLifecycleTransition.Start(new("R2"))));
        var first = Advance(policy, Observation(1,
            Evaluation("A", RuleEvaluationResult.Passed),
            Evaluation("B", RuleEvaluationResult.Passed)));
        var second = Advance(policy, Observation(2, Evaluation("A", RuleEvaluationResult.Failed, 2)), first.LifecycleProgression);
        var third = Advance(policy, Observation(3, Evaluation("A", RuleEvaluationResult.Passed, 3)), second.LifecycleProgression);

        Assert.False(first.WorkflowProgression.RuleEligibility.Single(item => item.RuleId == new RuleId("B")).IsEligible);
        Assert.DoesNotContain(new RuleId("B"), first.WorkflowProgression.EstablishedRuleIds);
        Assert.DoesNotContain(new RuleId("B"), third.LifecycleProgression.ActiveInstance!.WorkflowProgression.EstablishedRuleIds);
        Assert.Contains(new RuleId("A"), third.LifecycleProgression.ActiveInstance.WorkflowProgression.EstablishedRuleIds);
    }

    [Fact]
    public void StateReplacementDoesNotEnableTransitiveProgressionWithinTheSameFrame()
    {
        var policy = Policy(
            (1, StrategyReplayLifecycleTransition.Start(new("R1"))),
            (2, StrategyReplayLifecycleTransition.ReplaceActiveState(new("R2"))));
        var first = Advance(policy, Observation(1, Evaluation("A", RuleEvaluationResult.Passed)));

        var second = Advance(policy, Observation(2,
            Evaluation("B", RuleEvaluationResult.Passed, 2),
            Evaluation("C", RuleEvaluationResult.Passed, 2)), first.LifecycleProgression);

        Assert.True(second.WorkflowProgression.RuleEligibility.Single(item => item.RuleId == new RuleId("B")).IsEligible);
        Assert.False(second.WorkflowProgression.RuleEligibility.Single(item => item.RuleId == new RuleId("C")).IsEligible);
        Assert.Contains(new RuleId("B"), second.WorkflowProgression.EstablishedRuleIds);
        Assert.DoesNotContain(new RuleId("C"), second.WorkflowProgression.EstablishedRuleIds);
        Assert.Equal("R2", second.LifecycleProgression.ActiveInstance!.StateReference.Value);
    }

    [Fact]
    public void TerminatedInstanceCannotBeUpdatedOrResumed()
    {
        var policy = Policy(
            (1, StrategyReplayLifecycleTransition.Start(new("R1"))),
            (2, StrategyReplayLifecycleTransition.Cancel()),
            (3, StrategyReplayLifecycleTransition.ReplaceActiveState(new("R2"))));
        var first = Advance(policy, Observation(1, Evaluation("A", RuleEvaluationResult.Passed)));
        var second = Advance(policy, Observation(2, Evaluation("A", RuleEvaluationResult.Failed, 2)), first.LifecycleProgression);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Advance(policy, Observation(3, Evaluation("A", RuleEvaluationResult.Passed, 3)), second.LifecycleProgression));

        Assert.Contains("requires an active progression", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FutureTransitionsCannotChangeAnEquivalentPrefixSnapshot()
    {
        var shortPolicy = Policy((1, StrategyReplayLifecycleTransition.Start(new("R1"))));
        var longPolicy = Policy(
            (1, StrategyReplayLifecycleTransition.Start(new("R1"))),
            (3, StrategyReplayLifecycleTransition.ReplaceActiveState(new("R2"))),
            (4, StrategyReplayLifecycleTransition.Cancel()),
            (5, StrategyReplayLifecycleTransition.Start(new("R3"))),
            (6, StrategyReplayLifecycleTransition.Expire()));
        var shortFirst = Advance(shortPolicy, Observation(1, Evaluation("A", RuleEvaluationResult.Passed)));
        var shortAtT = Advance(shortPolicy, Observation(2, Evaluation("B", RuleEvaluationResult.Passed, 2)), shortFirst.LifecycleProgression);
        var longFirst = Advance(longPolicy, Observation(1, Evaluation("A", RuleEvaluationResult.Passed)));
        var longAtT = Advance(longPolicy, Observation(2, Evaluation("B", RuleEvaluationResult.Passed, 2)), longFirst.LifecycleProgression);
        var current = longAtT.LifecycleProgression;
        for (var step = 3; step <= 6; step++)
            current = Advance(longPolicy, Observation(step, Evaluation("A", RuleEvaluationResult.Failed, step)), current).LifecycleProgression;

        Assert.Equal(Signature(shortAtT.LifecycleProgression), Signature(longAtT.LifecycleProgression));
    }

    [Fact]
    public void EquivalentExecutionsProduceValueEquivalentIdentitiesHistoryAndProgression()
    {
        var policy = Policy(
            (1, StrategyReplayLifecycleTransition.Start(new("R1"))),
            (2, StrategyReplayLifecycleTransition.ReplaceActiveState(new("R2"))),
            (3, StrategyReplayLifecycleTransition.Cancel()),
            (4, StrategyReplayLifecycleTransition.Start(new("R3"))));

        var left = Replay(policy, 4);
        var right = Replay(policy, 4);

        Assert.Equal(Signature(left), Signature(right));
    }

    [Fact]
    public void IndependentStrategiesVersionsProvidersAndSymbolsNeverShareLifecycleState()
    {
        var policy = Policy((1, StrategyReplayLifecycleTransition.Start(new("R1"))));
        var firstIdentity = Advance(policy, Observation(1, Evaluation("A", RuleEvaluationResult.Passed))).LifecycleProgression;
        var otherMarketIdentity = Advance(
            policy,
            Observation(
                1,
                [Evaluation("A", RuleEvaluationResult.Passed)],
                provider: new("other-provider"),
                symbol: new("OTHER"))).LifecycleProgression;
        var otherVersion = new StrategyVersion("v2");
        var otherWorkflow = new StrategyReplayWorkflowDefinition(
            Strategy,
            otherVersion,
            [new(new("B"), [new("A")])]);
        var otherPolicy = new SyntheticPolicy(
            Strategy,
            otherVersion,
            new Dictionary<int, StrategyReplayLifecycleTransition>
            {
                [1] = StrategyReplayLifecycleTransition.Start(new("R1")),
            });
        var versionObservation = Observation(
            1,
            [Evaluation("A", RuleEvaluationResult.Passed)],
            version: otherVersion);
        var otherVersionResult = useCase.Execute(otherWorkflow, otherPolicy, versionObservation, null).LifecycleProgression;

        Assert.Equal(1, firstIdentity.ActiveInstance!.InstanceId.Ordinal);
        Assert.Equal(1, otherMarketIdentity.ActiveInstance!.InstanceId.Ordinal);
        Assert.Equal(1, otherVersionResult.ActiveInstance!.InstanceId.Ordinal);
        Assert.Equal(Provider, firstIdentity.ProviderId);
        Assert.Equal(new MarketDataProviderId("other-provider"), otherMarketIdentity.ProviderId);
        Assert.Equal(Symbol, firstIdentity.Symbol);
        Assert.Equal(new MarketSymbol("OTHER"), otherMarketIdentity.Symbol);
        Assert.Equal(Version, firstIdentity.StrategyVersion);
        Assert.Equal(otherVersion, otherVersionResult.StrategyVersion);
        Assert.NotSame(firstIdentity.ActiveInstance, otherMarketIdentity.ActiveInstance);
        Assert.NotSame(firstIdentity.ActiveInstance, otherVersionResult.ActiveInstance);
    }

    [Theory]
    [InlineData("strategy")]
    [InlineData("version")]
    [InlineData("provider")]
    [InlineData("symbol")]
    [InlineData("step")]
    [InlineData("timestamp")]
    public void FoldRejectsCrossIdentityAndOutOfOrderContinuation(string mismatch)
    {
        var policy = Policy((1, StrategyReplayLifecycleTransition.Start(new("R1"))));
        var first = Advance(policy, Observation(1, Evaluation("A", RuleEvaluationResult.Passed)));
        var observation = mismatch switch
        {
            "strategy" => Observation(2, [Evaluation("A", RuleEvaluationResult.Passed, 2)], strategy: new("other")),
            "version" => Observation(2, [Evaluation("A", RuleEvaluationResult.Passed, 2)], version: new("v2")),
            "provider" => Observation(2, [Evaluation("A", RuleEvaluationResult.Passed, 2)], provider: new("other")),
            "symbol" => Observation(2, [Evaluation("A", RuleEvaluationResult.Passed, 2)], symbol: new("OTHER")),
            "step" => Observation(3, Evaluation("A", RuleEvaluationResult.Passed, 3)),
            "timestamp" => Observation(2, [Evaluation("A", RuleEvaluationResult.Passed, 0)], asOfUtc: Start),
            _ => throw new InvalidOperationException(),
        };

        Assert.Throws<InvalidOperationException>(() => Advance(policy, observation, first.LifecycleProgression));
    }

    [Fact]
    public void PolicyCatalogResolvesExactIdentityAndRejectsDuplicatesAndUnknownStrategies()
    {
        var definition = Definition(Strategy, Version);
        var policy = Policy();
        var catalog = new StrategyReplayLifecyclePolicyCatalog([definition], [policy]);

        Assert.Same(policy, catalog.Find(Strategy, Version));
        Assert.Null(catalog.Find(Strategy, new("v2")));
        Assert.Null(StrategyReplayLifecyclePolicyCatalog.Empty.Find(Strategy, Version));
        Assert.Throws<ArgumentException>(() => new StrategyReplayLifecyclePolicyCatalog([definition], [policy, policy]));
        Assert.Throws<InvalidOperationException>(() => new StrategyReplayLifecyclePolicyCatalog(
            [definition],
            [new SyntheticPolicy(new("other"), Version, new Dictionary<int, StrategyReplayLifecycleTransition>())]));
    }

    private StrategyReplayLifecycleSnapshot Replay(IStrategyReplayLifecyclePolicy policy, int frameCount)
    {
        StrategyReplayLifecycleSnapshot? current = null;
        for (var step = 1; step <= frameCount; step++)
            current = Advance(policy, Observation(step, Evaluation("A", RuleEvaluationResult.Passed, step)), current).LifecycleProgression;
        return current!;
    }

    private StrategyReplayLifecycleAdvanceResult Advance(
        IStrategyReplayLifecyclePolicy policy,
        StrategyReplayContextObservation observation,
        StrategyReplayLifecycleSnapshot? previous = null) => useCase.Execute(Workflow, policy, observation, previous);

    private static SyntheticPolicy Policy(params (int Step, StrategyReplayLifecycleTransition Transition)[] decisions) =>
        new(Strategy, Version, decisions.ToDictionary(item => item.Step, item => item.Transition));

    private static string Signature(StrategyReplayLifecycleSnapshot snapshot) => string.Join('|',
        snapshot.StrategyId,
        snapshot.StrategyVersion,
        snapshot.ProviderId,
        snapshot.Symbol,
        snapshot.Step,
        snapshot.AsOfUtc,
        string.Join(';', snapshot.Instances.Select(instance =>
            $"{instance.InstanceId.Ordinal}:{instance.StartedAtUtc:o}:{instance.StateReference}:{instance.TerminationKind}:{instance.TerminatedAtUtc:o}:{string.Join(',', instance.WorkflowProgression.EstablishedRuleIds)}")),
        string.Join(';', snapshot.TransitionHistory.Select(record =>
            $"{record.InstanceId.Ordinal}:{record.AsOfUtc:o}:{record.Kind}:{record.PriorActiveInstanceId}:{record.ResultingActiveInstanceId}:{record.PriorStateReference}:{record.ResultingStateReference}")));

    private static StrategyReplayContextObservation Observation(int step, params RuleEvaluation[] evaluations) =>
        Observation(step, evaluations, Strategy, Version, Provider, Symbol, Start.AddMinutes(step));

    private static StrategyReplayContextObservation Observation(
        int step,
        IEnumerable<RuleEvaluation> evaluations,
        StrategyId? strategy = null,
        StrategyVersion? version = null,
        MarketDataProviderId? provider = null,
        MarketSymbol? symbol = null,
        DateTimeOffset? asOfUtc = null) => new(
            strategy ?? Strategy,
            version ?? Version,
            provider ?? Provider,
            symbol ?? Symbol,
            step,
            asOfUtc ?? Start.AddMinutes(step),
            evaluations);

    private static RuleEvaluation Evaluation(string ruleId, RuleEvaluationResult result, int step = 1) => new(
        new(ruleId),
        RuleDefinitionStatus.Confirmed,
        result,
        ruleId[0] - 'A' + 1,
        true,
        "Synthetic.",
        Start.AddMinutes(step),
        null);

    private static StrategyDefinition Definition(StrategyId strategy, StrategyVersion version) => new(
        strategy,
        version,
        "Synthetic",
        "test",
        [new(new("A"), "A", "stage", 1, true, RuleDefinitionStatus.Confirmed, "description", "source")]);

    private sealed class SyntheticPolicy(
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        IReadOnlyDictionary<int, StrategyReplayLifecycleTransition> decisions) : IStrategyReplayLifecyclePolicy
    {
        public StrategyId StrategyId { get; } = strategyId;
        public StrategyVersion StrategyVersion { get; } = strategyVersion;

        public StrategyReplayLifecycleTransition Decide(StrategyReplayLifecyclePolicyContext context) =>
            decisions.GetValueOrDefault(context.Observation.Step, StrategyReplayLifecycleTransition.None);
    }
}

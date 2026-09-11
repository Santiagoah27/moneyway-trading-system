using MoneyWay.Application.Backtesting;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Observability;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.StrategyReplay.Workflow;

public sealed class StrategyReplayLifecycleEvidenceInfrastructureTests
{
    private static readonly StrategyId Strategy = new("synthetic-evidence");
    private static readonly StrategyVersion Version = new("v1");
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly StrategyDefinition Definition = new(
        Strategy, Version, "Synthetic", "test",
        [new(new("A"), "A", "stage", 1, true, RuleDefinitionStatus.Confirmed, "description", "source")]);
    private static readonly StrategyReplayWorkflowDefinition Workflow = new(Strategy, Version, []);

    [Fact]
    public void CanonicalOrchestrationTransportsTypedEvidenceAndBindsItToTheCorrectInstances()
    {
        var policy = new RecordingPolicy(context => context.Observation.Step switch
        {
            1 or 3 => StrategyReplayLifecycleTransition.Start(new($"R{context.Observation.Step}")),
            2 => StrategyReplayLifecycleTransition.Cancel(),
            _ => StrategyReplayLifecycleTransition.None,
        });
        var producer = new CloseEvidenceProducer();

        var run = UseCase(policy).Execute(Definition, Series(100m, 101m, 102m), producer);

        Assert.Equal(3, policy.Contexts.Count);
        Assert.All(policy.Contexts, context => Assert.True(context.Evidence.TryGet<SyntheticEvidence>(out _)));
        Assert.Equal(StrategyReplayLifecycleEvidenceScope.ActivationCandidate, policy.Contexts[0].Evidence.Scope);
        Assert.Equal(new StrategyReplayProgressionInstanceId(1), policy.Contexts[1].Evidence.InstanceId);
        Assert.Equal(StrategyReplayLifecycleEvidenceScope.ActivationCandidate, policy.Contexts[2].Evidence.Scope);
        var lifecycle = run.StrategyObservations[^1].LifecycleProgression!;
        Assert.Equal(2, lifecycle.Instances.Count);
        Assert.Equal(new StrategyReplayProgressionInstanceId(1), lifecycle.Instances[0].Evidence.InstanceId);
        Assert.Equal(new StrategyReplayProgressionInstanceId(2), lifecycle.Instances[1].Evidence.InstanceId);
        Assert.NotSame(lifecycle.Instances[0].Evidence, lifecycle.Instances[1].Evidence);
        Assert.True(lifecycle.Instances[0].Evidence.TryGet<SyntheticEvidence>(out var first));
        Assert.True(lifecycle.Instances[1].Evidence.TryGet<SyntheticEvidence>(out var second));
        Assert.Equal(101m, first!.Value);
        Assert.Equal(102m, second!.Value);
    }

    [Fact]
    public void RepeatedExecuteCallsAreDeterministicAndDoNotShareLifecycleEvidence()
    {
        var policy = new RecordingPolicy(context => context.Observation.Step == 1
            ? StrategyReplayLifecycleTransition.Start(new("R1"))
            : StrategyReplayLifecycleTransition.None);
        var useCase = UseCase(policy);
        var producer = new CloseEvidenceProducer();

        var first = useCase.Execute(Definition, Series(100m, 101m), producer);
        var firstEvidence = first.StrategyObservations[^1].LifecycleProgression!.ActiveInstance!.Evidence;
        var second = useCase.Execute(Definition, Series(100m, 101m), producer);
        var secondEvidence = second.StrategyObservations[^1].LifecycleProgression!.ActiveInstance!.Evidence;

        Assert.NotSame(firstEvidence, secondEvidence);
        Assert.Equal(Signature(firstEvidence), Signature(secondEvidence));
        Assert.Equal(new StrategyReplayProgressionInstanceId(1), secondEvidence.InstanceId);
    }

    [Fact]
    public void ChangedFutureDataCannotChangeEarlierEvidencePrefixes()
    {
        var firstPolicy = StartPolicy();
        var secondPolicy = StartPolicy();

        var first = UseCase(firstPolicy).Execute(Definition, Series(100m, 101m, 102m), new CloseEvidenceProducer());
        var changedFuture = UseCase(secondPolicy).Execute(Definition, Series(100m, 101m, 999m), new CloseEvidenceProducer());

        Assert.Equal(
            firstPolicy.Contexts.Take(2).Select(context => Signature(context.Evidence)),
            secondPolicy.Contexts.Take(2).Select(context => Signature(context.Evidence)));
        Assert.NotEqual(Signature(firstPolicy.Contexts[2].Evidence), Signature(secondPolicy.Contexts[2].Evidence));
    }

    [Fact]
    public void SnapshotCopiesInputRejectsDuplicateTypesAndRejectsFutureEvidence()
    {
        var evidence = new IStrategyReplayLifecycleEvidence[] { new SyntheticEvidence(100m, Start.AddMinutes(1)) };
        var snapshot = Snapshot(1, StrategyReplayLifecycleEvidenceScope.ActivationCandidate, null, evidence);
        evidence[0] = new SyntheticEvidence(999m, Start.AddMinutes(1));

        Assert.True(snapshot.TryGet<SyntheticEvidence>(out var retained));
        Assert.Equal(100m, retained!.Value);
        Assert.Throws<ArgumentException>(() => Snapshot(1, StrategyReplayLifecycleEvidenceScope.ActivationCandidate, null,
            [new SyntheticEvidence(1m, Start), new SyntheticEvidence(2m, Start)]));
        Assert.Throws<ArgumentException>(() => Snapshot(1, StrategyReplayLifecycleEvidenceScope.ActivationCandidate, null,
            [new SyntheticEvidence(1m, Start.AddMinutes(2))]));

        var ordered = Snapshot(1, StrategyReplayLifecycleEvidenceScope.ActivationCandidate, null,
            [new SyntheticEvidence(1m, Start), new AlternativeEvidence("A", Start)]);
        var reversed = Snapshot(1, StrategyReplayLifecycleEvidenceScope.ActivationCandidate, null,
            [new AlternativeEvidence("A", Start), new SyntheticEvidence(1m, Start)]);
        Assert.Equal(ordered.Evidence.Select(item => item.GetType()), reversed.Evidence.Select(item => item.GetType()));
    }

    [Fact]
    public void IdentityMismatchIsRejectedBeforeEvidenceReachesPolicy()
    {
        var mismatches = new StrategyReplayLifecycleEvidenceSnapshot[]
        {
            new(new("other"), Version, Provider, Symbol, 1, Start.AddMinutes(1), StrategyReplayLifecycleEvidenceScope.ActivationCandidate, null, [new SyntheticEvidence(1m, Start)]),
            new(Strategy, new("v2"), Provider, Symbol, 1, Start.AddMinutes(1), StrategyReplayLifecycleEvidenceScope.ActivationCandidate, null, [new SyntheticEvidence(1m, Start)]),
            new(Strategy, Version, new("other"), Symbol, 1, Start.AddMinutes(1), StrategyReplayLifecycleEvidenceScope.ActivationCandidate, null, [new SyntheticEvidence(1m, Start)]),
            new(Strategy, Version, Provider, new("OTHER"), 1, Start.AddMinutes(1), StrategyReplayLifecycleEvidenceScope.ActivationCandidate, null, [new SyntheticEvidence(1m, Start)]),
        };

        foreach (var mismatch in mismatches)
        {
            var policy = StartPolicy();
            Assert.Throws<ArgumentException>(() => UseCase(policy).Execute(Definition, Series(100m), new FixedProducer(mismatch)));
            Assert.Empty(policy.Contexts);
        }
    }

    [Fact]
    public void ExistingPolicyReceivesAlignedEmptyEvidenceWithoutAProducer()
    {
        var policy = StartPolicy();

        var run = UseCase(policy).Execute(Definition, Series(100m));

        var context = Assert.Single(policy.Contexts);
        Assert.Equal(StrategyReplayLifecycleEvidenceScope.Empty, context.Evidence.Scope);
        Assert.Empty(context.Evidence.Evidence);
        Assert.Empty(run.StrategyObservations[0].LifecycleProgression!.ActiveInstance!.Evidence.Evidence);
    }

    [Fact]
    public void TemporalObservabilityCanTravelWithoutImplyingALifecycleTransition()
    {
        var policy = new RecordingPolicy(_ => StrategyReplayLifecycleTransition.None);
        var producer = new TemporalEvidenceProducer();

        var run = UseCase(policy).Execute(Definition, Series(100m), producer);

        var context = Assert.Single(policy.Contexts);
        Assert.True(context.Evidence.TryGet<StrategyReplayLifecycleTemporalObservabilityEvidence>(out var evidence));
        Assert.Equal(ReplayMarketDataObservabilityStatus.ResolutionInsufficient, evidence!.Status);
        Assert.Empty(run.StrategyObservations[0].LifecycleProgression!.Instances);
        Assert.Empty(run.StrategyObservations[0].LifecycleProgression!.TransitionHistory);
    }

    private static GenerateMultiTimeframeStrategyBacktestRunUseCase UseCase(RecordingPolicy policy) => new(
        new RunMultiTimeframeReplayUseCase(),
        new CreateStrategyReplayContextUseCase(),
        new EvaluateStrategyReplayContextUseCase([new PassedEvaluator()]),
        new([Definition], [Workflow]),
        new AdvanceStrategyReplayProgressionUseCase(),
        new([Definition], [policy]),
        new(new()));

    private static RecordingPolicy StartPolicy() => new(context => context.Observation.Step == 1
        ? StrategyReplayLifecycleTransition.Start(new("R1"))
        : StrategyReplayLifecycleTransition.None);

    private static CandleSeries[] Series(params decimal[] closes) => [new(Provider, Symbol, Minute,
        closes.Select((close, index) => new Candle(
            Provider, Symbol, Minute, Start.AddMinutes(index), Start.AddMinutes(index + 1),
            close, close, close, close, null)))];

    private static StrategyReplayLifecycleEvidenceSnapshot Snapshot(
        int step,
        StrategyReplayLifecycleEvidenceScope scope,
        StrategyReplayProgressionInstanceId? instanceId,
        IEnumerable<IStrategyReplayLifecycleEvidence> evidence) => new(
            Strategy, Version, Provider, Symbol, step, Start.AddMinutes(step), scope, instanceId, evidence);

    private static string Signature(StrategyReplayLifecycleEvidenceSnapshot snapshot) => string.Join('|',
        snapshot.Step, snapshot.AsOfUtc, snapshot.Scope, snapshot.InstanceId,
        string.Join(';', snapshot.Evidence.Select(item => item is SyntheticEvidence value
            ? $"{value.Value}:{value.ObservedAtUtc:o}"
            : item.ToString())));

    private sealed record SyntheticEvidence(decimal Value, DateTimeOffset ObservedAtUtc) : IStrategyReplayLifecycleEvidence;
    private sealed record AlternativeEvidence(string Value, DateTimeOffset ObservedAtUtc) : IStrategyReplayLifecycleEvidence;

    private sealed class CloseEvidenceProducer : IStrategyReplayLifecycleEvidenceProducer
    {
        public StrategyId StrategyId => Strategy;
        public StrategyVersion StrategyVersion => Version;

        public StrategyReplayLifecycleEvidenceSnapshot Capture(StrategyReplayLifecycleEvidenceProductionContext context)
        {
            Assert.True(context.ReplayContext.TryGetFrame(Minute, out var frame));
            var close = frame!.AvailableCandles[^1].Close;
            var active = context.PreviousLifecycle?.ActiveInstance;
            return Snapshot(context.Observation.Step,
                active is null ? StrategyReplayLifecycleEvidenceScope.ActivationCandidate : StrategyReplayLifecycleEvidenceScope.ActiveInstance,
                active?.InstanceId,
                [new SyntheticEvidence(close, context.AsOfUtc)]);
        }
    }

    private sealed class FixedProducer(StrategyReplayLifecycleEvidenceSnapshot snapshot) : IStrategyReplayLifecycleEvidenceProducer
    {
        public StrategyId StrategyId => Strategy;
        public StrategyVersion StrategyVersion => Version;
        public StrategyReplayLifecycleEvidenceSnapshot Capture(StrategyReplayLifecycleEvidenceProductionContext context) => snapshot;
    }

    private sealed class TemporalEvidenceProducer : IStrategyReplayLifecycleEvidenceProducer
    {
        public StrategyId StrategyId => Strategy;
        public StrategyVersion StrategyVersion => Version;

        public StrategyReplayLifecycleEvidenceSnapshot Capture(StrategyReplayLifecycleEvidenceProductionContext context)
        {
            var window = new ReplayTemporalEvidenceWindow("first", context.AsOfUtc.AddMinutes(-1), context.AsOfUtc);
            var other = new ReplayTemporalEvidenceWindow("second", context.AsOfUtc.AddMinutes(-1), context.AsOfUtc);
            return Snapshot(context.Observation.Step, StrategyReplayLifecycleEvidenceScope.ActivationCandidate, null,
                [new StrategyReplayLifecycleTemporalObservabilityEvidence(
                    context.AsOfUtc, window, other, ReplayMarketDataObservabilityStatus.ResolutionInsufficient)]);
        }
    }

    private sealed class RecordingPolicy(
        Func<StrategyReplayLifecyclePolicyContext, StrategyReplayLifecycleTransition> decide) : IStrategyReplayLifecyclePolicy
    {
        public StrategyId StrategyId => Strategy;
        public StrategyVersion StrategyVersion => Version;
        public List<StrategyReplayLifecyclePolicyContext> Contexts { get; } = [];

        public StrategyReplayLifecycleTransition Decide(StrategyReplayLifecyclePolicyContext context)
        {
            Contexts.Add(context);
            return decide(context);
        }
    }

    private sealed class PassedEvaluator : IReplayRuleEvaluator
    {
        public StrategyId StrategyId => Strategy;
        public StrategyVersion StrategyVersion => Version;
        public RuleId RuleId { get; } = new("A");
        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context) =>
            new(RuleEvaluationResult.Passed, "Synthetic.", null);
    }
}

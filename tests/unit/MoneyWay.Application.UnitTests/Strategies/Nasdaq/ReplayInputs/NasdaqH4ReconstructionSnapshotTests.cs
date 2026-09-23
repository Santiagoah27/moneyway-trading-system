using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqH4ReconstructionSnapshotTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private static readonly StrategyDefinition Definition = MoneyWayNasdaqStrategyDefinition.Instance;

    [Fact]
    public void SevenActiveVariantsPreserveExactStateEpisodeAndMarketCursor()
    {
        var fixture = CreateFixture();
        NasdaqH4ReconstructionSnapshot[] snapshots =
        [
            new NasdaqH4ReconstructionSnapshot.InvalidatedAwaitingOrigin(fixture.Awaiting),
            new NasdaqH4ReconstructionSnapshot.OppositeImpulse(fixture.Impulse),
            new NasdaqH4ReconstructionSnapshot.Correction(fixture.Correction),
            new NasdaqH4ReconstructionSnapshot.Candidate(fixture.Candidate),
            new NasdaqH4ReconstructionSnapshot.RebuildPending(fixture.Pending),
            new NasdaqH4ReconstructionSnapshot.RebuiltTracking(fixture.Tracking),
            new NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion(fixture.Breakout),
        ];
        Assert.Equal(7, snapshots.Select(snapshot => snapshot.Kind).Distinct().Count());
        Assert.Equal(8, Enum.GetValues<NasdaqH4ReconstructionSnapshotKind>().Length);
        Assert.All(snapshots, snapshot =>
        {
            Assert.Equal(fixture.Episode, snapshot.Episode);
            Assert.Same(Definition.Version, snapshot.StrategyVersion);
            Assert.Equal(H4, snapshot.MarketCursor.Timeframe);
        });

        Assert.Same(fixture.Awaiting, Assert.IsType<NasdaqH4ReconstructionSnapshot.InvalidatedAwaitingOrigin>(snapshots[0]).State);
        Assert.Same(fixture.Awaiting.InvalidatingCandle, snapshots[0].MarketCursor);
        Assert.Same(fixture.Impulse, Assert.IsType<NasdaqH4ReconstructionSnapshot.OppositeImpulse>(snapshots[1]).State);
        Assert.Same(fixture.Impulse.LastProcessedCandle, snapshots[1].MarketCursor);
        Assert.Same(fixture.Correction, Assert.IsType<NasdaqH4ReconstructionSnapshot.Correction>(snapshots[2]).State);
        Assert.Same(fixture.Correction.LastProcessedCandle, snapshots[2].MarketCursor);
        Assert.Same(fixture.Candidate, Assert.IsType<NasdaqH4ReconstructionSnapshot.Candidate>(snapshots[3]).State);
        Assert.Same(fixture.Candidate.TerminalCandle, snapshots[3].MarketCursor);
        Assert.Same(fixture.Pending, Assert.IsType<NasdaqH4ReconstructionSnapshot.RebuildPending>(snapshots[4]).State);
        Assert.Same(fixture.Pending.LastProcessedCandle, snapshots[4].MarketCursor);
        Assert.Same(fixture.Tracking, Assert.IsType<NasdaqH4ReconstructionSnapshot.RebuiltTracking>(snapshots[5]).State);
        Assert.Same(fixture.Tracking.LastProcessedCandle, snapshots[5].MarketCursor);
        Assert.Same(fixture.Breakout, Assert.IsType<NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion>(snapshots[6]).State);
        Assert.Same(fixture.Breakout.ValidatingCandle, snapshots[6].MarketCursor);
        Assert.IsNotType<NasdaqH4ReconstructionSnapshot.Candidate>(snapshots[6]);
    }

    [Fact]
    public void PreOriginRequiresVerifiedMatchingInvalidation()
    {
        var fixture = CreateFixture();
        var wrongEpisode = new NasdaqHumanOriginVertexEpisode(Definition.StrategyId, Definition.Version,
            Provider, Symbol, fixture.Awaiting.InvalidatingCandle.OpenTimeUtc.AddHours(4));

        Assert.Throws<ArgumentException>(() => new NasdaqH4InvalidatedAwaitingOriginState(wrongEpisode, fixture.Awaiting.Invalidation));
        Assert.Same(fixture.Awaiting.Invalidation.CurrentCandle, fixture.Awaiting.LastProcessedCandle);
        Assert.Equal(StructuralCandidateTurnBoundaryKind.StructureInvalidated, fixture.Awaiting.Invalidation.Kind);
    }

    [Fact]
    public void PreEvaluationTransportIsExplicitAndFrameLocalWithoutBecomingHumanInput()
    {
        var fixture = CreateFixture();
        var first = new NasdaqH4ReconstructionSnapshot.InvalidatedAwaitingOrigin(fixture.Awaiting);
        var second = new NasdaqH4ReconstructionSnapshot.OppositeImpulse(fixture.Impulse);
        var frames = Frames(fixture.Origin, fixture.OldCandidate, fixture.Awaiting.InvalidatingCandle, fixture.Correction.CorrectionStartCandle);
        var laterHuman = new NasdaqHumanOriginVertexObservation(Definition.StrategyId, Definition.Version,
            Provider, Symbol, fixture.Awaiting.InvalidatingCandle.OpenTimeUtc, [fixture.Origin.OpenTimeUtc],
            frames[3].AsOfUtc, "review:later");
        var factory = new CreateStrategyReplayContextUseCase();

        var t1 = factory.ExecuteWithPreEvaluationState(Definition, frames[2], first, [laterHuman]);
        var t2 = factory.ExecuteWithPreEvaluationState(Definition, frames[3], second, [laterHuman]);
        var absent = factory.Execute(Definition, frames[2]);

        Assert.True(t1.TryGetPreEvaluationState<NasdaqH4ReconstructionSnapshot>(out var atT1));
        Assert.Same(first, atT1);
        Assert.Same(second, t2.PreEvaluationState);
        Assert.Same(first, t1.PreEvaluationState);
        Assert.Empty(t1.InputObservations);
        Assert.Single(t2.InputObservations);
        Assert.Null(absent.PreEvaluationState);
        Assert.False(absent.TryGetPreEvaluationState<NasdaqH4ReconstructionSnapshot>(out _));
        Assert.Same(fixture.Awaiting.InvalidatingCandle, first.MarketCursor);
        Assert.Same(fixture.Impulse.LastProcessedCandle, second.MarketCursor);

        var probe = new SnapshotProbeEvaluator();
        var probeDefinition = new StrategyDefinition(Definition.StrategyId, Definition.Version, "Probe", "probe",
            [new(new("TEST-SNAPSHOT"), "Probe", "test", 1, false, RuleDefinitionStatus.Confirmed, "probe", "test")]);
        var useCase = new EvaluateStrategyReplayContextUseCase([probe]);
        var t1Observation = useCase.Execute(probeDefinition, t1);
        var t2Observation = useCase.Execute(probeDefinition, t2);
        Assert.Contains("InvalidatedAwaitingOrigin", Assert.Single(t1Observation.Evaluations).Reason);
        Assert.Contains("OppositeImpulse", Assert.Single(t2Observation.Evaluations).Reason);
        Assert.DoesNotContain("PreEvaluationState", t1Observation.GetType().GetProperties().Select(property => property.Name));
        Assert.Same(first, t1.PreEvaluationState);
    }

    [Fact]
    public void TransportRejectsDifferentStrategyVersionAndInstrument()
    {
        var fixture = CreateFixture();
        var snapshot = new NasdaqH4ReconstructionSnapshot.InvalidatedAwaitingOrigin(fixture.Awaiting);
        var frame = Frames(fixture.Origin, fixture.OldCandidate, fixture.Awaiting.InvalidatingCandle)[2];
        var factory = new CreateStrategyReplayContextUseCase();
        var differentVersion = new StrategyDefinition(Definition.StrategyId, new("different"), "Other", "other",
            [new(new("TEST"), "Test", "test", 1, false, RuleDefinitionStatus.Confirmed, "test", "test")]);

        Assert.Throws<ArgumentException>(() => factory.ExecuteWithPreEvaluationState(differentVersion, frame, snapshot));
        var otherSymbol = new MarketSymbol("OTHER");
        var otherCandle = new Candle(Provider, otherSymbol, H4, Start, Start.AddHours(4),
            100, 110, 90, 100, null);
        var otherCursor = new MultiTimeframeCandleReplayCursor(
            [new CandleSeries(Provider, otherSymbol, H4, [otherCandle])]);
        otherCursor.TryAdvance(out var otherFrame);
        Assert.Throws<ArgumentException>(() => factory.ExecuteWithPreEvaluationState(Definition, otherFrame!, snapshot));
        Assert.Throws<ArgumentNullException>(() => factory.ExecuteWithPreEvaluationState(Definition, frame, null!));
    }

    [Fact]
    public void CanonicalPriceEnhancedContextCarriesOnlyTheExplicitSnapshot()
    {
        var fixture = CreateFixture();
        var state = new NasdaqH4ReconstructionSnapshot.InvalidatedAwaitingOrigin(fixture.Awaiting);
        var series = new CandleSeries(Provider, Symbol, H4,
            [fixture.Origin, fixture.OldCandidate, fixture.Awaiting.InvalidatingCandle]);
        var cursor = new CanonicalMultiTimeframeReplayCursor([series],
            new HistoricalMarketPriceObservationSeries(Provider, Symbol, []));
        CanonicalMultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;

        var context = new CreateStrategyReplayContextUseCase()
            .ExecuteCanonicalWithPreEvaluationState(Definition, frame!, state);

        Assert.Same(state, context.PreEvaluationState);
        Assert.Empty(context.InputObservations);
        Assert.Same(fixture.Awaiting.InvalidatingCandle, state.MarketCursor);
    }

    private static Fixture CreateFixture()
    {
        var origin = Candle(0, 100, 110, 95, 110);
        var oldCandidate = Candle(4, 95, 100, 90, 96);
        var invalidating = Candle(8, 100, 110, 80, 90);
        var observation = new NasdaqHumanOriginVertexObservation(Definition.StrategyId, Definition.Version,
            Provider, Symbol, invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var frame = Frames(origin, oldCandidate, invalidating)[2];
        var context = new CreateStrategyReplayContextUseCase().Execute(Definition, frame, [observation]);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(
            context, H4, 130, 95, 140, [oldCandidate], StructuralCandidateExtremeSide.Lower);
        var episode = new NasdaqHumanOriginVertexEpisode(Definition.StrategyId, Definition.Version,
            Provider, Symbol, invalidating.OpenTimeUtc);
        var awaiting = new NasdaqH4InvalidatedAwaitingOriginState(episode, boundary);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var geometry = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, geometry);
        var start = Candle(12, 70, 105, 65, 95);
        var correction = new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, start));
        var candidate = new NasdaqPostInvalidationCorrectionTransitionCalculator()
            .Evaluate(correction, Candle(16, 100, 130, 90, 99)).Candidate!;
        var pending = new NasdaqPostInvalidationCandidateRebuildTransitionCalculator()
            .Evaluate(candidate, Candle(20, 100, 140, 80, 100));
        var tracking = new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator()
            .Evaluate(pending, Candle(24, 100, 140, 80, 90)).Tracking!;
        var breakout = new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator()
            .Evaluate(pending, Candle(24, 100, 140, 50, 69)).Breakout!;
        return new(origin, oldCandidate, awaiting, impulse, correction, candidate, pending, tracking, breakout, members.Episode);
    }

    private static List<MultiTimeframeReplayFrame> Frames(params Candle[] candles)
    {
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, candles)]);
        var frames = new List<MultiTimeframeReplayFrame>();
        while (cursor.TryAdvance(out var frame)) frames.Add(frame!);
        return frames;
    }

    private static Candle Candle(int hour, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, H4, Start.AddHours(hour), Start.AddHours(hour + 4), open, high, low, close, null);

    private sealed record Fixture(Candle Origin, Candle OldCandidate, NasdaqH4InvalidatedAwaitingOriginState Awaiting,
        NasdaqPostInvalidationOppositeImpulseState Impulse, NasdaqPostInvalidationCorrectionState Correction,
        NasdaqPostInvalidationCandidateState Candidate, NasdaqPostInvalidationCandidateRebuildPendingState Pending,
        NasdaqPostInvalidationRebuiltCandidateTrackingState Tracking,
        NasdaqPostInvalidationCandidateRebuildBreakoutState Breakout, NasdaqHumanOriginVertexEpisode Episode);

    private sealed class SnapshotProbeEvaluator : IReplayRuleEvaluator
    {
        public StrategyId StrategyId => Definition.StrategyId;
        public StrategyVersion StrategyVersion => Definition.Version;
        public RuleId RuleId { get; } = new("TEST-SNAPSHOT");
        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
        {
            if (!context.TryGetPreEvaluationState<NasdaqH4ReconstructionSnapshot>(out var snapshot))
                return new(RuleEvaluationResult.NotApplicable, "No snapshot supplied.", null);
            return new(RuleEvaluationResult.NotApplicable, $"Snapshot: {snapshot.Kind}.", null);
        }
    }
}

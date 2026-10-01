using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqH4InvalidatedOriginEvidenceSnapshotReducerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private static readonly StrategyDefinition Definition = MoneyWayNasdaqStrategyDefinition.Instance;
    private readonly NasdaqH4InvalidatedOriginEvidenceSnapshotReducer reducer = new();

    [Fact]
    public void MissingAndFutureEvidencePreserveTheExactFrozenSnapshot()
    {
        var fixture = CreateFixture();
        var future = Observation([0], 20, "review:future");
        var withoutFuture = reducer.Reduce(fixture.Snapshot, Context(fixture, 12));
        var withFuture = reducer.Reduce(fixture.Snapshot, Context(fixture, 12, future));

        var missing = Assert.IsType<NasdaqH4InvalidatedOriginEvidenceReductionResult.Missing>(withFuture);
        Assert.IsType<NasdaqH4InvalidatedOriginEvidenceReductionResult.Missing>(withoutFuture);
        Assert.Same(fixture.Snapshot, missing.Snapshot);
        Assert.Same(fixture.Snapshot.State, missing.PendingSnapshot.State);
        Assert.Same(fixture.Invalidating, missing.Snapshot.MarketCursor);
        Assert.Equal(NasdaqHumanOriginVertexObservationSelectionKind.Missing, missing.Selection.Kind);
        Assert.Empty(missing.Selection.SupportingObservations);
        Assert.Equal(withoutFuture.Selection.DistinctMemberships, missing.Selection.DistinctMemberships);
    }

    [Fact]
    public void ImmediateUniqueResolvesExactMembersAndInitializesImpulseAtInvalidation()
    {
        var fixture = CreateFixture();
        var observation = Observation([0], 12, "review:origin");

        var resolved = Assert.IsType<NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved>(
            reducer.Reduce(fixture.Snapshot, Context(fixture, 12, observation)));

        Assert.Equal(NasdaqHumanOriginVertexObservationSelectionKind.Unique, resolved.Selection.Kind);
        Assert.Same(observation, Assert.Single(resolved.Selection.SupportingObservations));
        Assert.Same(fixture.Origin, Assert.Single(resolved.Members.SelectedMembers));
        Assert.Equal([fixture.Origin.OpenTimeUtc], resolved.Selection.SemanticMemberOpenTimesUtc);
        Assert.Equal(fixture.Snapshot.Episode, resolved.Members.Episode);
        Assert.Same(resolved.OriginGeometry, resolved.State.OriginGeometry);
        Assert.Same(resolved.State, resolved.ImpulseSnapshot.State);
        Assert.Equal(NasdaqH4ReconstructionSnapshotKind.OppositeImpulse, resolved.Snapshot.Kind);
        Assert.Equal(fixture.Snapshot.Episode, resolved.Snapshot.Episode);
        Assert.Same(fixture.Invalidating, resolved.Snapshot.MarketCursor);
        Assert.Same(fixture.Invalidating, resolved.State.LastProcessedCandle);
    }

    [Fact]
    public void LateUniqueResolvesOriginalEpisodeWithoutConsumingObservableBacklogOrRewritingEarlierResult()
    {
        var fixture = CreateFixture();
        var observation = Observation([0], 20, "review:late");
        var early = Assert.IsType<NasdaqH4InvalidatedOriginEvidenceReductionResult.Missing>(
            reducer.Reduce(fixture.Snapshot, Context(fixture, 16, observation)));
        var later = Assert.IsType<NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved>(
            reducer.Reduce(fixture.Snapshot, Context(fixture, 20, observation)));

        Assert.Same(fixture.Snapshot, early.Snapshot);
        Assert.Empty(early.Selection.SupportingObservations);
        Assert.Same(fixture.Invalidating, early.Snapshot.MarketCursor);
        Assert.Same(observation, Assert.Single(later.Selection.SupportingObservations));
        Assert.Same(fixture.Invalidating, later.Snapshot.MarketCursor);
        Assert.Same(fixture.Invalidating, later.State.LastProcessedCandle);
        Assert.Same(fixture.Invalidating, later.State.InvalidatingCandle);
        Assert.Equal(fixture.Snapshot.Episode, later.Snapshot.Episode);
    }

    [Fact]
    public void CompatibleDuplicatesRetainAllProvenanceWithoutSelectingReviewerAuthority()
    {
        var fixture = CreateFixture();
        var first = Observation([0], 12, "review:first");
        var second = Observation([0], 20, "review:second");
        var resolved = Assert.IsType<NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved>(
            reducer.Reduce(fixture.Snapshot, Context(fixture, 20, second, first)));

        Assert.Equal([first, second], resolved.Selection.SupportingObservations);
        Assert.Single(resolved.Selection.DistinctMemberships);
        Assert.Same(fixture.Origin, Assert.Single(resolved.Members.SelectedMembers));
        Assert.Same(resolved.OriginGeometry, resolved.State.OriginGeometry);
        Assert.Same(fixture.Invalidating, resolved.Snapshot.MarketCursor);
    }

    [Fact]
    public void ConflictAndAdditionalLaterEvidenceNeverSelectAWinner()
    {
        var fixture = CreateFixture();
        var first = Observation([0], 12, "review:first");
        var conflicting = Observation([4], 16, "review:conflicting");
        var additional = Observation([0], 20, "review:additional");
        var atT1 = Assert.IsType<NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved>(
            reducer.Reduce(fixture.Snapshot, Context(fixture, 12, first, conflicting, additional)));
        var atT2 = Assert.IsType<NasdaqH4InvalidatedOriginEvidenceReductionResult.Conflict>(
            reducer.Reduce(fixture.Snapshot, Context(fixture, 16, first, conflicting, additional)));
        var atT3 = Assert.IsType<NasdaqH4InvalidatedOriginEvidenceReductionResult.Conflict>(
            reducer.Reduce(fixture.Snapshot, Context(fixture, 20, first, conflicting, additional)));

        Assert.Single(atT1.Selection.SupportingObservations);
        Assert.Same(fixture.Snapshot, atT2.Snapshot);
        Assert.Same(fixture.Snapshot.State, atT2.PendingSnapshot.State);
        Assert.Same(fixture.Invalidating, atT2.Snapshot.MarketCursor);
        Assert.Equal(NasdaqHumanOriginVertexObservationSelectionKind.Conflict, atT2.Selection.Kind);
        Assert.Equal([first, conflicting], atT2.Selection.SupportingObservations);
        Assert.Equal(2, atT2.Selection.DistinctMemberships.Count);
        Assert.Same(fixture.Snapshot, atT3.Snapshot);
        Assert.Equal([first, conflicting, additional], atT3.Selection.SupportingObservations);
        Assert.Equal(2, atT3.Selection.DistinctMemberships.Count);
    }

    [Fact]
    public void InputOrderDoesNotChangeUniqueOrConflictStructuralOutcome()
    {
        var fixture = CreateFixture();
        var first = Observation([0], 12, "review:first");
        var duplicate = Observation([0], 16, "review:duplicate");
        var conflicting = Observation([4], 20, "review:conflicting");
        var one = Assert.IsType<NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved>(
            reducer.Reduce(fixture.Snapshot, Context(fixture, 16, first, duplicate)));
        var reverse = Assert.IsType<NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved>(
            reducer.Reduce(fixture.Snapshot, Context(fixture, 16, duplicate, first)));
        var conflict = Assert.IsType<NasdaqH4InvalidatedOriginEvidenceReductionResult.Conflict>(
            reducer.Reduce(fixture.Snapshot, Context(fixture, 20, conflicting, duplicate, first)));

        Assert.Equal(one.Selection.SupportingObservations, reverse.Selection.SupportingObservations);
        Assert.Equal(one.Members.SelectedMembers, reverse.Members.SelectedMembers);
        Assert.Equal(one.OriginGeometry.StructuralPrice, reverse.OriginGeometry.StructuralPrice);
        Assert.Equal(one.OriginGeometry.ProtectionAnchor, reverse.OriginGeometry.ProtectionAnchor);
        Assert.Equal(NasdaqHumanOriginVertexObservationSelectionKind.Conflict, conflict.Selection.Kind);
    }

    [Fact]
    public void RejectsEveryOtherSnapshotVariantAndAContextBeforeInvalidationCloses()
    {
        var fixture = CreateFixture();
        var observation = Observation([0], 12, "review:origin");
        var context = Context(fixture, 20, observation);
        var resolved = Assert.IsType<NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved>(
            reducer.Reduce(fixture.Snapshot, context));
        var impulse = resolved.State;
        var correction = new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, fixture.Later1));
        var candidate = new NasdaqPostInvalidationCorrectionTransitionCalculator()
            .Evaluate(correction, fixture.Later2).Candidate!;
        var pending = new NasdaqPostInvalidationCandidateRebuildTransitionCalculator()
            .Evaluate(candidate, Candle(20, 100, 140, 80, 100));
        var tracking = new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator()
            .Evaluate(pending, Candle(24, 100, 140, 80, 90)).Tracking!;
        var breakout = new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator()
            .Evaluate(pending, Candle(24, 100, 140, 50, 69)).Breakout!;
        var validating = Candle(20, 100, 140, 50, 69);
        var constructor = typeof(NasdaqPostInvalidationCandidateRebuildBreakoutState)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(item => item.GetParameters()[0].ParameterType == typeof(NasdaqPostInvalidationCandidateState));
        var directionalBreakout = (NasdaqPostInvalidationCandidateRebuildBreakoutState)constructor.Invoke(
            [candidate, validating, validating.High,
                NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody]);
        NasdaqH4ReconstructionSnapshot[] invalid =
        [
            resolved.ImpulseSnapshot,
            new NasdaqH4ReconstructionSnapshot.Correction(correction),
            new NasdaqH4ReconstructionSnapshot.Candidate(candidate),
            new NasdaqH4ReconstructionSnapshot.RebuildPending(pending),
            new NasdaqH4ReconstructionSnapshot.RebuiltTracking(tracking),
            new NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion(breakout),
            new NasdaqH4ReconstructionSnapshot.Completed.Directional(
                new NasdaqDirectionalMigrationBreakoutCompletionCalculator().Evaluate(directionalBreakout)),
        ];

        Assert.All(invalid, snapshot => Assert.Throws<ArgumentException>(() => reducer.Reduce(snapshot, context)));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(fixture.Snapshot, Context(fixture, 8)));
    }

    private static Fixture CreateFixture()
    {
        var origin = Candle(0, 100, 110, 95, 110);
        var oldCandidate = Candle(4, 95, 100, 90, 96);
        var invalidating = Candle(8, 100, 110, 80, 90);
        var later1 = Candle(12, 70, 105, 65, 95);
        var later2 = Candle(16, 100, 130, 90, 99);
        var frame = Frames(origin, oldCandidate, invalidating, later1, later2)[2];
        var context = new CreateStrategyReplayContextUseCase().Execute(Definition, frame);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(
            context, H4, 130, 95, 140, [oldCandidate], StructuralCandidateExtremeSide.Lower);
        var episode = new NasdaqHumanOriginVertexEpisode(Definition.StrategyId, Definition.Version,
            Provider, Symbol, invalidating.OpenTimeUtc);
        var snapshot = new NasdaqH4ReconstructionSnapshot.InvalidatedAwaitingOrigin(
            new NasdaqH4InvalidatedAwaitingOriginState(episode, boundary));
        return new(origin, invalidating, later1, later2, snapshot);
    }

    private static StrategyReplayContext Context(Fixture fixture, int asOfHour,
        params IStrategyReplayInputObservation[] observations)
    {
        var oldCandidate = Candle(4, 95, 100, 90, 96);
        var frame = Frames(fixture.Origin, oldCandidate, fixture.Invalidating, fixture.Later1, fixture.Later2)
            .Single(item => item.AsOfUtc == Start.AddHours(asOfHour));
        return new CreateStrategyReplayContextUseCase().Execute(Definition, frame, observations);
    }

    private static NasdaqHumanOriginVertexObservation Observation(int[] members, int observedAtHour, string source) => new(
        Definition.StrategyId, Definition.Version, Provider, Symbol, Start.AddHours(8),
        members.Select(hour => Start.AddHours(hour)), Start.AddHours(observedAtHour), source);

    private static List<MultiTimeframeReplayFrame> Frames(params Candle[] candles)
    {
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, candles)]);
        var frames = new List<MultiTimeframeReplayFrame>();
        while (cursor.TryAdvance(out var frame)) frames.Add(frame!);
        return frames;
    }

    private static Candle Candle(int hour, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, H4, Start.AddHours(hour), Start.AddHours(hour + 4), open, high, low, close, null);

    private sealed record Fixture(Candle Origin, Candle Invalidating, Candle Later1, Candle Later2,
        NasdaqH4ReconstructionSnapshot.InvalidatedAwaitingOrigin Snapshot);
}

using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqH4ReconstructionCatchUpOrchestratorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqH4ReconstructionCatchUpOrchestrator orchestrator = new();

    [Fact]
    public void NoBacklogPreservesExactImpulseAndCorrectionSnapshots()
    {
        var fixture = Fixture.Create();
        var impulse = orchestrator.CatchUp(fixture.Impulse, Context(12, fixture.BaseCandles));
        var correction = new NasdaqH4OppositeImpulseSnapshotReducer().Reduce(fixture.Impulse, fixture.CorrectionStart);
        var correctionResult = orchestrator.CatchUp(correction,
            Context(16, [.. fixture.BaseCandles, fixture.CorrectionStart]));

        Assert.Same(fixture.Impulse, Assert.IsType<NasdaqH4ReconstructionCatchUpResult.UpToDate>(impulse).Snapshot);
        Assert.Same(correction, Assert.IsType<NasdaqH4ReconstructionCatchUpResult.UpToDate>(correctionResult).Snapshot);
        Assert.Same(fixture.Invalidating, fixture.Impulse.MarketCursor);
        Assert.Same(fixture.CorrectionStart, correction.MarketCursor);
    }

    [Fact]
    public void OneContinuationCandleUsesExactFocusedReductionAndAdvancesCursor()
    {
        var fixture = Fixture.Create();
        var continuation = Candle(12, 90, 95, 78, 85);
        var expected = Assert.IsType<NasdaqH4ReconstructionSnapshot.OppositeImpulse>(
            new NasdaqH4OppositeImpulseSnapshotReducer().Reduce(fixture.Impulse, continuation));

        var result = Assert.IsType<NasdaqH4ReconstructionCatchUpResult.UpToDate>(orchestrator.CatchUp(
            fixture.Impulse, Context(16, [.. fixture.BaseCandles, continuation])));

        var actual = Assert.IsType<NasdaqH4ReconstructionSnapshot.OppositeImpulse>(result.Snapshot);
        Assert.Same(continuation, actual.MarketCursor);
        Assert.Equal(expected.State.ProvisionalTerminal.StructuralPrice, actual.State.ProvisionalTerminal.StructuralPrice);
        Assert.Same(fixture.Invalidating, fixture.Impulse.MarketCursor);
    }

    [Fact]
    public void MultiStateBacklogConsumesOneCandlePerReducerInChronologicalOrder()
    {
        var fixture = Fixture.Create();
        var expectedCorrection = new NasdaqH4OppositeImpulseSnapshotReducer()
            .Reduce(fixture.Impulse, fixture.CorrectionStart);
        var expectedCandidate = new NasdaqH4CorrectionSnapshotReducer()
            .Reduce(expectedCorrection, fixture.CandidateTurn);

        var result = Assert.IsType<NasdaqH4ReconstructionCatchUpResult.UpToDate>(orchestrator.CatchUp(
            fixture.Impulse, Context(20, [.. fixture.BaseCandles, fixture.CorrectionStart, fixture.CandidateTurn])));

        var candidate = Assert.IsType<NasdaqH4ReconstructionSnapshot.Candidate>(result.Snapshot);
        var manual = Assert.IsType<NasdaqH4ReconstructionSnapshot.Candidate>(expectedCandidate);
        Assert.Same(fixture.CandidateTurn, candidate.MarketCursor);
        Assert.Same(fixture.CorrectionStart, Assert.IsType<NasdaqH4ReconstructionSnapshot.Correction>(expectedCorrection).MarketCursor);
        Assert.Equal(manual.State.CandidateGeometry.StructuralPrice, candidate.State.CandidateGeometry.StructuralPrice);
        Assert.Equal(manual.State.CandidateGeometry.ProtectionAnchor, candidate.State.CandidateGeometry.ProtectionAnchor);
        Assert.Same(fixture.Invalidating, fixture.Impulse.MarketCursor);
    }

    [Fact]
    public void LateOriginWaitsThenResolvesAndFoldsCurrentBacklogWithoutRewritingEarlierResult()
    {
        var fixture = Fixture.Create();
        var evidence = OriginObservation(fixture, [fixture.Origin.OpenTimeUtc], 24, "review:late");
        var early = Assert.IsType<NasdaqH4ReconstructionCatchUpResult.OriginEvidenceMissing>(
            orchestrator.CatchUp(fixture.Awaiting, Context(20,
                [.. fixture.BaseCandles, fixture.CorrectionStart, fixture.CandidateTurn], evidence)));
        var later = Assert.IsType<NasdaqH4ReconstructionCatchUpResult.UpToDate>(
            orchestrator.CatchUp(fixture.Awaiting, Context(24,
                [.. fixture.BaseCandles, fixture.CorrectionStart, fixture.CandidateTurn, fixture.Next], evidence)));
        var resolved = Assert.IsType<NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved>(
            new NasdaqH4InvalidatedOriginEvidenceSnapshotReducer().Reduce(fixture.Awaiting,
                Context(24, [.. fixture.BaseCandles, fixture.CorrectionStart, fixture.CandidateTurn, fixture.Next], evidence)));
        var afterFirst = new NasdaqH4OppositeImpulseSnapshotReducer().Reduce(resolved.Snapshot, fixture.CorrectionStart);
        var afterSecond = new NasdaqH4CorrectionSnapshotReducer().Reduce(afterFirst, fixture.CandidateTurn);
        var expected = new NasdaqH4CandidateSnapshotReducer().Reduce(afterSecond, fixture.Next);

        Assert.Same(fixture.Awaiting, early.Snapshot);
        Assert.Same(fixture.Invalidating, early.Snapshot.MarketCursor);
        Assert.Empty(early.Reduction.Selection.SupportingObservations);
        Assert.Equal(expected.Kind, later.Snapshot.Kind);
        Assert.Same(fixture.Next, later.Snapshot.MarketCursor);
        Assert.Same(evidence, Assert.Single(later.OriginResolution!.Selection.SupportingObservations));
        Assert.Same(fixture.Origin, Assert.Single(later.OriginResolution.Members.SelectedMembers));
        Assert.Same(fixture.Invalidating, later.OriginResolution.Snapshot.MarketCursor);
        Assert.Same(fixture.Awaiting, early.Snapshot);
    }

    [Fact]
    public void OriginConflictStopsBeforeBacklogAndPreservesAllEvidence()
    {
        var fixture = Fixture.Create();
        var first = OriginObservation(fixture, [fixture.Origin.OpenTimeUtc], 12, "review:first");
        var conflicting = OriginObservation(fixture, [fixture.OldCandidate.OpenTimeUtc], 16, "review:conflict");
        var result = Assert.IsType<NasdaqH4ReconstructionCatchUpResult.OriginEvidenceConflict>(
            orchestrator.CatchUp(fixture.Awaiting,
                Context(20, [.. fixture.BaseCandles, fixture.CorrectionStart, fixture.CandidateTurn], first, conflicting)));

        Assert.Same(fixture.Awaiting, result.Snapshot);
        Assert.Same(fixture.Invalidating, result.Snapshot.MarketCursor);
        Assert.Equal([first, conflicting], result.Reduction.Selection.SupportingObservations);
        Assert.Equal(2, result.Reduction.Selection.DistinctMemberships.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BreakoutEvidenceMissingOrConflictStopsBeforeLaterCandles(bool conflict)
    {
        var fixture = Fixture.Create(bearish: true);
        var collision = Candle(20, 140, 150, 50, 131);
        var later = Candle(24, 100, 140, 50, 90);
        var candidate = fixture.Candidate;
        var expected = Assert.IsType<NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion>(
            new NasdaqH4CandidateSnapshotReducer().Reduce(candidate, collision));
        Assert.Equal(NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired,
            expected.State.CollisionKind);
        var evidence = conflict
            ? new IStrategyReplayInputObservation[]
            {
                CollisionObservation(expected, 111m, 28, "review:first"),
                CollisionObservation(expected, 112m, 28, "review:second"),
            }
            : [];

        var result = orchestrator.CatchUp(candidate,
            Context(28, [.. fixture.BaseCandles, fixture.CorrectionStart, fixture.CandidateTurn, collision, later], evidence));

        Assert.Same(collision, result.Snapshot.MarketCursor);
        if (conflict)
        {
            var stopped = Assert.IsType<NasdaqH4ReconstructionCatchUpResult.BreakoutEvidenceConflict>(result);
            var branch = Assert.IsType<NasdaqH4BreakoutAwaitingCompletionReductionResult.Conflict.HumanStructuralPrice008>(stopped.Reduction);
            Assert.Equal(2, branch.Reduction.Selection.SupportingObservations.Count);
        }
        else
        {
            var stopped = Assert.IsType<NasdaqH4ReconstructionCatchUpResult.BreakoutEvidenceMissing>(result);
            Assert.IsType<NasdaqH4BreakoutAwaitingCompletionReductionResult.Missing.HumanStructuralPrice008>(stopped.Reduction);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CatchUpCompletes007Or008AndStopsBeforeRemainingBacklog(bool human008)
    {
        var fixture = Fixture.Create(bearish: true);
        var collision = human008 ? Candle(20, 140, 150, 50, 131) : Candle(20, 100, 140, 50, 131);
        var later = Candle(24, 100, 140, 50, 90);
        var breakout = Assert.IsType<NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion>(
            new NasdaqH4CandidateSnapshotReducer().Reduce(fixture.Candidate, collision));
        IStrategyReplayInputObservation[] observations = human008
            ? [CollisionObservation(breakout, 111m, 28, "review:unique")]
            : [];

        var result = Assert.IsType<NasdaqH4ReconstructionCatchUpResult.Completed>(orchestrator.CatchUp(
            fixture.Candidate,
            Context(28, [.. fixture.BaseCandles, fixture.CorrectionStart, fixture.CandidateTurn, collision, later], observations)));

        Assert.Same(collision, result.Snapshot.MarketCursor);
        Assert.NotNull(result.BreakoutCompletion);
        if (human008)
            Assert.IsType<NasdaqH4BreakoutAwaitingCompletionReductionResult.Completed.HumanStructuralPrice008>(result.BreakoutCompletion);
        else
            Assert.IsType<NasdaqH4BreakoutAwaitingCompletionReductionResult.Completed.Directional007>(result.BreakoutCompletion);
    }

    [Fact]
    public void AlreadyCompletedInputStopsWithoutRequiringAnH4Frame()
    {
        var fixture = Fixture.Create(bearish: true);
        var validating = Candle(20, 100, 140, 60, 131);
        var completed = Assert.IsType<NasdaqH4ReconstructionSnapshot.Completed.DirectCandidate>(
            new NasdaqH4CandidateSnapshotReducer().Reduce(fixture.Candidate, validating));
        var result = Assert.IsType<NasdaqH4ReconstructionCatchUpResult.Completed>(
            orchestrator.CatchUp(completed, MinuteOnlyContext(24)));

        Assert.Same(completed, result.Snapshot);
        Assert.Null(result.BreakoutCompletion);
        Assert.Same(validating, result.Snapshot.MarketCursor);
    }

    [Fact]
    public void CandidatePendingAndTrackingInputsFoldTheSameChronologicalMarketPath()
    {
        var fixture = Fixture.Create(bearish: true);
        var migration = Candle(20, 100, 140, 50, 90);
        var turn = Candle(24, 100, 140, 50, 101);
        var continuation = Candle(28, 100, 140, 50, 90);
        var pending = Assert.IsType<NasdaqH4ReconstructionSnapshot.RebuildPending>(
            new NasdaqH4CandidateSnapshotReducer().Reduce(fixture.Candidate, migration));
        var tracking = Assert.IsType<NasdaqH4ReconstructionSnapshot.RebuiltTracking>(
            new NasdaqH4RebuildPendingSnapshotReducer().Reduce(pending, turn));
        var expected = new NasdaqH4RebuiltTrackingSnapshotReducer().Reduce(tracking, continuation);
        Candle[] candles =
        [
            .. fixture.BaseCandles, fixture.CorrectionStart, fixture.CandidateTurn,
            migration, turn, continuation,
        ];
        var context = Context(32, candles);

        foreach (var source in new NasdaqH4ReconstructionSnapshot[] { fixture.Candidate, pending, tracking })
        {
            var result = Assert.IsType<NasdaqH4ReconstructionCatchUpResult.UpToDate>(
                orchestrator.CatchUp(source, context));
            Assert.IsType<NasdaqH4ReconstructionSnapshot.RebuiltTracking>(result.Snapshot);
            Assert.Same(continuation, result.Snapshot.MarketCursor);
            Assert.Equal(expected.Kind, result.Snapshot.Kind);
        }
        Assert.Same(fixture.CandidateTurn, fixture.Candidate.MarketCursor);
        Assert.Same(migration, pending.MarketCursor);
        Assert.Same(turn, tracking.MarketCursor);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ordinary009StopsForMissingEvidenceOrCompletesWithCurrentUniqueEvidence(bool unique)
    {
        var fixture = Fixture.Create(bearish: true);
        var migration = Candle(20, 100, 120, 50, 90);
        var breakoutCandle = Candle(24, 100, 140, 50, 131);
        var later = Candle(28, 100, 140, 50, 90);
        var pending = Assert.IsType<NasdaqH4ReconstructionSnapshot.RebuildPending>(
            new NasdaqH4CandidateSnapshotReducer().Reduce(fixture.Candidate, migration));
        var breakout = Assert.IsType<NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion>(
            new NasdaqH4RebuildPendingSnapshotReducer().Reduce(pending, breakoutCandle));
        Assert.Equal(NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.None, breakout.State.CollisionKind);
        var rebuild = Assert.IsType<NasdaqPostInvalidationBreakoutOrigin.Rebuild>(breakout.State.Origin);
        IStrategyReplayInputObservation[] evidence = unique
            ? [new NasdaqHumanRebuiltCandidateVertexObservation(
                breakout.Episode.StrategyId, breakout.Episode.StrategyVersion,
                Provider, Symbol, breakout.Episode.InvalidatingCandleOpenTimeUtc,
                rebuild.PriorMigrationCandle.OpenTimeUtc, breakout.State.CandidateSide,
                [migration.OpenTimeUtc], Start.AddHours(32), "review:rebuilt")]
            : [];
        var result = orchestrator.CatchUp(fixture.Candidate,
            Context(32, [.. fixture.BaseCandles, fixture.CorrectionStart,
                fixture.CandidateTurn, migration, breakoutCandle, later], evidence));

        Assert.Same(breakoutCandle, result.Snapshot.MarketCursor);
        if (unique)
        {
            var completed = Assert.IsType<NasdaqH4ReconstructionCatchUpResult.Completed>(result);
            var branch = Assert.IsType<NasdaqH4BreakoutAwaitingCompletionReductionResult.Completed.OrdinaryRebuilt009>(
                completed.BreakoutCompletion);
            Assert.Single(branch.Reduction.Selection.SupportingObservations);
        }
        else
        {
            var missing = Assert.IsType<NasdaqH4ReconstructionCatchUpResult.BreakoutEvidenceMissing>(result);
            Assert.IsType<NasdaqH4BreakoutAwaitingCompletionReductionResult.Missing.OrdinaryRebuilt009>(missing.Reduction);
        }
    }

    [Fact]
    public void FutureCandlesAndFutureEvidenceDoNotChangeEarlierOutcome()
    {
        var fixture = Fixture.Create();
        var future = OriginObservation(fixture, [fixture.Origin.OpenTimeUtc], 24, "review:future");
        var atT = Context(20, [.. fixture.BaseCandles, fixture.CorrectionStart, fixture.CandidateTurn]);
        var withFuture = Context(20,
            [.. fixture.BaseCandles, fixture.CorrectionStart, fixture.CandidateTurn, fixture.Next], future);
        var first = Assert.IsType<NasdaqH4ReconstructionCatchUpResult.OriginEvidenceMissing>(
            orchestrator.CatchUp(fixture.Awaiting, atT));
        var second = Assert.IsType<NasdaqH4ReconstructionCatchUpResult.OriginEvidenceMissing>(
            orchestrator.CatchUp(fixture.Awaiting, withFuture));
        var marketFirst = Assert.IsType<NasdaqH4ReconstructionCatchUpResult.UpToDate>(
            orchestrator.CatchUp(fixture.Impulse, atT));
        var marketSecond = Assert.IsType<NasdaqH4ReconstructionCatchUpResult.UpToDate>(
            orchestrator.CatchUp(fixture.Impulse, withFuture));

        Assert.Same(fixture.Awaiting, first.Snapshot);
        Assert.Same(fixture.Awaiting, second.Snapshot);
        Assert.Empty(second.Reduction.Selection.SupportingObservations);
        Assert.Equal(marketFirst.Snapshot.Kind, marketSecond.Snapshot.Kind);
        Assert.Same(fixture.CandidateTurn, marketFirst.Snapshot.MarketCursor);
        Assert.Same(fixture.CandidateTurn, marketSecond.Snapshot.MarketCursor);
    }

    [Fact]
    public void MissingH4HistoryOrDisagreeingCursorFailsRatherThanClaimingUpToDate()
    {
        var fixture = Fixture.Create();
        Assert.Throws<InvalidOperationException>(() => orchestrator.CatchUp(fixture.Impulse, MinuteOnlyContext(20)));
        Assert.Throws<InvalidOperationException>(() => orchestrator.CatchUp(fixture.Impulse,
            Context(20, [fixture.CorrectionStart, fixture.CandidateTurn])));
        var differentInvalidating = Candle(8, 100, 110, 79, 90);
        Assert.Throws<InvalidOperationException>(() => orchestrator.CatchUp(fixture.Impulse,
            Context(20, [fixture.Origin, fixture.OldCandidate, differentInvalidating,
                fixture.CorrectionStart, fixture.CandidateTurn])));
    }

    [Fact]
    public void GappedH4HistoryProcessesOnlyExistingClosedCandlesAndIgnoresOtherTimeframes()
    {
        var fixture = Fixture.Create();
        var first = Candle(12, 90, 95, 78, 85);
        var later = Candle(20, 80, 90, 70, 75);
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var other = new Candle(Provider, Symbol, minute, Start.AddHours(24).AddMinutes(-1),
            Start.AddHours(24), 100, 101, 99, 100, null);
        var expected = new NasdaqH4OppositeImpulseSnapshotReducer().Reduce(
            new NasdaqH4OppositeImpulseSnapshotReducer().Reduce(fixture.Impulse, first), later);
        var context = Context(24, [.. fixture.BaseCandles, first, later], extra: other);

        var result = Assert.IsType<NasdaqH4ReconstructionCatchUpResult.UpToDate>(
            orchestrator.CatchUp(fixture.Impulse, context));

        Assert.Equal(expected.Kind, result.Snapshot.Kind);
        Assert.Same(later, result.Snapshot.MarketCursor);
        Assert.Equal(expected.MarketCursor.Close, result.Snapshot.MarketCursor.Close);
    }

    [Fact]
    public void RepeatedCatchUpIsDeterministicAndLeavesItsInputsUnchanged()
    {
        var fixture = Fixture.Create();
        var candles = new[] { fixture.Origin, fixture.OldCandidate, fixture.Invalidating, fixture.CorrectionStart };
        var context = Context(16, candles);

        var first = Assert.IsType<NasdaqH4ReconstructionCatchUpResult.UpToDate>(
            orchestrator.CatchUp(fixture.Impulse, context));
        var second = Assert.IsType<NasdaqH4ReconstructionCatchUpResult.UpToDate>(
            orchestrator.CatchUp(fixture.Impulse, context));

        Assert.Equal(first.Snapshot.Kind, second.Snapshot.Kind);
        Assert.Same(fixture.CorrectionStart, first.Snapshot.MarketCursor);
        Assert.Same(fixture.CorrectionStart, second.Snapshot.MarketCursor);
        Assert.Same(fixture.Invalidating, fixture.Impulse.MarketCursor);
        Assert.True(context.TryGetFrame(H4, out var frame));
        Assert.Equal(candles, frame!.AvailableCandles);
        Assert.Empty(context.InputObservations);
    }

    private static StrategyReplayContext Context(int asOfHour, Candle[] candles,
        IStrategyReplayInputObservation[]? observations = null, Candle? extra = null)
    {
        var series = new List<CandleSeries> { new(Provider, Symbol, H4, candles) };
        if (extra is not null)
            series.Add(new CandleSeries(Provider, Symbol, extra.Timeframe, [extra]));
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        while (cursor.TryAdvance(out var frame))
        {
            if (frame!.AsOfUtc == Start.AddHours(asOfHour))
                return new CreateStrategyReplayContextUseCase().Execute(
                    MoneyWayNasdaqStrategyDefinition.Instance, frame, observations ?? []);
        }
        throw new InvalidOperationException("The requested fixture frame is unavailable.");
    }

    private static StrategyReplayContext Context(int asOfHour, Candle[] candles,
        params IStrategyReplayInputObservation[] observations) => Context(asOfHour, candles, observations, null);

    private static StrategyReplayContext MinuteOnlyContext(int asOfHour)
    {
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var candle = new Candle(Provider, Symbol, minute, Start.AddHours(asOfHour).AddMinutes(-1),
            Start.AddHours(asOfHour), 100, 101, 99, 100, null);
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, minute, [candle])]);
        cursor.TryAdvance(out var frame);
        return new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame!);
    }

    private static NasdaqHumanOriginVertexObservation OriginObservation(Fixture fixture,
        DateTimeOffset[] members, int observedAtHour, string source) => new(
        MoneyWayNasdaqStrategyDefinition.Instance.StrategyId,
        MoneyWayNasdaqStrategyDefinition.Instance.Version, Provider, Symbol,
        fixture.Invalidating.OpenTimeUtc, members, Start.AddHours(observedAtHour), source);

    private static NasdaqHumanCollisionStructuralPriceObservation CollisionObservation(
        NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion breakout,
        decimal price, int observedAtHour, string source) => new(
        breakout.Episode, breakout.MarketCursor.OpenTimeUtc, breakout.State.CandidateSide,
        price, Start.AddHours(observedAtHour), source);

    private static Candle Candle(int hour, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, H4, Start.AddHours(hour), Start.AddHours(hour + 4), open, high, low, close, null);

    private sealed record Fixture(
        Candle Origin, Candle OldCandidate, Candle Invalidating, Candle CorrectionStart,
        Candle CandidateTurn, Candle Next,
        NasdaqH4ReconstructionSnapshot.InvalidatedAwaitingOrigin Awaiting,
        NasdaqH4ReconstructionSnapshot.OppositeImpulse Impulse,
        NasdaqH4ReconstructionSnapshot.Candidate Candidate)
    {
        public Candle[] BaseCandles => [Origin, OldCandidate, Invalidating];

        public static Fixture Create(bool bearish = false)
        {
            var origin = bearish ? Candle(0, 100, 110, 85, 90) : Candle(0, 100, 110, 95, 110);
            var prior = bearish ? Candle(4, 105, 115, 100, 110) : Candle(4, 95, 100, 90, 96);
            var invalidating = bearish ? Candle(8, 100, 120, 90, 110) : Candle(8, 100, 110, 80, 90);
            var correctionStart = bearish ? Candle(12, 130, 145, 95, 115) : Candle(12, 70, 105, 65, 95);
            var candidateTurn = bearish ? Candle(16, 80, 100, 60, 90) : Candle(16, 100, 130, 90, 99);
            var next = Candle(20, 100, 140, 60, 100);
            var context = Context(12, [origin, prior, invalidating]);
            var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(
                context, H4, bearish ? 70 : 130, bearish ? 105 : 95,
                bearish ? 75 : 140, [prior],
                bearish ? StructuralCandidateExtremeSide.Upper : StructuralCandidateExtremeSide.Lower);
            var episode = new NasdaqHumanOriginVertexEpisode(
                MoneyWayNasdaqStrategyDefinition.Instance.StrategyId,
                MoneyWayNasdaqStrategyDefinition.Instance.Version, Provider, Symbol, invalidating.OpenTimeUtc);
            var awaiting = new NasdaqH4ReconstructionSnapshot.InvalidatedAwaitingOrigin(
                new NasdaqH4InvalidatedAwaitingOriginState(episode, boundary));
            var observation = new NasdaqHumanOriginVertexObservation(
                MoneyWayNasdaqStrategyDefinition.Instance.StrategyId,
                MoneyWayNasdaqStrategyDefinition.Instance.Version, Provider, Symbol,
                invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
            var observedContext = Context(12, [origin, prior, invalidating], observation);
            var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, observedContext);
            var geometry = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
            var impulse = new NasdaqH4ReconstructionSnapshot.OppositeImpulse(
                new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, geometry));
            var correction = new NasdaqH4OppositeImpulseSnapshotReducer().Reduce(impulse, correctionStart);
            var candidate = Assert.IsType<NasdaqH4ReconstructionSnapshot.Candidate>(
                new NasdaqH4CorrectionSnapshotReducer().Reduce(correction, candidateTurn));
            return new(origin, prior, invalidating, correctionStart, candidateTurn, next, awaiting, impulse, candidate);
        }
    }
}

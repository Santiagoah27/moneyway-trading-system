using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqH4OrdinaryRebuiltBreakoutEvidenceSnapshotReducerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqH4OrdinaryRebuiltBreakoutEvidenceSnapshotReducer reducer = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FutureEvidenceAndNoEvidenceBothRemainMissingAtTheSameReplayFrame(bool fromTracking)
    {
        var fixture = Ordinary(fromTracking);
        var atBreakout = fixture.Snapshot.State.ValidatingCandle.CloseTimeUtc;
        var later = atBreakout.AddHours(4);
        var future = Observation(fixture.Snapshot.State, [fixture.Migration.OpenTimeUtc], later, "review:future");

        var withoutFuture = reducer.Reduce(fixture.Snapshot, Context(fixture.Candles, atBreakout));
        var withFuture = reducer.Reduce(fixture.Snapshot, Context(fixture.Candles, atBreakout, future));

        Assert.Equal(NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionKind.Missing, withoutFuture.Kind);
        Assert.Equal(withoutFuture.Kind, withFuture.Kind);
        Assert.Same(fixture.Snapshot, withFuture.Snapshot);
        Assert.Same(fixture.Snapshot.State,
            Assert.IsType<NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion>(withFuture.Snapshot).State);
        Assert.Same(fixture.Snapshot.State.ValidatingCandle, withFuture.Snapshot.MarketCursor);
        Assert.Same(fixture.Snapshot.Episode, withFuture.Snapshot.Episode);
        Assert.Empty(withFuture.Selection.SupportingObservations);
        Assert.Empty(withFuture.Selection.DistinctMemberships);
        Assert.Null(withFuture.CompletionResult);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LateUniqueCompletesTheOriginalBreakoutWithResolvedHumanMembers(bool fromTracking)
    {
        var fixture = Ordinary(fromTracking);
        var atBreakout = fixture.Snapshot.State.ValidatingCandle.CloseTimeUtc;
        var later = atBreakout.AddHours(4);
        var selected = fromTracking
            ? new[] { fixture.Migration.OpenTimeUtc, fixture.Turn!.OpenTimeUtc }
            : [fixture.Migration.OpenTimeUtc];
        var observation = Observation(fixture.Snapshot.State, selected, later, "review:members");
        var early = reducer.Reduce(fixture.Snapshot, Context(fixture.Candles, atBreakout, observation));

        var completedResult = reducer.Reduce(fixture.Snapshot, Context(fixture.Candles, later, observation));
        var repeatedResult = reducer.Reduce(fixture.Snapshot, Context(fixture.Candles, later, observation));

        var completed = Assert.IsType<NasdaqH4ReconstructionSnapshot.Completed.Ordinary>(completedResult.Snapshot);
        var rebuild = Assert.IsType<NasdaqPostInvalidationBreakoutOrigin.Rebuild>(completed.Result.Breakout.Origin);
        Assert.Equal(NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionKind.Missing, early.Kind);
        Assert.Same(fixture.Snapshot, early.Snapshot);
        Assert.Null(early.CompletionResult);
        Assert.Equal(NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionKind.Completed, completedResult.Kind);
        Assert.Equal(completedResult.Kind, repeatedResult.Kind);
        Assert.Equal(completed.Result.CandidateGeometry.StructuralPrice,
            repeatedResult.CompletionResult!.CandidateGeometry.StructuralPrice);
        Assert.Equal(completedResult.Selection.SemanticMemberOpenTimesUtc,
            repeatedResult.Selection.SemanticMemberOpenTimesUtc);
        Assert.Same(completed.Result, completedResult.CompletionResult);
        Assert.Same(fixture.Snapshot.State, completed.Result.Breakout);
        Assert.Same(fixture.Snapshot.Episode, completed.Episode);
        Assert.Same(fixture.Snapshot.State.ValidatingCandle, completed.MarketCursor);
        Assert.Same(fixture.Migration, rebuild.PriorMigrationCandle);
        Assert.NotSame(completed.MarketCursor, rebuild.PriorMigrationCandle);
        Assert.Equal(fixture.Snapshot.State.PreviousProtectionAnchor, completed.Result.MemberResolution.KnownProtectionAnchor);
        Assert.Equal(fixture.Snapshot.State.EffectiveProtectionAnchor, completed.Result.CandidateGeometry.ProtectionAnchor);
        Assert.Equal(fromTracking ? [fixture.Migration, fixture.Turn!] : [fixture.Migration],
            completed.Result.MemberResolution.SelectedMembers);
        Assert.Equal(fromTracking ? 80m : 90m, completed.Result.CandidateGeometry.StructuralPrice);
        Assert.Equal(completed.Result.CandidateGeometry.StructuralPrice, completed.ValidatedCandidate.StructuralPrice);
        Assert.Same(observation, Assert.Single(completedResult.Selection.SupportingObservations));
        Assert.Null(typeof(NasdaqHumanRebuiltCandidateVertexObservation).GetProperty("StructuralPrice"));
    }

    [Fact]
    public void CompatibleDuplicateMemberSetsCompleteAndKeepBothReviewSources()
    {
        var fixture = Ordinary(false);
        var atBreakout = fixture.Snapshot.State.ValidatingCandle.CloseTimeUtc;
        var first = Observation(fixture.Snapshot.State, [fixture.Migration.OpenTimeUtc], atBreakout, "review:first");
        var second = Observation(fixture.Snapshot.State, [fixture.Migration.OpenTimeUtc], atBreakout.AddHours(4), "review:second");

        var result = reducer.Reduce(fixture.Snapshot, Context(fixture.Candles, atBreakout.AddHours(4), second, first));

        Assert.Equal(NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionKind.Completed, result.Kind);
        Assert.Equal([first, second], result.Selection.SupportingObservations);
        Assert.Single(result.Selection.DistinctMemberships);
        Assert.Equal([fixture.Migration.OpenTimeUtc], result.Selection.SemanticMemberOpenTimesUtc);
        Assert.Equal([fixture.Migration], result.CompletionResult!.MemberResolution.SelectedMembers);
        Assert.Same(fixture.Snapshot.State.ValidatingCandle, result.Snapshot.MarketCursor);
    }

    [Fact]
    public void IncompatibleMemberSetsRemainConflictAfterAnotherVisibleReview()
    {
        var fixture = Ordinary(true);
        var breakout = fixture.Snapshot.State;
        var atBreakout = breakout.ValidatingCandle.CloseTimeUtc;
        var first = Observation(breakout, [fixture.Migration.OpenTimeUtc], atBreakout, "review:first");
        var second = Observation(breakout, [fixture.Migration.OpenTimeUtc, fixture.Turn!.OpenTimeUtc],
            atBreakout, "review:second");
        var third = Observation(breakout, [fixture.Migration.OpenTimeUtc], atBreakout.AddHours(4), "review:third");

        var conflict = reducer.Reduce(fixture.Snapshot, Context(fixture.Candles, atBreakout, first, second, third));
        var later = reducer.Reduce(fixture.Snapshot, Context(fixture.Candles, atBreakout.AddHours(4), first, second, third));

        Assert.Equal(NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionKind.Conflict, conflict.Kind);
        Assert.Same(fixture.Snapshot, conflict.Snapshot);
        Assert.Same(breakout.ValidatingCandle, conflict.Snapshot.MarketCursor);
        Assert.Equal([first, second], conflict.Selection.SupportingObservations);
        Assert.Equal(2, conflict.Selection.DistinctMemberships.Count);
        Assert.Null(conflict.CompletionResult);
        Assert.Equal(NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionKind.Conflict, later.Kind);
        Assert.Same(fixture.Snapshot, later.Snapshot);
        Assert.Equal([first, second, third], later.Selection.SupportingObservations);
        Assert.Equal(2, later.Selection.DistinctMemberships.Count);
        Assert.Null(later.CompletionResult);
    }

    [Fact]
    public void Rejects007008CandidateOriginAndNonBreakoutSnapshots()
    {
        var candidate = Candidate();
        var directional = new NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculator()
            .Evaluate(candidate, Candle(20, 100, 140, 50, 131));
        var humanPrice = new NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculator()
            .Evaluate(candidate, Candle(20, 140, 150, 50, 131));
        var fixture = Ordinary(false);
        var pending = new NasdaqPostInvalidationCandidateRebuildTransitionCalculator()
            .Evaluate(Candidate(), Candle(20, 100, 120, 50, 90));
        var atBreakout = fixture.Snapshot.State.ValidatingCandle.CloseTimeUtc;
        var review = Observation(fixture.Snapshot.State, [fixture.Migration.OpenTimeUtc], atBreakout, "review:members");
        var context = Context(fixture.Candles, atBreakout, review);
        var completed = reducer.Reduce(fixture.Snapshot, context).Snapshot;

        Assert.Throws<ArgumentException>(() => reducer.Reduce(new NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion(directional), context));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(new NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion(humanPrice), context));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(new NasdaqH4ReconstructionSnapshot.Candidate(candidate), context));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(new NasdaqH4ReconstructionSnapshot.RebuildPending(pending), context));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(completed, context));
    }

    private static OrdinaryFixture Ordinary(bool fromTracking)
    {
        var pending = new NasdaqPostInvalidationCandidateRebuildTransitionCalculator()
            .Evaluate(Candidate(), Candle(20, 100, 120, 50, 90));
        Candle? turn = null;
        NasdaqPostInvalidationCandidateRebuildBreakoutState breakout;
        if (fromTracking)
        {
            turn = Candle(24, 80, 120, 60, 100);
            var tracking = new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator()
                .Evaluate(pending, turn).Tracking!;
            breakout = new NasdaqPostInvalidationRebuiltCandidateTrackingTransitionCalculator()
                .Evaluate(tracking, Candle(28, 100, 140, 55, 131)).Breakout!;
        }
        else
        {
            breakout = new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator()
                .Evaluate(pending, Candle(24, 100, 140, 50, 131)).Breakout!;
        }
        Assert.Equal(NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.None, breakout.CollisionKind);
        var candles = turn is null
            ? new[] { pending.MigrationCandle, breakout.ValidatingCandle }
            : [pending.MigrationCandle, turn, breakout.ValidatingCandle];
        return new(new(breakout), pending.MigrationCandle, turn, candles);
    }

    private static NasdaqHumanRebuiltCandidateVertexObservation Observation(
        NasdaqPostInvalidationCandidateRebuildBreakoutState breakout,
        IEnumerable<DateTimeOffset> members,
        DateTimeOffset observedAtUtc,
        string source)
    {
        var rebuild = (NasdaqPostInvalidationBreakoutOrigin.Rebuild)breakout.Origin;
        return new(breakout.Episode.StrategyId, breakout.Episode.StrategyVersion,
            breakout.Episode.ProviderId, breakout.Episode.Symbol,
            breakout.Episode.InvalidatingCandleOpenTimeUtc, rebuild.PriorMigrationCandle.OpenTimeUtc,
            breakout.CandidateSide, members, observedAtUtc, source);
    }

    private static StrategyReplayContext Context(
        IReadOnlyList<Candle> h4Candles,
        DateTimeOffset asOfUtc,
        params IStrategyReplayInputObservation[] observations)
    {
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var later = h4Candles.Max(candle => candle.CloseTimeUtc).AddHours(4);
        var trigger = new Candle(Provider, Symbol, minute, later.AddMinutes(-1), later,
            100, 101, 99, 100, null);
        var cursor = new MultiTimeframeCandleReplayCursor([
            new CandleSeries(Provider, Symbol, H4, h4Candles),
            new CandleSeries(Provider, Symbol, minute, [trigger])]);
        while (cursor.TryAdvance(out var frame))
        {
            if (frame!.AsOfUtc == asOfUtc)
                return new CreateStrategyReplayContextUseCase().Execute(
                    MoneyWayNasdaqStrategyDefinition.Instance, frame, observations);
        }
        throw new InvalidOperationException("Test replay frame was not found.");
    }

    private static NasdaqPostInvalidationCandidateState Candidate()
    {
        var origin = Candle(0, 100, 110, 85, 90);
        var prior = Candle(4, 105, 115, 100, 110);
        var invalidating = Candle(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version,
            Provider, Symbol, invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var cursor = new MultiTimeframeCandleReplayCursor([
            new CandleSeries(Provider, Symbol, H4, [origin, prior, invalidating])]);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;
        var context = new CreateStrategyReplayContextUseCase().Execute(definition, frame!, [observation]);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(
            context, H4, 70, 105, 75, [prior], StructuralCandidateExtremeSide.Upper);
        var geometry = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, geometry);
        var correction = new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(
                impulse, Candle(12, 130, 145, 95, 115)));
        return new NasdaqPostInvalidationCorrectionTransitionCalculator()
            .Evaluate(correction, Candle(16, 80, 100, 60, 90)).Candidate!;
    }

    private static Candle Candle(int hour, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, H4, Start.AddHours(hour), Start.AddHours(hour + 4), open, high, low, close, null);

    private sealed record OrdinaryFixture(
        NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion Snapshot,
        Candle Migration,
        Candle? Turn,
        IReadOnlyList<Candle> Candles);
}

using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionActiveImpulseInitializerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionActiveImpulseInitializer initializer = new();

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(7, false)]
    [InlineData(7, true)]
    [InlineData(9, false)]
    [InlineData(9, true)]
    public void InitializesUniversalTrendHandoffWithExactProtectedTurnAndProvenance(int branch, bool bearish)
    {
        var completion = Completed(branch, bearish);
        var protectedTurn = completion.ValidatedCandidate;
        var cursor = completion.MarketCursor;
        var state = initializer.Initialize(completion);
        Assert.Same(completion, state.Completion);
        Assert.Same(completion, state.Episode.Completion);
        Assert.Same(completion.Episode, state.Episode.PreviousCompletedEpisode);
        Assert.IsNotType<NasdaqHumanOriginVertexEpisode>(state.Episode);
        Assert.Equal(NasdaqPostCompletionEpisode.FromCompleted(completion), state.Episode);
        Assert.Equal(bearish ? StructuralTurnBodyCoordinateSide.Lower : StructuralTurnBodyCoordinateSide.Upper, state.ActiveExtremeSide);
        Assert.Equal(bearish ? StructuralCandidateExtremeSide.Upper : StructuralCandidateExtremeSide.Lower, state.ValidatedProtectedTurn.CandidateSide);
        Assert.Same(protectedTurn, state.ValidatedProtectedTurn);
        Assert.Same(protectedTurn.CandidateGeometry, state.ProtectedTurnGeometry);
        Assert.Equal(protectedTurn.StructuralPrice, state.ProtectedTurnGeometry.StructuralPrice);
        Assert.Equal(protectedTurn.ProtectionAnchor, state.ProtectedTurnGeometry.ProtectionAnchor);
        Assert.Same(cursor, state.MarketCursor);
        Assert.Same(cursor, state.ConfirmingCandle);
        Assert.Same(cursor, state.Episode.ConfirmingCandle);
        Assert.True(bearish ? cursor.Close < cursor.Open : cursor.Close > cursor.Open);
        var second = initializer.Initialize(completion);
        Assert.Equal(state.Episode, second.Episode);
        Assert.Same(state.Completion, second.Completion);
        Assert.Same(state.ValidatedProtectedTurn, second.ValidatedProtectedTurn);
        Assert.Same(state.MarketCursor, second.MarketCursor);
        Assert.Same(protectedTurn, completion.ValidatedCandidate);
        Assert.Same(cursor, completion.MarketCursor);
        if (completion is NasdaqH4ReconstructionSnapshot.Completed.Ordinary ordinary)
        {
            Assert.Same(ordinary.Result.MemberResolution,
                Assert.IsType<NasdaqH4ReconstructionSnapshot.Completed.Ordinary>(state.Completion).Result.MemberResolution);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void RejectsOppositeAndExactNeutralConfirmationWithoutInitializingCorrection(bool bearish, bool neutral)
    {
        var completion = Completed(0, bearish, opposite: !neutral, neutral: neutral);
        Assert.True(completion.ValidatedCandidate.IsValidated);
        var cursor = completion.MarketCursor;
        Assert.Throws<ArgumentException>(() => initializer.Initialize(completion));
        Assert.Same(cursor, completion.MarketCursor);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ValidHumanPrice008CompletionCannotEnterTrendBodyPath(bool bearish, bool neutral)
    {
        // The source calculator requires opposite or neutral body for 008; a trend fixture would be invalid.
        var completion = Assert.IsType<NasdaqH4ReconstructionSnapshot.Completed.HumanStructuralPrice>(Completed(8, bearish, neutral: neutral));
        Assert.True(completion.ValidatedCandidate.IsValidated);
        Assert.NotEmpty(completion.Result.HumanPriceSelection.SupportingObservations);
        Assert.Throws<ArgumentException>(() => initializer.Initialize(completion));
    }

    [Fact]
    public void StateApiHasOnlyImmutableSourceFactsWithoutPrematureExtremeOrMembership()
    {
        var type = typeof(NasdaqPostCompletionActiveImpulseState);
        Assert.Empty(type.GetConstructors());
        Assert.All(type.GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.Equal(new[] { "ActiveExtremeSide", "Completion", "ConfirmingCandle", "Episode", "MarketCursor", "ProtectedTurnGeometry", "ValidatedProtectedTurn" },
            type.GetProperties().Select(property => property.Name).Order());
        Assert.Throws<ArgumentNullException>(() => initializer.Initialize(null!));
    }

    private static NasdaqH4ReconstructionSnapshot.Completed Completed(int branch, bool bearish, bool opposite = false, bool neutral = false)
    {
        Candle C(int hour, decimal open, decimal high, decimal low, decimal close) => Candle(hour, open, high, low, close, bearish);
        var origin = C(0, 100, 110, 85, 90);
        var prior = C(4, 105, 115, 100, 110);
        var invalidating = C(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version, Provider, Symbol,
            invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var context = Context([origin, prior, invalidating], observation);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(context, H4,
            bearish ? 130 : 70, bearish ? 95 : 105, bearish ? 125 : 75, [prior],
            bearish ? StructuralCandidateExtremeSide.Lower : StructuralCandidateExtremeSide.Upper);
        var geometry = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, geometry);
        var correction = new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, C(12, 130, 145, 95, 115)));
        var candidate = new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, C(16, 80, 100, 60, 90)).Candidate!;
        if (branch == 0)
            return new NasdaqH4ReconstructionSnapshot.Completed.DirectCandidate(new NasdaqDirectCandidateBreakoutCompletionCalculator()
                .Evaluate(candidate, C(20, neutral ? 131 : opposite ? 140 : 130, 150, 60, 131)));
        if (branch == 7)
        {
            var breakout = new NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculator().Evaluate(candidate, C(20, 100, 140, 50, 131));
            return new NasdaqH4DirectionalBreakoutCompletionSnapshotReducer().Reduce(new NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion(breakout));
        }
        NasdaqPostInvalidationCandidateRebuildBreakoutState state;
        IStrategyReplayInputObservation completionObservation;
        Candle[] candles;
        if (branch == 8)
        {
            state = new NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculator().Evaluate(candidate, C(20, neutral ? 131 : 140, 150, 50, 131));
            completionObservation = new NasdaqHumanCollisionStructuralPriceObservation(state.Episode, state.ValidatingCandle.OpenTimeUtc,
                state.CandidateSide, bearish ? 89 : 111, Start.AddHours(40), "review:price");
            candles = [state.ValidatingCandle];
        }
        else
        {
            var pending = new NasdaqPostInvalidationCandidateRebuildTransitionCalculator().Evaluate(candidate, C(20, 100, 120, 50, 90));
            var turn = C(24, 80, 120, 60, 100);
            var tracking = new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator().Evaluate(pending, turn).Tracking!;
            state = new NasdaqPostInvalidationRebuiltCandidateTrackingTransitionCalculator().Evaluate(tracking, C(28, 100, 140, 55, 131)).Breakout!;
            completionObservation = new NasdaqHumanRebuiltCandidateVertexObservation(state.Episode.StrategyId, state.Episode.StrategyVersion,
                Provider, Symbol, state.Episode.InvalidatingCandleOpenTimeUtc, pending.MigrationCandle.OpenTimeUtc,
                state.CandidateSide, [pending.MigrationCandle.OpenTimeUtc], Start.AddHours(40), "review:members");
            candles = [pending.MigrationCandle, turn, state.ValidatingCandle];
        }
        var completionContext = Context([.. candles, C(40, 100, 110, 90, 100)], completionObservation);
        return (NasdaqH4ReconstructionSnapshot.Completed)new NasdaqH4BreakoutAwaitingCompletionReducer()
            .Reduce(new NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion(state), completionContext).Snapshot;
    }

    private static StrategyReplayContext Context(Candle[] candles, params IStrategyReplayInputObservation[] observations)
    {
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, candles)]);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;
        return new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame!, observations);
    }

    private static Candle Candle(int hour, decimal open, decimal high, decimal low, decimal close, bool bearish) =>
        new(Provider, Symbol, H4, Start.AddHours(hour), Start.AddHours(hour + 4),
            bearish ? 200 - open : open, bearish ? 200 - low : high, bearish ? 200 - high : low,
            bearish ? 200 - close : close, null);
}

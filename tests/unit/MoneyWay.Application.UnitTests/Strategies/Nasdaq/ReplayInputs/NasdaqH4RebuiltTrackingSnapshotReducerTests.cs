using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqH4RebuiltTrackingSnapshotReducerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqH4RebuiltTrackingSnapshotReducer reducer = new();

    [Fact]
    public void ContinuationWrapsNewTrackingPayloadWithoutMutatingInput()
    {
        var tracking = Tracking();
        var input = new NasdaqH4ReconstructionSnapshot.RebuiltTracking(tracking);
        var candle = Candle(28, 100, 140, 50, 90);

        var next = Assert.IsType<NasdaqH4ReconstructionSnapshot.RebuiltTracking>(reducer.Reduce(input, candle));

        Assert.NotSame(input, next);
        Assert.NotSame(tracking, next.State);
        Assert.Same(candle, next.MarketCursor);
        Assert.Same(tracking.FirstTurnCandle, next.State.FirstTurnCandle);
        Assert.Equal(tracking.KnownProtectionAnchor, next.State.KnownProtectionAnchor);
        Assert.Same(tracking.LastProcessedCandle, input.MarketCursor);
    }

    [Fact]
    public void StrictMigrationWithoutTurnResetsToPending()
    {
        var tracking = Tracking();
        var candle = Candle(28, 100, 140, 40, 90);

        var next = Assert.IsType<NasdaqH4ReconstructionSnapshot.RebuildPending>(
            reducer.Reduce(new NasdaqH4ReconstructionSnapshot.RebuiltTracking(tracking), candle));

        Assert.Equal(40m, next.State.KnownProtectionAnchor);
        Assert.Same(candle, next.State.MigrationCandle);
        Assert.Same(candle, next.MarketCursor);
        Assert.Equal(50m, tracking.KnownProtectionAnchor);
    }

    [Fact]
    public void StrictMigrationWithTurnRestartsTrackingWithoutIntermediatePendingSnapshot()
    {
        var tracking = Tracking();
        var candle = Candle(28, 100, 140, 40, 101);

        var next = Assert.IsType<NasdaqH4ReconstructionSnapshot.RebuiltTracking>(
            reducer.Reduce(new NasdaqH4ReconstructionSnapshot.RebuiltTracking(tracking), candle));

        Assert.Same(candle, next.State.MigrationCandle);
        Assert.Same(candle, next.State.FirstTurnCandle);
        Assert.Same(candle, next.MarketCursor);
        Assert.Equal(40m, next.State.KnownProtectionAnchor);
    }

    [Theory]
    [InlineData(100, 140, 50, 131, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.None)]
    [InlineData(100, 140, 40, 131, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody)]
    [InlineData(140, 150, 40, 131, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired)]
    [InlineData(131, 150, 40, 131, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired)]
    public void EveryBreakoutRemainsAwaitingCompletion(
        decimal open, decimal high, decimal low, decimal close,
        NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind kind)
    {
        var candle = Candle(28, open, high, low, close);
        var next = Assert.IsType<NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion>(
            reducer.Reduce(new NasdaqH4ReconstructionSnapshot.RebuiltTracking(Tracking()), candle));

        Assert.Equal(kind, next.State.CollisionKind);
        Assert.IsType<NasdaqPostInvalidationBreakoutOrigin.Rebuild>(next.State.Origin);
        Assert.Same(candle, next.MarketCursor);
    }

    [Fact]
    public void RejectsOtherVariantsAndPropagatesTrackingValidation()
    {
        var tracking = Tracking();
        var candidate = Candidate();
        var input = new NasdaqH4ReconstructionSnapshot.RebuiltTracking(tracking);
        var pending = new NasdaqH4ReconstructionSnapshot.RebuildPending(Pending());
        var completed = new NasdaqH4ReconstructionSnapshot.Completed.DirectCandidate(
            new NasdaqDirectCandidateBreakoutCompletionCalculator().Evaluate(candidate, Candle(20, 100, 140, 60, 131)));
        var wrongProvider = new Candle(new MarketDataProviderId("other"), Symbol, H4,
            Start.AddHours(28), Start.AddHours(32), 100, 140, 50, 90, null);

        Assert.Throws<ArgumentException>(() => reducer.Reduce(new NasdaqH4ReconstructionSnapshot.Candidate(candidate), Candle(28, 100, 140, 50, 90)));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(pending, Candle(28, 100, 140, 50, 90)));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(completed, Candle(28, 100, 140, 50, 90)));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(input, tracking.LastProcessedCandle));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(input, Candle(20, 100, 140, 50, 90)));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(input, wrongProvider));
        Assert.Equal(NasdaqH4ReconstructionSnapshotKind.RebuiltTracking,
            reducer.Reduce(input, Candle(32, 100, 140, 50, 90)).Kind);
    }

    private static NasdaqPostInvalidationRebuiltCandidateTrackingState Tracking() =>
        new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator().Evaluate(Pending(), Candle(24, 100, 140, 50, 101)).Tracking!;

    private static NasdaqPostInvalidationCandidateRebuildPendingState Pending() =>
        new NasdaqPostInvalidationCandidateRebuildTransitionCalculator().Evaluate(Candidate(), Candle(20, 100, 140, 50, 90));

    private static NasdaqPostInvalidationCandidateState Candidate()
    {
        var origin = Candle(0, 100, 110, 85, 90);
        var prior = Candle(4, 105, 115, 100, 110);
        var invalidating = Candle(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version,
            Provider, Symbol, invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var context = Context([origin, prior, invalidating], [observation]);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(
            context, H4, 70, 105, 75, [prior], StructuralCandidateExtremeSide.Upper);
        var geometry = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, geometry);
        var correction = new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, Candle(12, 130, 145, 95, 115)));
        return new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, Candle(16, 80, 100, 60, 90)).Candidate!;
    }

    private static StrategyReplayContext Context(Candle[] candles, IStrategyReplayInputObservation[] observations)
    {
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, candles)]);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;
        return new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame!, observations);
    }

    private static Candle Candle(int hour, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, H4, Start.AddHours(hour), Start.AddHours(hour + 4), open, high, low, close, null);
}

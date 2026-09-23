using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqH4CandidateSnapshotReducerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqH4CandidateSnapshotReducer reducer = new();

    [Fact]
    public void NoEventCreatesNewCandidateSnapshotWithoutMutatingInput()
    {
        var candidate = Candidate(StructuralCandidateExtremeSide.Lower);
        var input = new NasdaqH4ReconstructionSnapshot.Candidate(candidate);
        var incoming = Candle(20, 100, 140, 60, 100);

        var next = Assert.IsType<NasdaqH4ReconstructionSnapshot.Candidate>(reducer.Reduce(input, incoming));

        Assert.NotSame(input, next);
        Assert.Same(incoming, next.MarketCursor);
        Assert.Same(candidate.LastProcessedCandle, input.MarketCursor);
        Assert.Same(candidate.TerminalCandle, next.State.TerminalCandle);
        Assert.Same(candidate.CandidateGeometry, next.State.CandidateGeometry);
        Assert.Same(candidate.Episode, next.Episode);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 60, 131, NasdaqH4ReconstructionSnapshotKind.Completed)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 50, 90, NasdaqH4ReconstructionSnapshotKind.RebuildPending)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 60, 90, NasdaqH4ReconstructionSnapshotKind.RebuiltTracking)]
    public void MapsDirectPendingAndTrackingBranchesToExactSnapshotVariants(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close,
        NasdaqH4ReconstructionSnapshotKind expectedKind)
    {
        var candidate = Candidate(side);
        var incoming = Candle(20, open, high, low, close);

        var next = reducer.Reduce(new NasdaqH4ReconstructionSnapshot.Candidate(candidate), incoming);

        Assert.Equal(expectedKind, next.Kind);
        Assert.Same(candidate.Episode, next.Episode);
        Assert.Same(incoming, next.MarketCursor);
        if (next is NasdaqH4ReconstructionSnapshot.Completed.DirectCandidate completed)
        {
            Assert.Same(candidate, completed.Result.Candidate);
            Assert.Same(candidate.CandidateGeometry, completed.Result.ValidatedCandidate.CandidateGeometry);
        }
        else if (next is NasdaqH4ReconstructionSnapshot.RebuildPending pending)
        {
            Assert.Equal(low, pending.State.KnownProtectionAnchor);
        }
        else
        {
            var tracking = Assert.IsType<NasdaqH4ReconstructionSnapshot.RebuiltTracking>(next);
            Assert.Same(incoming, tracking.State.MigrationCandle);
            Assert.Same(incoming, tracking.State.FirstTurnCandle);
        }
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 50, 131, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 140, 150, 50, 131, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 69, 140, 60, 69, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired)]
    public void CollisionStopsAtBreakoutAwaitingCompletion(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close,
        NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind kind)
    {
        var candidate = Candidate(side);
        var incoming = Candle(20, open, high, low, close);

        var next = Assert.IsType<NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion>(
            reducer.Reduce(new NasdaqH4ReconstructionSnapshot.Candidate(candidate), incoming));

        Assert.Equal(kind, next.State.CollisionKind);
        Assert.IsType<NasdaqPostInvalidationBreakoutOrigin.Candidate>(next.State.Origin);
        Assert.Same(incoming, next.MarketCursor);
        Assert.IsNotType<NasdaqH4ReconstructionSnapshot.Completed.Directional>(next);
    }

    [Fact]
    public void RejectsNonCandidateVariantsAndPropagatesDispatcherValidation()
    {
        var candidate = Candidate(StructuralCandidateExtremeSide.Lower);
        var pending = new NasdaqH4ReconstructionSnapshot.RebuildPending(
            new NasdaqPostInvalidationCandidateRebuildTransitionCalculator().Evaluate(candidate, Candle(20, 100, 140, 50, 90)));
        var completed = new NasdaqH4ReconstructionSnapshot.Completed.DirectCandidate(
            new NasdaqDirectCandidateBreakoutCompletionCalculator().Evaluate(candidate, Candle(20, 100, 140, 60, 131)));
        var input = new NasdaqH4ReconstructionSnapshot.Candidate(candidate);
        var wrongProvider = new Candle(new MarketDataProviderId("other"), Symbol, H4,
            Start.AddHours(20), Start.AddHours(24), 100, 140, 60, 100, null);
        var wrongSymbol = new Candle(Provider, new MarketSymbol("OTHER"), H4,
            Start.AddHours(20), Start.AddHours(24), 100, 140, 60, 100, null);
        var wrongTimeframe = new Candle(Provider, Symbol, new Timeframe(1, TimeframeUnit.Hour),
            Start.AddHours(20), Start.AddHours(21), 100, 140, 60, 100, null);

        Assert.Throws<ArgumentException>(() => reducer.Reduce(pending, Candle(24, 100, 140, 60, 100)));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(completed, Candle(24, 100, 140, 60, 100)));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(input, candidate.LastProcessedCandle));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(input, Candle(12, 100, 140, 60, 100)));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(input, wrongProvider));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(input, wrongSymbol));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(input, wrongTimeframe));
        Assert.Equal(NasdaqH4ReconstructionSnapshotKind.Candidate, reducer.Reduce(input, Candle(24, 100, 140, 60, 100)).Kind);
    }

    private static NasdaqPostInvalidationCandidateState Candidate(StructuralCandidateExtremeSide side)
    {
        var oldSide = side == StructuralCandidateExtremeSide.Upper ? StructuralCandidateExtremeSide.Lower : StructuralCandidateExtremeSide.Upper;
        var bullish = oldSide == StructuralCandidateExtremeSide.Lower;
        var origin = bullish ? Candle(0, 100, 110, 95, 110) : Candle(0, 100, 110, 85, 90);
        var prior = bullish ? Candle(4, 95, 100, 90, 96) : Candle(4, 105, 115, 100, 110);
        var invalidating = bullish ? Candle(8, 100, 110, 80, 90) : Candle(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version,
            Provider, Symbol, invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var context = Context([origin, prior, invalidating], [observation]);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(
            context, H4, bullish ? 130 : 70, bullish ? 95 : 105, bullish ? 140 : 75, [prior], oldSide);
        var originGeometry = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, originGeometry);
        var start = bullish ? Candle(12, 70, 105, 65, 95) : Candle(12, 130, 145, 95, 115);
        var correction = new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, start));
        var terminal = side == StructuralCandidateExtremeSide.Lower
            ? Candle(16, 80, 100, 60, 90) : Candle(16, 100, 130, 90, 99);
        return new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, terminal).Candidate!;
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

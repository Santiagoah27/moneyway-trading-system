using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostInvalidationCandidateTransitionCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostInvalidationCandidateTransitionCalculator calculator = new();

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 60, 100)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 130, 60, 100)]
    public void NoMigrationAndNoBreakoutContinuesCandidate(StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var candidate = Candidate(side);
        var candle = Candle(20, open, high, low, close);

        var result = calculator.Evaluate(candidate, candle);
        var branch = Assert.IsType<NasdaqPostInvalidationCandidateTransitionResult.CandidateContinues>(result);

        Assert.Equal(NasdaqPostInvalidationCandidateTransitionKind.CandidateContinues, result.Kind);
        Assert.Same(candle, branch.State.LastProcessedCandle);
        Assert.Same(candidate.TerminalCandle, branch.State.TerminalCandle);
        Assert.Same(candidate.CandidateGeometry, branch.State.CandidateGeometry);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 60, 131)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 130, 60, 69)]
    public void NonMigratingBreakoutUsesDirectCompletion(StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var candidate = Candidate(side);
        var candle = Candle(20, open, high, low, close);

        var result = calculator.Evaluate(candidate, candle);
        var branch = Assert.IsType<NasdaqPostInvalidationCandidateTransitionResult.DirectCompleted>(result);

        Assert.Equal(NasdaqPostInvalidationCandidateTransitionKind.DirectCompleted, result.Kind);
        Assert.Same(candidate, branch.Result.Candidate);
        Assert.Same(candidate.CandidateGeometry, branch.Result.ValidatedCandidate.CandidateGeometry);
        Assert.Same(candle, branch.Result.LastProcessedCandle);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 50, 90)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 50, 100)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 60, 110)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 60, 100)]
    public void MigrationWithoutBreakoutAndNoFirstTurnUsesPending(StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var candle = Candle(20, open, high, low, close);
        var result = calculator.Evaluate(Candidate(side), candle);
        var branch = Assert.IsType<NasdaqPostInvalidationCandidateTransitionResult.RebuildPending>(result);

        Assert.Equal(NasdaqPostInvalidationCandidateTransitionKind.RebuildPending, result.Kind);
        Assert.Same(candle, branch.State.MigrationCandle);
        Assert.Equal(side == StructuralCandidateExtremeSide.Lower ? low : high, branch.State.KnownProtectionAnchor);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 50, 101)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 60, 90)]
    public void MigrationWithoutBreakoutAndFirstTurnStartsTracking(StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var candle = Candle(20, open, high, low, close);
        var result = calculator.Evaluate(Candidate(side), candle);
        var branch = Assert.IsType<NasdaqPostInvalidationCandidateTransitionResult.RebuiltTracking>(result);

        Assert.Equal(NasdaqPostInvalidationCandidateTransitionKind.RebuiltTracking, result.Kind);
        Assert.Same(candle, branch.State.MigrationCandle);
        Assert.Same(candle, branch.State.FirstTurnCandle);
        Assert.Same(candle, branch.State.LastProcessedCandle);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 50, 131, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 60, 69, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 140, 150, 50, 131, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 60, 140, 60, 69, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 131, 150, 50, 131, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 69, 140, 60, 69, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired)]
    public void MigrationAndBreakoutUsesCandidateOriginCollision(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close,
        NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind collisionKind)
    {
        var candidate = Candidate(side);
        var candle = Candle(20, open, high, low, close);
        var result = calculator.Evaluate(candidate, candle);
        var branch = Assert.IsType<NasdaqPostInvalidationCandidateTransitionResult.CollisionBreakout>(result);

        Assert.Equal(NasdaqPostInvalidationCandidateTransitionKind.CollisionBreakout, result.Kind);
        Assert.IsType<NasdaqPostInvalidationBreakoutOrigin.Candidate>(branch.State.Origin);
        Assert.Equal(collisionKind, branch.State.CollisionKind);
        Assert.Same(candle, branch.State.ValidatingCandle);
        Assert.Equal(candidate.CandidateGeometry.ProtectionAnchor, branch.State.PreviousProtectionAnchor);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 60, 130, NasdaqPostInvalidationCandidateTransitionKind.CandidateContinues)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 130, 60, 70, NasdaqPostInvalidationCandidateTransitionKind.CandidateContinues)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 50, 130, NasdaqPostInvalidationCandidateTransitionKind.RebuiltTracking)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 60, 70, NasdaqPostInvalidationCandidateTransitionKind.RebuiltTracking)]
    public void StrictEqualityFollowsItsNonEventOrNoBreakoutBranch(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close,
        NasdaqPostInvalidationCandidateTransitionKind expected)
    {
        Assert.Equal(expected, calculator.Evaluate(Candidate(side), Candle(20, open, high, low, close)).Kind);
    }

    [Fact]
    public void RejectsInvalidChronologyAndSeriesWhileAllowingNonContiguousLaterCandle()
    {
        var candidate = Candidate(StructuralCandidateExtremeSide.Lower);
        var valid = Candle(24, 100, 140, 60, 100);
        var wrongProvider = new Candle(new MarketDataProviderId("other"), Symbol, H4,
            Start.AddHours(20), Start.AddHours(24), 100, 140, 60, 100, null);
        var wrongSymbol = new Candle(Provider, new MarketSymbol("OTHER"), H4,
            Start.AddHours(20), Start.AddHours(24), 100, 140, 60, 100, null);
        var wrongTimeframe = new Candle(Provider, Symbol, new Timeframe(1, TimeframeUnit.Hour),
            Start.AddHours(20), Start.AddHours(21), 100, 140, 60, 100, null);

        Assert.Equal(NasdaqPostInvalidationCandidateTransitionKind.CandidateContinues, calculator.Evaluate(candidate, valid).Kind);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, candidate.LastProcessedCandle));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, Candle(12, 100, 140, 60, 100)));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, wrongProvider));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, wrongSymbol));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, wrongTimeframe));
    }

    [Fact]
    public void EveryDispatcherPayloadMapsToAnExistingSnapshotVariant()
    {
        var lower = Candidate(StructuralCandidateExtremeSide.Lower);
        var upper = Candidate(StructuralCandidateExtremeSide.Upper);
        NasdaqPostInvalidationCandidateTransitionResult[] results =
        [
            calculator.Evaluate(lower, Candle(20, 100, 140, 60, 100)),
            calculator.Evaluate(lower, Candle(20, 100, 140, 60, 131)),
            calculator.Evaluate(lower, Candle(20, 100, 140, 50, 90)),
            calculator.Evaluate(lower, Candle(20, 100, 140, 50, 101)),
            calculator.Evaluate(upper, Candle(20, 100, 140, 60, 69)),
        ];

        var snapshots = results.Select(result => result switch
        {
            NasdaqPostInvalidationCandidateTransitionResult.CandidateContinues branch =>
                (NasdaqH4ReconstructionSnapshot)new NasdaqH4ReconstructionSnapshot.Candidate(branch.State),
            NasdaqPostInvalidationCandidateTransitionResult.DirectCompleted branch =>
                new NasdaqH4ReconstructionSnapshot.Completed.DirectCandidate(branch.Result),
            NasdaqPostInvalidationCandidateTransitionResult.RebuildPending branch =>
                new NasdaqH4ReconstructionSnapshot.RebuildPending(branch.State),
            NasdaqPostInvalidationCandidateTransitionResult.RebuiltTracking branch =>
                new NasdaqH4ReconstructionSnapshot.RebuiltTracking(branch.State),
            NasdaqPostInvalidationCandidateTransitionResult.CollisionBreakout branch =>
                new NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion(branch.State),
            _ => throw new InvalidOperationException("Unexpected candidate transition result."),
        }).ToArray();

        Assert.Equal(5, snapshots.Select(snapshot => snapshot.Kind).Distinct().Count());
        Assert.All(snapshots, snapshot => Assert.Equal(H4, snapshot.MarketCursor.Timeframe));
    }

    private static NasdaqPostInvalidationCandidateState Candidate(StructuralCandidateExtremeSide side)
    {
        var oldSide = side == StructuralCandidateExtremeSide.Upper
            ? StructuralCandidateExtremeSide.Lower : StructuralCandidateExtremeSide.Upper;
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

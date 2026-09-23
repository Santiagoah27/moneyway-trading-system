using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostInvalidationRebuiltCandidateTrackingTransitionCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostInvalidationRebuiltCandidateTrackingTransitionCalculator calculator = new();

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 120, 50, 110)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 120, 50, 90)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 120, 50, 100)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 80, 90)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 80, 110)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 80, 100)]
    public void NoMigrationOrBreakoutContinuesWithOnlyCursorAdvanced(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var current = Tracking(side);
        var candle = Candle(28, open, high, low, close);

        var result = calculator.Evaluate(current, candle);

        Assert.Equal(NasdaqPostInvalidationRebuiltCandidateTrackingTransitionKind.TrackingContinues, result.Kind);
        var next = Assert.IsType<NasdaqPostInvalidationRebuiltCandidateTrackingState>(result.Tracking);
        Assert.Same(candle, next.LastProcessedCandle);
        Assert.Same(current.FirstTurnCandle, next.FirstTurnCandle);
        Assert.Same(current.MigrationCandle, next.MigrationCandle);
        Assert.Equal(current.KnownProtectionAnchor, next.KnownProtectionAnchor);
        Assert.Same(current.FrozenImpulseTerminal, next.FrozenImpulseTerminal);
        Assert.Same(current.OriginGeometry, next.OriginGeometry);
        Assert.Same(current.Episode, next.Episode);
        Assert.Same(current.FirstTurnCandle, current.LastProcessedCandle);
        Assert.Null(next.GetType().GetProperty("StructuralPrice"));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 120, 49, 90, 49)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 120, 49, 100, 49)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 141, 80, 110, 141)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 141, 80, 100, 141)]
    public void MigrationWithoutNewTurnResetsToPendingAndDiscardsOldTurn(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close, decimal expectedAnchor)
    {
        var current = Tracking(side);
        var candle = Candle(28, open, high, low, close);

        var result = calculator.Evaluate(current, candle);

        Assert.Equal(NasdaqPostInvalidationRebuiltCandidateTrackingTransitionKind.PendingReset, result.Kind);
        var pending = Assert.IsType<NasdaqPostInvalidationCandidateRebuildPendingState>(result.Pending);
        Assert.Same(candle, pending.MigrationCandle);
        Assert.NotSame(current.MigrationCandle, pending.MigrationCandle);
        Assert.Same(candle, pending.LastProcessedCandle);
        Assert.Equal(expectedAnchor, pending.KnownProtectionAnchor);
        Assert.Same(current.InvalidatingCandle, pending.InvalidatingCandle);
        Assert.Same(current.OriginGeometry, pending.OriginGeometry);
        Assert.Same(current.FrozenImpulseTerminal, pending.FrozenImpulseTerminal);
        Assert.Same(current.Episode, pending.Episode);
        Assert.Null(pending.GetType().GetProperty("FirstTurnCandle"));
        Assert.Same(current.FirstTurnCandle, current.LastProcessedCandle);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 120, 49, 110, 49)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 141, 80, 90, 141)]
    public void MigrationAndTurnRestartTrackingOnSameCandle(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close, decimal expectedAnchor)
    {
        var current = Tracking(side);
        var candle = Candle(28, open, high, low, close);

        var result = calculator.Evaluate(current, candle);

        Assert.Equal(NasdaqPostInvalidationRebuiltCandidateTrackingTransitionKind.TrackingRestarted, result.Kind);
        var restarted = Assert.IsType<NasdaqPostInvalidationRebuiltCandidateTrackingState>(result.Tracking);
        Assert.Same(candle, restarted.MigrationCandle);
        Assert.Same(candle, restarted.FirstTurnCandle);
        Assert.Same(candle, restarted.LastProcessedCandle);
        Assert.Equal(expectedAnchor, restarted.KnownProtectionAnchor);
        Assert.Same(current.Episode, restarted.Episode);
        Assert.Same(current.FirstTurnCandle, current.LastProcessedCandle);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 140, 145, 50, 131)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 131, 140, 50, 131)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 60, 140, 50, 69)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 69, 140, 50, 69)]
    public void BreakoutWithoutMigrationEndsTrackingRegardlessOfBody(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var current = Tracking(side);
        var candle = Candle(28, open, high, low, close);

        var result = calculator.Evaluate(current, candle);

        Assert.Equal(NasdaqPostInvalidationRebuiltCandidateTrackingTransitionKind.BreakoutDetected, result.Kind);
        var breakout = Assert.IsType<NasdaqPostInvalidationCandidateRebuildBreakoutState>(result.Breakout);
        Assert.False(breakout.HasStrictMigration);
        Assert.Equal(NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.None, breakout.CollisionKind);
        Assert.Equal(current.KnownProtectionAnchor, breakout.PreviousProtectionAnchor);
        Assert.Equal(breakout.PreviousProtectionAnchor, breakout.EffectiveProtectionAnchor);
        Assert.Same(current.FirstTurnCandle, breakout.FirstTurnCandle);
        var origin = Assert.IsType<NasdaqPostInvalidationBreakoutOrigin.Rebuild>(breakout.Origin);
        Assert.Same(current.MigrationCandle, origin.PriorMigrationCandle);
        Assert.Equal(current.KnownProtectionAnchor, breakout.PreviousProtectionAnchor);
        Assert.Equal(current.KnownProtectionAnchor, breakout.EffectiveProtectionAnchor);
        Assert.Same(candle, breakout.ValidatingCandle);
        Assert.Same(candle, breakout.LastProcessedCandle);
        Assert.Same(current.Episode, breakout.Episode);
        Assert.Null(result.Tracking);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 49, 131, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 141, 50, 69, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 140, 145, 49, 131, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 60, 141, 50, 69, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 131, 140, 49, 131, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 69, 141, 50, 69, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired)]
    public void MigrationAndBreakoutReuseDirectionalOrHumanPriceCollisionResult(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close,
        NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind expected)
    {
        var current = Tracking(side);
        var candle = Candle(28, open, high, low, close);

        var result = calculator.Evaluate(current, candle);

        Assert.Equal(NasdaqPostInvalidationRebuiltCandidateTrackingTransitionKind.BreakoutDetected, result.Kind);
        var breakout = Assert.IsType<NasdaqPostInvalidationCandidateRebuildBreakoutState>(result.Breakout);
        Assert.True(breakout.HasStrictMigration);
        Assert.IsType<NasdaqPostInvalidationBreakoutOrigin.Rebuild>(breakout.Origin);
        Assert.Equal(current.KnownProtectionAnchor, breakout.PreviousProtectionAnchor);
        Assert.Equal(expected, breakout.CollisionKind);
        Assert.Equal(side == StructuralCandidateExtremeSide.Lower ? low : high, breakout.EffectiveProtectionAnchor);
        Assert.Same(current.FirstTurnCandle, breakout.FirstTurnCandle);
        Assert.Same(candle, breakout.LastProcessedCandle);
        Assert.Null(breakout.GetType().GetProperty("StructuralPrice"));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 50, 130)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 50, 70)]
    public void ExactAnchorAndTerminalEqualityDoNotResetOrBreakOut(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var current = Tracking(side);
        var candle = Candle(28, open, high, low, close);

        var result = calculator.Evaluate(current, candle);

        Assert.Equal(NasdaqPostInvalidationRebuiltCandidateTrackingTransitionKind.TrackingContinues, result.Kind);
        Assert.Same(current.MigrationCandle, result.Tracking!.MigrationCandle);
        Assert.Equal(current.KnownProtectionAnchor, result.Tracking.KnownProtectionAnchor);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower)]
    [InlineData(StructuralCandidateExtremeSide.Upper)]
    public void TemporalAndSeriesSafetyRejectsInvalidCandlesAndRepeatedEvaluationIsDeterministic(StructuralCandidateExtremeSide side)
    {
        var current = Tracking(side);
        var valid = Candle(28, 100, 120, 80, 100);
        var earlier = Candle(20, 100, 120, 80, 100);
        var wrong = new Candle(Provider, new MarketSymbol("OTHER"), H4,
            Start.AddHours(28), Start.AddHours(32), 100, 120, 80, 100, null);

        Assert.Throws<ArgumentException>(() => calculator.Evaluate(current, current.LastProcessedCandle));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(current, earlier));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(current, wrong));
        var first = calculator.Evaluate(current, valid);
        var repeated = calculator.Evaluate(current, valid);
        Assert.Equal(first.Kind, repeated.Kind);
        Assert.Same(valid, first.Tracking!.LastProcessedCandle);
        Assert.Same(valid, repeated.Tracking!.LastProcessedCandle);
        Assert.Same(current.FirstTurnCandle, current.LastProcessedCandle);
    }

    private static NasdaqPostInvalidationRebuiltCandidateTrackingState Tracking(StructuralCandidateExtremeSide side)
    {
        var pending = Pending(side);
        var firstTurn = side == StructuralCandidateExtremeSide.Lower
            ? Candle(24, 100, 120, 50, 110)
            : Candle(24, 100, 140, 80, 90);
        return new NasdaqPostInvalidationRebuiltCandidateTrackingInitializer().Initialize(pending, firstTurn);
    }

    private static NasdaqPostInvalidationCandidateRebuildPendingState Pending(StructuralCandidateExtremeSide side)
    {
        var correction = InitialCorrection(side);
        var terminal = side == StructuralCandidateExtremeSide.Lower
            ? Candle(16, 80, 100, 60, 90)
            : Candle(16, 100, 130, 90, 99);
        var candidate = new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, terminal).Candidate!;
        var migration = side == StructuralCandidateExtremeSide.Lower
            ? Candle(20, 100, 120, 50, 90)
            : Candle(20, 100, 140, 80, 100);
        return new NasdaqPostInvalidationCandidateRebuildTransitionCalculator().Evaluate(candidate, migration);
    }

    private static NasdaqPostInvalidationCorrectionState InitialCorrection(StructuralCandidateExtremeSide side)
    {
        var oldSide = side == StructuralCandidateExtremeSide.Upper ? StructuralCandidateExtremeSide.Lower : StructuralCandidateExtremeSide.Upper;
        var bullish = oldSide == StructuralCandidateExtremeSide.Lower;
        var originCandle = bullish ? Candle(0, 100, 110, 95, 110) : Candle(0, 100, 110, 85, 90);
        var oldCandidate = bullish ? Candle(4, 95, 100, 90, 96) : Candle(4, 105, 115, 100, 110);
        var invalidating = bullish ? Candle(8, 100, 110, 80, 90) : Candle(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version,
            Provider, Symbol, invalidating.OpenTimeUtc, [originCandle.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, [originCandle, oldCandidate, invalidating])]);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;
        var context = new CreateStrategyReplayContextUseCase().Execute(definition, frame!, [observation]);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(
            context, H4, bullish ? 130 : 70, bullish ? 95 : 105, bullish ? 140 : 75, [oldCandidate], oldSide);
        var origin = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, origin);
        var start = bullish ? Candle(12, 70, 105, 65, 95) : Candle(12, 130, 145, 95, 115);
        return new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, start));
    }

    private static Candle Candle(int openHour, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, H4, Start.AddHours(openHour), Start.AddHours(openHour + 4), open, high, low, close, null);
}

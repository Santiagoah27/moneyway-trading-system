using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator calculator = new();

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 120, 50, 90)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 80, 110)]
    public void NonTurnWithoutMigrationOrBreakoutContinuesAndAdvancesCursor(StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var pending = Pending(side);
        var candle = Candle(24, open, high, low, close);

        var result = calculator.Evaluate(pending, candle);

        Assert.Equal(NasdaqPostInvalidationCandidateRebuildPendingTransitionKind.PendingContinues, result.Kind);
        var continued = Assert.IsType<NasdaqPostInvalidationCandidateRebuildPendingState>(result.Pending);
        Assert.Same(candle, continued.LastProcessedCandle);
        Assert.Same(pending.MigrationCandle, continued.MigrationCandle);
        Assert.Equal(pending.KnownProtectionAnchor, continued.KnownProtectionAnchor);
        Assert.Same(pending.FrozenImpulseTerminal, continued.FrozenImpulseTerminal);
        Assert.Same(pending.Episode, continued.Episode);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 120, 49, 90, 49)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 141, 80, 110, 141)]
    public void StrictMigrationWithoutTurnResetsPending(StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close, decimal expectedAnchor)
    {
        var candle = Candle(24, open, high, low, close);

        var pending = Pending(side);
        var result = calculator.Evaluate(pending, candle);

        Assert.Equal(NasdaqPostInvalidationCandidateRebuildPendingTransitionKind.PendingReset, result.Kind);
        Assert.Same(candle, result.Pending!.MigrationCandle);
        Assert.NotSame(pending.MigrationCandle, result.Pending.MigrationCandle);
        Assert.Same(candle, result.Pending.LastProcessedCandle);
        Assert.Equal(expectedAnchor, result.Pending.KnownProtectionAnchor);
        Assert.Same(pending.Episode, result.Pending.Episode);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 120, 50, 110)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 80, 90)]
    public void StrictFirstTurnStartsTrackingWithoutGeometry(StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var candle = Candle(24, open, high, low, close);

        var pending = Pending(side);
        var result = calculator.Evaluate(pending, candle);

        Assert.Equal(NasdaqPostInvalidationCandidateRebuildPendingTransitionKind.TrackingStarted, result.Kind);
        Assert.Same(candle, result.Tracking!.FirstTurnCandle);
        Assert.Same(candle, result.Tracking.LastProcessedCandle);
        Assert.Same(pending.Episode, result.Tracking.Episode);
        Assert.Null(result.Tracking.GetType().GetProperty("StructuralPrice"));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 120, 49, 110, 49)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 141, 80, 90, 141)]
    public void MigrationAndTurnOnOneCandleResetsThenStartsTracking(StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close, decimal expectedAnchor)
    {
        var candle = Candle(24, open, high, low, close);

        var tracking = calculator.Evaluate(Pending(side), candle).Tracking!;

        Assert.Same(candle, tracking.MigrationCandle);
        Assert.Same(candle, tracking.FirstTurnCandle);
        Assert.Same(candle, tracking.LastProcessedCandle);
        Assert.Equal(expectedAnchor, tracking.KnownProtectionAnchor);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 50, 131)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 50, 69)]
    public void BreakoutWithoutMigrationIsDetectedRegardlessOfCandleBody(StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var candle = Candle(24, open, high, low, close);

        var pending = Pending(side);
        var result = calculator.Evaluate(pending, candle);

        Assert.Equal(NasdaqPostInvalidationCandidateRebuildPendingTransitionKind.BreakoutDetected, result.Kind);
        Assert.False(result.Breakout!.HasStrictMigration);
        Assert.Equal(NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.None, result.Breakout.CollisionKind);
        Assert.Equal(pending.KnownProtectionAnchor, result.Breakout.PreviousProtectionAnchor);
        Assert.Equal(result.Breakout.PreviousProtectionAnchor, result.Breakout.EffectiveProtectionAnchor);
        Assert.Same(candle, result.Breakout.LastProcessedCandle);
        Assert.Same(pending.Episode, result.Breakout.Episode);
        Assert.Equal(pending.Episode.StrategyId, result.Breakout.Episode.StrategyId);
        Assert.Same(pending.Episode.StrategyVersion, result.Breakout.Episode.StrategyVersion);
        Assert.Equal(pending.Episode.ProviderId, result.Breakout.Episode.ProviderId);
        Assert.Equal(pending.Episode.Symbol, result.Breakout.Episode.Symbol);
        Assert.Equal(H4, result.Breakout.Episode.Timeframe);
        Assert.Equal(pending.InvalidatingCandle.OpenTimeUtc, result.Breakout.Episode.InvalidatingCandleOpenTimeUtc);
        var origin = Assert.IsType<NasdaqPostInvalidationBreakoutOrigin.Rebuild>(result.Breakout.Origin);
        Assert.Equal(pending.MigrationCandle.OpenTimeUtc, origin.PriorMigrationCandle.OpenTimeUtc);
        Assert.Equal(pending.KnownProtectionAnchor, result.Breakout.PreviousProtectionAnchor);
        Assert.Equal(pending.CandidateSide, result.Breakout.CandidateSide);
        Assert.Null(result.Tracking);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 49, 131, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 141, 50, 69, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 140, 145, 49, 131, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 60, 141, 50, 69, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired)]
    public void MigrationBreakoutClassifiesDirectionalAndHumanStructuralPriceBranches(StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close, NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind expected)
    {
        var candle = Candle(24, open, high, low, close);
        var pending = Pending(side);

        var breakout = calculator.Evaluate(pending, candle).Breakout!;

        Assert.True(breakout.HasStrictMigration);
        Assert.IsType<NasdaqPostInvalidationBreakoutOrigin.Rebuild>(breakout.Origin);
        Assert.Equal(pending.KnownProtectionAnchor, breakout.PreviousProtectionAnchor);
        Assert.Equal(expected, breakout.CollisionKind);
        Assert.Equal(side == StructuralCandidateExtremeSide.Lower ? low : high, breakout.EffectiveProtectionAnchor);
        Assert.Null(breakout.GetType().GetProperty("StructuralPrice"));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 50, 130, 100)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 140, 70, 100)]
    public void ExactAnchorAndFrozenTerminalEqualityDoNotMigrateOrBreakOut(StructuralCandidateExtremeSide side, decimal anchor, decimal frozenClose, decimal open)
    {
        var candle = side == StructuralCandidateExtremeSide.Lower
            ? Candle(24, open, 140, anchor, frozenClose)
            : Candle(24, open, anchor, 50, frozenClose);

        var result = calculator.Evaluate(Pending(side), candle);

        Assert.NotEqual(NasdaqPostInvalidationCandidateRebuildPendingTransitionKind.BreakoutDetected, result.Kind);
        Assert.Equal(side == StructuralCandidateExtremeSide.Lower ? 50m : 140m,
            result.Pending?.KnownProtectionAnchor ?? result.Tracking!.KnownProtectionAnchor);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower)]
    [InlineData(StructuralCandidateExtremeSide.Upper)]
    public void SameEarlierAndWrongSeriesCandlesAreRejectedAndEvaluationIsDeterministic(StructuralCandidateExtremeSide side)
    {
        var pending = Pending(side);
        var valid = side == StructuralCandidateExtremeSide.Lower ? Candle(24, 100, 120, 50, 110) : Candle(24, 100, 140, 80, 90);
        var earlier = side == StructuralCandidateExtremeSide.Lower ? Candle(16, 100, 120, 50, 110) : Candle(16, 100, 140, 80, 90);
        var wrong = new Candle(Provider, new MarketSymbol("OTHER"), H4, Start.AddHours(24), Start.AddHours(28), 100, 140, 50, 100, null);

        Assert.Throws<ArgumentException>(() => calculator.Evaluate(pending, pending.LastProcessedCandle));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(pending, earlier));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(pending, wrong));
        Assert.Equal(calculator.Evaluate(pending, valid).Kind, calculator.Evaluate(pending, valid).Kind);
        Assert.Same(pending.MigrationCandle, pending.LastProcessedCandle);
    }

    private static NasdaqPostInvalidationCandidateRebuildPendingState Pending(StructuralCandidateExtremeSide side)
    {
        var correction = InitialCorrection(side);
        var terminal = side == StructuralCandidateExtremeSide.Lower ? Candle(16, 80, 100, 60, 90) : Candle(16, 100, 130, 90, 99);
        var candidate = new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, terminal).Candidate!;
        var migration = side == StructuralCandidateExtremeSide.Lower ? Candle(20, 100, 120, 50, 90) : Candle(20, 100, 140, 80, 100);
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
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version, Provider, Symbol, invalidating.OpenTimeUtc, [originCandle.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, [originCandle, oldCandidate, invalidating])]);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;
        var context = new CreateStrategyReplayContextUseCase().Execute(definition, frame!, [observation]);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(context, H4, bullish ? 130 : 70, bullish ? 95 : 105, bullish ? 140 : 75, [oldCandidate], oldSide);
        var origin = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, origin);
        var start = bullish ? Candle(12, 70, 105, 65, 95) : Candle(12, 130, 145, 95, 115);
        return new NasdaqPostInvalidationCorrectionInitializer().Initialize(new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, start));
    }

    private static Candle Candle(int openHour, decimal open, decimal high, decimal low, decimal close) => new(Provider, Symbol, H4, Start.AddHours(openHour), Start.AddHours(openHour + 4), open, high, low, close, null);
}

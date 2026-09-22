using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostInvalidationRebuiltCandidateTrackingInitializerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostInvalidationRebuiltCandidateTrackingInitializer initializer = new();

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower)]
    [InlineData(StructuralCandidateExtremeSide.Upper)]
    public void ValidFirstTurnPreservesReconstructionFactsWithoutDefinitiveGeometry(StructuralCandidateExtremeSide side)
    {
        var pending = Pending(side);
        var turn = side == StructuralCandidateExtremeSide.Lower
            ? Candle(24, 100, 120, 50, 110)
            : Candle(24, 100, 140, 80, 90);

        var state = initializer.Initialize(pending, turn);

        Assert.Equal(side, state.CandidateSide);
        Assert.Equal(pending.KnownProtectionAnchor, state.KnownProtectionAnchor);
        Assert.Same(pending.FrozenImpulseTerminal, state.FrozenImpulseTerminal);
        Assert.Same(pending.InvalidatingCandle, state.InvalidatingCandle);
        Assert.Same(pending.Episode, state.Episode);
        Assert.Same(pending.OriginGeometry, state.OriginGeometry);
        Assert.Same(pending.MigrationCandle, state.MigrationCandle);
        Assert.Same(turn, state.FirstTurnCandle);
        Assert.Same(turn, state.LastProcessedCandle);
        Assert.Null(state.GetType().GetProperty("StructuralPrice"));
        Assert.Null(state.GetType().GetProperty("CandidateGeometry"));
        Assert.Null(state.GetType().GetProperty("CorrectionTurnCandles"));
        Assert.Null(state.GetType().GetProperty("SelectedMemberOpenTimesUtc"));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 120, 50, 90)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 120, 50, 100)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 80, 110)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 80, 100)]
    public void WrongOrNeutralFirstTurnBodyIsRejected(StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        Assert.Throws<ArgumentException>(() => initializer.Initialize(Pending(side), Candle(24, open, high, low, close)));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower)]
    [InlineData(StructuralCandidateExtremeSide.Upper)]
    public void SameMigrationCandleCanAlsoBeTheFirstTurn(StructuralCandidateExtremeSide side)
    {
        var pending = Pending(side, dualRoleMigration: true);

        var state = initializer.Initialize(pending, pending.MigrationCandle);

        Assert.Same(pending.MigrationCandle, state.FirstTurnCandle);
        Assert.Same(state.FirstTurnCandle, state.LastProcessedCandle);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 49, 120, 110)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 80, 141, 90)]
    public void LaterTurnThatCreatesNewStrictExtremeIsRejected(StructuralCandidateExtremeSide side, decimal low, decimal high, decimal close)
    {
        Assert.Throws<ArgumentException>(() => initializer.Initialize(Pending(side), Candle(24, 100, high, low, close)));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 130)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 70)]
    public void StrictFrozenTerminalBreakoutIsRejectedButEqualityIsAllowed(StructuralCandidateExtremeSide side, decimal frozenLevel)
    {
        var pending = Pending(side);
        var equality = side == StructuralCandidateExtremeSide.Lower
            ? Candle(24, 100, 140, 50, frozenLevel)
            : Candle(24, 100, 140, 50, frozenLevel);
        var breakout = side == StructuralCandidateExtremeSide.Lower
            ? Candle(24, 100, 140, 50, frozenLevel + 1)
            : Candle(24, 100, 140, 50, frozenLevel - 1);

        Assert.Same(equality, initializer.Initialize(pending, equality).LastProcessedCandle);
        Assert.Throws<ArgumentException>(() => initializer.Initialize(pending, breakout));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower)]
    [InlineData(StructuralCandidateExtremeSide.Upper)]
    public void EarlierTurnIsRejectedAndConstructionIsDeterministic(StructuralCandidateExtremeSide side)
    {
        var pending = Pending(side);
        var valid = side == StructuralCandidateExtremeSide.Lower
            ? Candle(24, 100, 120, 50, 110)
            : Candle(24, 100, 140, 80, 90);
        var earlier = side == StructuralCandidateExtremeSide.Lower
            ? Candle(16, 100, 120, 50, 110)
            : Candle(16, 100, 140, 80, 90);

        var first = initializer.Initialize(pending, valid);

        Assert.Throws<ArgumentException>(() => initializer.Initialize(pending, earlier));
        Assert.Equal(first.CandidateSide, initializer.Initialize(pending, valid).CandidateSide);
        Assert.Same(pending.MigrationCandle, pending.LastProcessedCandle);
    }

    private static NasdaqPostInvalidationCandidateRebuildPendingState Pending(
        StructuralCandidateExtremeSide side, bool dualRoleMigration = false)
    {
        var correction = InitialCorrection(side);
        var terminal = side == StructuralCandidateExtremeSide.Lower
            ? Candle(16, 80, 100, 60, 90)
            : Candle(16, 100, 130, 90, 99);
        var candidate = new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, terminal).Candidate!;
        var migration = side == StructuralCandidateExtremeSide.Lower
            ? Candle(20, 100, 120, 50, dualRoleMigration ? 110 : 90)
            : Candle(20, 100, 140, 80, dualRoleMigration ? 90 : 100);
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

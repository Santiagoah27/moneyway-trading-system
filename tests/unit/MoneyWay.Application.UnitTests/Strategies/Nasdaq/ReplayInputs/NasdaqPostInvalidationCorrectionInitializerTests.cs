using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostInvalidationCorrectionInitializerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostInvalidationCorrectionInitializer initializer = new();

    [Fact]
    public void BearishImpulseStartsUpperSideCorrectionAndPreservesUpdatedFrozenLow()
    {
        var (transition, origin) = Started(StructuralCandidateExtremeSide.Lower,
            Candle(12, 70, 105, 65, 95));

        var result = initializer.Initialize(transition);

        Assert.Equal(StructuralCandidateExtremeSide.Upper, result.CandidateSide);
        Assert.Equal(StructuralTurnBodyCoordinateSide.Lower, result.ImpulseTerminalSide);
        Assert.Same(origin, result.OriginGeometry);
        Assert.Same(transition.ResultingState.InvalidatingCandle, result.InvalidatingCandle);
        Assert.Same(transition.ResultingState.Episode, result.Episode);
        Assert.Same(transition.ResultingState.ProvisionalTerminal, result.FrozenImpulseTerminal);
        Assert.Equal(70m, result.FrozenImpulseTerminal.StructuralPrice);
        Assert.Equal(65m, result.FrozenImpulseTerminal.ProtectionAnchor);
        Assert.Equal(StructuralTurnBodyCoordinateSide.Upper, result.CorrectionGeometry.Side);
        Assert.Equal(95m, result.CorrectionGeometry.StructuralPrice);
        Assert.Equal(105m, result.CorrectionGeometry.ProtectionAnchor);
        Assert.Single(result.CorrectionTurnCandles);
        Assert.Same(transition.CorrectionStart!.FirstTurnCandle, result.CorrectionTurnCandles[0]);
        Assert.Same(result.CorrectionStartCandle, result.LastProcessedCandle);
        Assert.NotSame(result.InvalidatingCandle, result.CorrectionStartCandle);
    }

    [Fact]
    public void BullishImpulseStartsLowerSideCorrectionAndPreservesUpdatedFrozenHigh()
    {
        var (transition, origin) = Started(StructuralCandidateExtremeSide.Upper,
            Candle(12, 130, 145, 95, 115));

        var result = initializer.Initialize(transition);

        Assert.Equal(StructuralCandidateExtremeSide.Lower, result.CandidateSide);
        Assert.Equal(StructuralTurnBodyCoordinateSide.Upper, result.ImpulseTerminalSide);
        Assert.Same(origin, result.OriginGeometry);
        Assert.Same(transition.ResultingState.InvalidatingCandle, result.InvalidatingCandle);
        Assert.Same(transition.ResultingState.ProvisionalTerminal, result.FrozenImpulseTerminal);
        Assert.Equal(130m, result.FrozenImpulseTerminal.StructuralPrice);
        Assert.Equal(145m, result.FrozenImpulseTerminal.ProtectionAnchor);
        Assert.Equal(StructuralTurnBodyCoordinateSide.Lower, result.CorrectionGeometry.Side);
        Assert.Equal(115m, result.CorrectionGeometry.StructuralPrice);
        Assert.Equal(95m, result.CorrectionGeometry.ProtectionAnchor);
        Assert.Single(result.CorrectionTurnCandles);
        Assert.Same(transition.CorrectionStart!.FirstTurnCandle, result.CorrectionTurnCandles[0]);
        Assert.Same(result.CorrectionStartCandle, result.LastProcessedCandle);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 95, 100, 82, 100)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 105, 118, 100, 100)]
    public void StartWithoutImpulseExtensionKeepsPreviouslyFrozenTerminal(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var (transition, _) = Started(side, Candle(12, open, high, low, close));
        var result = initializer.Initialize(transition);

        Assert.Same(transition.ResultingState.ProvisionalTerminal, result.FrozenImpulseTerminal);
        Assert.Same(transition.CorrectionStart!.FirstTurnCandle, result.CorrectionTurnCandles[0]);
        Assert.Equal(result.CorrectionGeometry, initializer.Initialize(transition).CorrectionGeometry);
        Assert.Equal(result.FrozenImpulseTerminal, initializer.Initialize(transition).FrozenImpulseTerminal);
        Assert.IsAssignableFrom<IReadOnlyList<Candle>>(result.CorrectionTurnCandles);
        Assert.Throws<NotSupportedException>(() => ((IList<Candle>)result.CorrectionTurnCandles).Add(result.CorrectionStartCandle));
    }

    [Fact]
    public void ContinuingImpulseCannotInitializeCorrection()
    {
        var transition = Transition(StructuralCandidateExtremeSide.Lower, Candle(12, 90, 100, 78, 85));
        Assert.Equal(NasdaqPostInvalidationOppositeImpulseTransitionKind.ContinuingImpulse, transition.Kind);

        Assert.Throws<ArgumentException>(() => initializer.Initialize(transition));
    }

    private static (NasdaqPostInvalidationOppositeImpulseTransitionResult Transition, StructuralTurnGeometryResult Origin)
        Started(StructuralCandidateExtremeSide side, Candle startCandle)
    {
        var (transition, origin) = TransitionWithOrigin(side, startCandle);
        Assert.Equal(NasdaqPostInvalidationOppositeImpulseTransitionKind.CorrectionStarted, transition.Kind);
        return (transition, origin);
    }

    private static NasdaqPostInvalidationOppositeImpulseTransitionResult Transition(
        StructuralCandidateExtremeSide side, Candle candle) => TransitionWithOrigin(side, candle).Transition;

    private static (NasdaqPostInvalidationOppositeImpulseTransitionResult Transition, StructuralTurnGeometryResult Origin)
        TransitionWithOrigin(StructuralCandidateExtremeSide side, Candle candle)
    {
        var bullish = side == StructuralCandidateExtremeSide.Lower;
        var originCandle = bullish ? Candle(0, 100, 110, 95, 110) : Candle(0, 100, 110, 85, 90);
        var candidate = bullish ? Candle(4, 95, 100, 90, 96) : Candle(4, 105, 115, 100, 110);
        var invalidating = bullish ? Candle(8, 100, 110, 80, 90) : Candle(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version,
            Provider, Symbol, invalidating.OpenTimeUtc, [originCandle.OpenTimeUtc],
            invalidating.CloseTimeUtc, "review:origin");
        var cursor = new MultiTimeframeCandleReplayCursor(
            [new CandleSeries(Provider, Symbol, H4, [originCandle, candidate, invalidating])]);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;
        var context = new CreateStrategyReplayContextUseCase().Execute(definition, frame!, [observation]);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(
            context, H4, bullish ? 130 : 70, bullish ? 95 : 105, bullish ? 140 : 75,
            [candidate], side);
        var origin = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, origin);
        var transition = new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, candle);
        return (transition, origin);
    }

    private static Candle Candle(int openHour, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, H4, Start.AddHours(openHour), Start.AddHours(openHour + 4),
            open, high, low, close, null);
}

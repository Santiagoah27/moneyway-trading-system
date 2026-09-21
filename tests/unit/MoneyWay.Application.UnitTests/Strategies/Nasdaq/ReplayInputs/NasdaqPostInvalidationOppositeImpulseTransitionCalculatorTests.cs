using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostInvalidationOppositeImpulseTransitionCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostInvalidationOppositeImpulseTransitionCalculator calculator = new();

    [Theory]
    [InlineData(90, 95, 78, 85, 85, 78)]
    [InlineData(100, 105, 85, 95, 90, 80)]
    [InlineData(90, 100, 82, 85, 85, 80)]
    [InlineData(95, 100, 75, 90, 90, 75)]
    public void BearishContinuationAccumulatesIndependentBodyAndWickExtrema(
        decimal open, decimal high, decimal low, decimal close,
        decimal expectedBody, decimal expectedWick)
    {
        var (state, origin) = Initial(StructuralCandidateExtremeSide.Lower);
        var next = Candle(12, open, high, low, close);

        var result = calculator.Evaluate(state, next);

        Assert.Equal(NasdaqPostInvalidationOppositeImpulseTransitionKind.ContinuingImpulse, result.Kind);
        Assert.Null(result.CorrectionStart);
        Assert.Equal(expectedBody, result.ResultingState.ProvisionalTerminal.StructuralPrice);
        Assert.Equal(expectedWick, result.ResultingState.ProvisionalTerminal.ProtectionAnchor);
        Assert.Same(origin, result.ResultingState.OriginGeometry);
        Assert.Same(state.InvalidatingCandle, result.ResultingState.InvalidatingCandle);
        Assert.Same(next, result.ResultingState.LastProcessedCandle);
        Assert.False(result.ResultingState.IsTerminalFrozen);
        Assert.Equal(90m, state.ProvisionalTerminal.StructuralPrice);
        Assert.Equal(80m, state.ProvisionalTerminal.ProtectionAnchor);
    }

    [Theory]
    [InlineData(105, 125, 100, 120, 120, 125)]
    [InlineData(100, 115, 90, 105, 110, 120)]
    [InlineData(105, 118, 100, 115, 115, 120)]
    [InlineData(100, 130, 90, 105, 110, 130)]
    public void BullishContinuationAccumulatesIndependentBodyAndWickExtrema(
        decimal open, decimal high, decimal low, decimal close,
        decimal expectedBody, decimal expectedWick)
    {
        var (state, origin) = Initial(StructuralCandidateExtremeSide.Upper);
        var next = Candle(12, open, high, low, close);

        var result = calculator.Evaluate(state, next);

        Assert.Equal(NasdaqPostInvalidationOppositeImpulseTransitionKind.ContinuingImpulse, result.Kind);
        Assert.Equal(expectedBody, result.ResultingState.ProvisionalTerminal.StructuralPrice);
        Assert.Equal(expectedWick, result.ResultingState.ProvisionalTerminal.ProtectionAnchor);
        Assert.Same(origin, result.ResultingState.OriginGeometry);
        Assert.Same(state.InvalidatingCandle, result.ResultingState.InvalidatingCandle);
        Assert.Same(next, result.ResultingState.LastProcessedCandle);
        Assert.False(result.ResultingState.IsTerminalFrozen);
        Assert.Equal(110m, state.ProvisionalTerminal.StructuralPrice);
        Assert.Equal(120m, state.ProvisionalTerminal.ProtectionAnchor);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 70, 105, 65, 95, 70, 65)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 130, 145, 95, 115, 130, 145)]
    public void CorrectiveCandleCanExtendTerminalAndStartCorrectionOnSameClose(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close,
        decimal expectedBody, decimal expectedWick)
    {
        var (state, origin) = Initial(side);
        var next = Candle(12, open, high, low, close);

        var result = calculator.Evaluate(state, next);

        Assert.Equal(NasdaqPostInvalidationOppositeImpulseTransitionKind.CorrectionStarted, result.Kind);
        Assert.NotNull(result.CorrectionStart);
        Assert.Equal(CorrectionStartTransitionKind.StartCorrection, result.CorrectionStart.TransitionKind);
        Assert.Same(next, result.CorrectionStart.FirstTurnCandle);
        Assert.Equal(CorrectionTurnCurrentCandleMembership.NewTurn, result.CorrectionStart.CurrentCandleMembership);
        Assert.Equal(expectedBody, result.ResultingState.ProvisionalTerminal.StructuralPrice);
        Assert.Equal(expectedWick, result.ResultingState.ProvisionalTerminal.ProtectionAnchor);
        Assert.True(result.ResultingState.IsTerminalFrozen);
        Assert.Same(origin, result.ResultingState.OriginGeometry);
        Assert.Same(state.InvalidatingCandle, result.ResultingState.InvalidatingCandle);
        Assert.Same(next, result.ResultingState.LastProcessedCandle);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(result.ResultingState, Candle(16, 100, 110, 80, 105)));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 95, 100, 82, 100)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 105, 118, 100, 100)]
    public void CorrectiveCandleWithoutExtensionFreezesExistingTerminal(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var (state, _) = Initial(side);
        var result = calculator.Evaluate(state, Candle(12, open, high, low, close));

        Assert.Equal(NasdaqPostInvalidationOppositeImpulseTransitionKind.CorrectionStarted, result.Kind);
        Assert.Equal(state.ProvisionalTerminal, result.ResultingState.ProvisionalTerminal);
        Assert.True(result.ResultingState.IsTerminalFrozen);
    }

    [Fact]
    public void ExactDojiCanExtendTerminalWithoutStartingCorrection()
    {
        var (state, _) = Initial(StructuralCandidateExtremeSide.Lower);
        var result = calculator.Evaluate(state, Candle(12, 85, 100, 70, 85));

        Assert.Equal(NasdaqPostInvalidationOppositeImpulseTransitionKind.ContinuingImpulse, result.Kind);
        Assert.Equal(85m, result.ResultingState.ProvisionalTerminal.StructuralPrice);
        Assert.Equal(70m, result.ResultingState.ProvisionalTerminal.ProtectionAnchor);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower)]
    [InlineData(StructuralCandidateExtremeSide.Upper)]
    public void ExactNonzeroCorrectiveBodyStartsWithoutInventingNearDojiThreshold(
        StructuralCandidateExtremeSide side)
    {
        var (state, _) = Initial(side);
        var close = side == StructuralCandidateExtremeSide.Lower
            ? 1.0000000000000000000000000001m
            : 0.9999999999999999999999999999m;
        var result = calculator.Evaluate(state, Candle(12, 1m, 130, 0, close));

        Assert.Equal(NasdaqPostInvalidationOppositeImpulseTransitionKind.CorrectionStarted, result.Kind);
        Assert.True(result.ResultingState.IsTerminalFrozen);
    }

    [Fact]
    public void LaterH4CandleCanCrossSessionsWithoutContiguousTimeAssumption()
    {
        var (state, _) = Initial(StructuralCandidateExtremeSide.Lower);
        var later = Candle(24, 90, 100, 78, 85);

        var result = calculator.Evaluate(state, later);

        Assert.Equal(NasdaqPostInvalidationOppositeImpulseTransitionKind.ContinuingImpulse, result.Kind);
        Assert.Same(later, result.ResultingState.LastProcessedCandle);
    }

    [Fact]
    public void SameOrEarlierCandleAndWrongSeriesAreRejected()
    {
        var (state, _) = Initial(StructuralCandidateExtremeSide.Lower);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(state, state.LastProcessedCandle));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(state, Candle(4, 100, 110, 80, 90)));
        var wrongSymbol = new Candle(Provider, new MarketSymbol("OTHER"), H4,
            Start.AddHours(12), Start.AddHours(16), 100, 110, 80, 90, null);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(state, wrongSymbol));
    }

    [Fact]
    public void AFollowingUnprovidedCandleCannotChangeTheEarlierTransition()
    {
        var (state, _) = Initial(StructuralCandidateExtremeSide.Lower);
        var first = Candle(12, 90, 95, 78, 85);
        var before = calculator.Evaluate(state, first);
        var after = calculator.Evaluate(state, first);
        _ = calculator.Evaluate(before.ResultingState, Candle(16, 80, 90, 70, 75));

        Assert.Equal(before.Kind, after.Kind);
        Assert.Equal(before.ResultingState.ProvisionalTerminal, after.ResultingState.ProvisionalTerminal);
        Assert.Same(first, before.ResultingState.LastProcessedCandle);
        Assert.Equal(85m, before.ResultingState.ProvisionalTerminal.StructuralPrice);
        Assert.Equal(78m, before.ResultingState.ProvisionalTerminal.ProtectionAnchor);
    }

    private static (NasdaqPostInvalidationOppositeImpulseState State, StructuralTurnGeometryResult Origin) Initial(
        StructuralCandidateExtremeSide candidateSide)
    {
        var bullish = candidateSide == StructuralCandidateExtremeSide.Lower;
        var origin = bullish ? Candle(0, 100, 110, 95, 110) : Candle(0, 100, 110, 85, 90);
        var candidate = bullish ? Candle(4, 95, 100, 90, 96) : Candle(4, 105, 115, 100, 110);
        var invalidating = bullish ? Candle(8, 100, 110, 80, 90) : Candle(8, 100, 120, 90, 110);
        var series = new CandleSeries(Provider, Symbol, H4, [origin, candidate, invalidating]);
        var cursor = new MultiTimeframeCandleReplayCursor([series]);
        MultiTimeframeReplayFrame? replay = null;
        while (cursor.TryAdvance(out var frame)) replay = frame;
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version,
            Provider, Symbol, invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var context = new CreateStrategyReplayContextUseCase().Execute(definition, replay!, [observation]);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(
            context, H4, bullish ? 130 : 70, bullish ? 95 : 105, bullish ? 140 : 75,
            [candidate], candidateSide);
        var geometry = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var state = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, geometry);
        return (state, geometry);
    }

    private static Candle Candle(int openHour, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, H4, Start.AddHours(openHour), Start.AddHours(openHour + 4), open, high, low, close, null);
}

using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostInvalidationCorrectionTransitionCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostInvalidationCorrectionTransitionCalculator calculator = new();

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 120, 90, 110, 110, 120)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 105, 130, 95, 110, 110, 130)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 110, 115, 95, 110, 110, 115)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 95, 110, 85, 105, 105, 110)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 110, 80, 90, 90, 80)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 95, 112, 70, 90, 90, 70)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 90, 110, 82, 90, 90, 82)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 120, 135, 100, 115, 115, 95)]
    public void ContinuingCorrectionAccumulatesIndependentCoordinatesAndPreservesContext(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close,
        decimal expectedBody, decimal expectedWick)
    {
        var state = Initial(side);
        var next = Candle(16, open, high, low, close);

        var result = calculator.Evaluate(state, next);

        Assert.Equal(NasdaqPostInvalidationCorrectionTransitionKind.ContinuingCorrection, result.Kind);
        Assert.Null(result.Candidate);
        var continued = Assert.IsType<NasdaqPostInvalidationCorrectionState>(result.ContinuingCorrection);
        Assert.NotSame(state, continued);
        Assert.Equal(expectedBody, continued.CorrectionGeometry.StructuralPrice);
        Assert.Equal(expectedWick, continued.CorrectionGeometry.ProtectionAnchor);
        Assert.Equal(2, continued.CorrectionTurnCandles.Count);
        Assert.Same(state.CorrectionStartCandle, continued.CorrectionTurnCandles[0]);
        Assert.Same(next, continued.CorrectionTurnCandles[1]);
        Assert.Same(next, continued.LastProcessedCandle);
        Assert.Same(state.InvalidatingCandle, continued.InvalidatingCandle);
        Assert.Same(state.OriginGeometry, continued.OriginGeometry);
        Assert.Same(state.FrozenImpulseTerminal, continued.FrozenImpulseTerminal);
        Assert.Equal(state.CandidateSide, continued.CandidateSide);
        Assert.Equal(state.ImpulseTerminalSide, continued.ImpulseTerminalSide);
        Assert.Single(state.CorrectionTurnCandles);
        Assert.Equal(side == StructuralCandidateExtremeSide.Upper ? 95m : 115m,
            state.CorrectionGeometry.StructuralPrice);
        Assert.Equal(side == StructuralCandidateExtremeSide.Upper ? 105m : 95m,
            state.CorrectionGeometry.ProtectionAnchor);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 130, 90, 99, 95, 130)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 100, 90, 90, 95, 105)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 80, 100, 60, 90, 115, 60)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 120, 130, 100, 125, 115, 95)]
    public void FirstOppositeBodyFormsProvisionalCandidateWithTerminalWickButWithoutTerminalBody(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close,
        decimal expectedBody, decimal expectedWick)
    {
        var state = Initial(side);
        var terminal = Candle(16, open, high, low, close);

        var result = calculator.Evaluate(state, terminal);

        Assert.Equal(NasdaqPostInvalidationCorrectionTransitionKind.CandidateProvisional, result.Kind);
        Assert.Null(result.ContinuingCorrection);
        var candidate = Assert.IsType<NasdaqPostInvalidationCandidateState>(result.Candidate);
        Assert.Equal(side, candidate.CandidateSide);
        Assert.Equal(expectedBody, candidate.CandidateGeometry.StructuralPrice);
        Assert.Equal(expectedWick, candidate.CandidateGeometry.ProtectionAnchor);
        Assert.Single(candidate.CorrectionTurnCandles);
        Assert.Same(state.CorrectionStartCandle, candidate.CorrectionTurnCandles[0]);
        Assert.Same(terminal, candidate.TerminalCandle);
        Assert.Same(terminal, candidate.LastProcessedCandle);
        Assert.Same(state.InvalidatingCandle, candidate.InvalidatingCandle);
        Assert.Same(state.OriginGeometry, candidate.OriginGeometry);
        Assert.Same(state.FrozenImpulseTerminal, candidate.FrozenImpulseTerminal);
        Assert.Equal(state.ImpulseTerminalSide, candidate.ImpulseTerminalSide);
        Assert.Equal(state.CorrectionGeometry.StructuralPrice, candidate.CandidateGeometry.StructuralPrice);
        Assert.Equal(side == StructuralCandidateExtremeSide.Upper ? 70m : 130m,
            candidate.FrozenImpulseTerminal.StructuralPrice);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Upper)]
    [InlineData(StructuralCandidateExtremeSide.Lower)]
    public void ExactDojiRemainsCorrectionMemberAndDoesNotFormCandidate(
        StructuralCandidateExtremeSide side)
    {
        var state = Initial(side);
        var doji = Candle(16, 100, 140, 60, 100);
        var result = calculator.Evaluate(state, doji);

        Assert.Equal(NasdaqPostInvalidationCorrectionTransitionKind.ContinuingCorrection, result.Kind);
        Assert.Null(result.Candidate);
        Assert.Same(doji, result.ContinuingCorrection!.LastProcessedCandle);
        Assert.Equal(side == StructuralCandidateExtremeSide.Upper ? 140m : 60m,
            result.ContinuingCorrection.CorrectionGeometry.ProtectionAnchor);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Upper, 95, 105, 90, 90, 95, 105)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 115, 120, 95, 120, 115, 95)]
    public void EqualBodyAndWickLevelsNeedNoTerminalCandleTieBreak(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close,
        decimal expectedBody, decimal expectedWick)
    {
        var state = Initial(side);
        var result = calculator.Evaluate(state, Candle(16, open, high, low, close));

        Assert.Equal(NasdaqPostInvalidationCorrectionTransitionKind.CandidateProvisional, result.Kind);
        Assert.Equal(expectedBody, result.Candidate!.CandidateGeometry.StructuralPrice);
        Assert.Equal(expectedWick, result.Candidate.CandidateGeometry.ProtectionAnchor);
        Assert.Single(result.Candidate.CorrectionTurnCandles);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Upper)]
    [InlineData(StructuralCandidateExtremeSide.Lower)]
    public void ExactNonzeroOppositeBodyFormsCandidateWithoutNearDojiThreshold(
        StructuralCandidateExtremeSide side)
    {
        var state = Initial(side);
        var close = side == StructuralCandidateExtremeSide.Upper
            ? 0.9999999999999999999999999999m
            : 1.0000000000000000000000000001m;
        var result = calculator.Evaluate(state, Candle(16, 1m, 130, 0, close));

        Assert.Equal(NasdaqPostInvalidationCorrectionTransitionKind.CandidateProvisional, result.Kind);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 120, 90, 110, 115, 130, 100, 105, 110, 130)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 110, 80, 90, 85, 100, 60, 95, 90, 60)]
    public void CandidateAfterContinuedCorrectionKeepsAllBodyMembersButOnlyTerminalWick(
        StructuralCandidateExtremeSide side,
        decimal memberOpen, decimal memberHigh, decimal memberLow, decimal memberClose,
        decimal terminalOpen, decimal terminalHigh, decimal terminalLow, decimal terminalClose,
        decimal expectedBody, decimal expectedWick)
    {
        var initial = Initial(side);
        var member = Candle(16, memberOpen, memberHigh, memberLow, memberClose);
        var continued = calculator.Evaluate(initial, member).ContinuingCorrection!;
        var terminal = Candle(20, terminalOpen, terminalHigh, terminalLow, terminalClose);

        var result = calculator.Evaluate(continued, terminal);

        Assert.Equal(NasdaqPostInvalidationCorrectionTransitionKind.CandidateProvisional, result.Kind);
        Assert.Equal(expectedBody, result.Candidate!.CandidateGeometry.StructuralPrice);
        Assert.Equal(expectedWick, result.Candidate.CandidateGeometry.ProtectionAnchor);
        Assert.Equal(2, result.Candidate.CorrectionTurnCandles.Count);
        Assert.Same(member, result.Candidate.CorrectionTurnCandles[1]);
        Assert.Same(terminal, result.Candidate.TerminalCandle);
        Assert.Same(terminal, result.Candidate.LastProcessedCandle);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<Candle>)result.Candidate.CorrectionTurnCandles).Add(terminal));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Upper)]
    [InlineData(StructuralCandidateExtremeSide.Lower)]
    public void SameOrEarlierCandleAndWrongSeriesAreRejected(StructuralCandidateExtremeSide side)
    {
        var state = Initial(side);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(state, state.LastProcessedCandle));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(state, Candle(8, 100, 110, 90, 100)));
        var wrongSymbol = new Candle(Provider, new MarketSymbol("OTHER"), H4,
            Start.AddHours(16), Start.AddHours(20), 100, 110, 90, 100, null);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(state, wrongSymbol));
        var wrongTimeframe = new Candle(Provider, Symbol, new Timeframe(1, TimeframeUnit.Hour),
            Start.AddHours(16), Start.AddHours(17), 100, 110, 90, 100, null);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(state, wrongTimeframe));
    }

    [Fact]
    public void LaterNoncontiguousCandleAndRepeatedEvaluationAreDeterministic()
    {
        var state = Initial(StructuralCandidateExtremeSide.Upper);
        var next = Candle(24, 100, 110, 90, 105);
        var first = calculator.Evaluate(state, next);
        var repeated = calculator.Evaluate(state, next);
        _ = calculator.Evaluate(first.ContinuingCorrection!, Candle(28, 105, 130, 90, 100));

        Assert.Equal(first.Kind, repeated.Kind);
        Assert.Equal(first.ContinuingCorrection!.CorrectionGeometry,
            repeated.ContinuingCorrection!.CorrectionGeometry);
        Assert.Same(next, first.ContinuingCorrection.LastProcessedCandle);
        Assert.Equal(110m, first.ContinuingCorrection.CorrectionGeometry.ProtectionAnchor);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<Candle>)first.ContinuingCorrection.CorrectionTurnCandles).Add(next));
    }

    private static NasdaqPostInvalidationCorrectionState Initial(StructuralCandidateExtremeSide side)
    {
        var oldSide = side == StructuralCandidateExtremeSide.Upper
            ? StructuralCandidateExtremeSide.Lower
            : StructuralCandidateExtremeSide.Upper;
        var bullish = oldSide == StructuralCandidateExtremeSide.Lower;
        var originCandle = bullish ? Candle(0, 100, 110, 95, 110) : Candle(0, 100, 110, 85, 90);
        var oldCandidate = bullish ? Candle(4, 95, 100, 90, 96) : Candle(4, 105, 115, 100, 110);
        var invalidating = bullish ? Candle(8, 100, 110, 80, 90) : Candle(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version,
            Provider, Symbol, invalidating.OpenTimeUtc, [originCandle.OpenTimeUtc],
            invalidating.CloseTimeUtc, "review:origin");
        var cursor = new MultiTimeframeCandleReplayCursor(
            [new CandleSeries(Provider, Symbol, H4, [originCandle, oldCandidate, invalidating])]);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;
        var context = new CreateStrategyReplayContextUseCase().Execute(definition, frame!, [observation]);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(
            context, H4, bullish ? 130 : 70, bullish ? 95 : 105, bullish ? 140 : 75,
            [oldCandidate], oldSide);
        var origin = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, origin);
        var start = bullish ? Candle(12, 70, 105, 65, 95) : Candle(12, 130, 145, 95, 115);
        var transition = new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, start);
        return new NasdaqPostInvalidationCorrectionInitializer().Initialize(transition);
    }

    private static Candle Candle(int openHour, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, H4, Start.AddHours(openHour), Start.AddHours(openHour + 4),
            open, high, low, close, null);
}

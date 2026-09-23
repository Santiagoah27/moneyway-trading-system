using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostInvalidationCandidateContinuationCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostInvalidationCandidateContinuationCalculator calculator = new();

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 60, 100)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 130, 60, 100)]
    public void NoEventCreatesNewCandidateWithOnlyCursorAdvanced(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var candidate = Candidate(side);
        var incoming = Candle(20, open, high, low, close);

        var next = calculator.Evaluate(candidate, incoming);

        Assert.NotSame(candidate, next);
        Assert.Same(incoming, next.LastProcessedCandle);
        Assert.Same(candidate.LastProcessedCandle, candidate.TerminalCandle);
        Assert.Same(candidate.Episode, next.Episode);
        Assert.Same(candidate.CandidateGeometry, next.CandidateGeometry);
        Assert.Same(candidate.TerminalCandle, next.TerminalCandle);
        Assert.Equal(candidate.CorrectionTurnCandles, next.CorrectionTurnCandles);
        Assert.Equal(candidate.CandidateSide, next.CandidateSide);
        Assert.Same(candidate.FrozenImpulseTerminal, next.FrozenImpulseTerminal);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 90, 140, 60, 100)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 110, 140, 60, 100)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 60, 100)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 90, 130, 60, 100)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 110, 130, 60, 100)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 130, 60, 100)]
    public void BodyDirectionIncludingDojiDoesNotChangeNoEventContinuation(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var incoming = Candle(20, open, high, low, close);

        Assert.Same(incoming, calculator.Evaluate(Candidate(side), incoming).LastProcessedCandle);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 60, 130)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 130, 60, 70)]
    public void EqualityAtAnchorAndFrozenTerminalIsStillNoEvent(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var incoming = Candle(20, open, high, low, close);

        var next = calculator.Evaluate(Candidate(side), incoming);

        Assert.Same(incoming, next.LastProcessedCandle);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 60, 131)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 130, 60, 69)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 50, 100)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 60, 100)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 50, 131)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 60, 69)]
    public void RejectsEveryOtherCandidateMatrixRow(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(Candidate(side), Candle(20, open, high, low, close)));
    }

    [Fact]
    public void EnforcesCursorChronologyAndSeriesAndPreservesSnapshotCursor()
    {
        var candidate = Candidate(StructuralCandidateExtremeSide.Lower);
        var incoming = Candle(24, 100, 140, 60, 100);
        var next = calculator.Evaluate(candidate, incoming);
        var before = new NasdaqH4ReconstructionSnapshot.Candidate(candidate);
        var after = new NasdaqH4ReconstructionSnapshot.Candidate(next);
        var same = Candle(16, 100, 140, 60, 100);
        var earlier = Candle(12, 100, 140, 60, 100);
        var wrongProvider = new Candle(new MarketDataProviderId("other"), Symbol, H4,
            Start.AddHours(20), Start.AddHours(24), 100, 140, 60, 100, null);
        var wrongSymbol = new Candle(Provider, new MarketSymbol("OTHER"), H4,
            Start.AddHours(20), Start.AddHours(24), 100, 140, 60, 100, null);
        var wrongTimeframe = new Candle(Provider, Symbol, new Timeframe(1, TimeframeUnit.Hour),
            Start.AddHours(20), Start.AddHours(21), 100, 140, 60, 100, null);

        Assert.Same(candidate.LastProcessedCandle, before.MarketCursor);
        Assert.Same(incoming, after.MarketCursor);
        Assert.Same(candidate.Episode, after.Episode);
        Assert.Same(candidate.TerminalCandle, next.TerminalCandle);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, same));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, earlier));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, wrongProvider));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, wrongSymbol));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, wrongTimeframe));
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

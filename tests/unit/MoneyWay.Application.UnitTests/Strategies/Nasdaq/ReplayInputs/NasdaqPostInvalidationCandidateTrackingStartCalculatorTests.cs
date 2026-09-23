using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostInvalidationCandidateTrackingStartCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostInvalidationCandidateTrackingStartCalculator calculator = new();

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 50, 101, 50)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 60, 90, 140)]
    public void StrictMigrationWithMatchingFirstTurnStartsTrackingDirectly(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close, decimal anchor)
    {
        var candidate = Candidate(side);
        var incoming = Candle(20, open, high, low, close);

        var tracking = calculator.Evaluate(candidate, incoming);
        var snapshot = new NasdaqH4ReconstructionSnapshot.RebuiltTracking(tracking);

        Assert.Same(incoming, tracking.MigrationCandle);
        Assert.Same(incoming, tracking.FirstTurnCandle);
        Assert.Same(incoming, tracking.LastProcessedCandle);
        Assert.Equal(anchor, tracking.KnownProtectionAnchor);
        Assert.Same(candidate.Episode, tracking.Episode);
        Assert.Same(candidate.FrozenImpulseTerminal, tracking.FrozenImpulseTerminal);
        Assert.Same(candidate.OriginGeometry, tracking.OriginGeometry);
        Assert.Same(incoming, snapshot.MarketCursor);
        Assert.Same(candidate.Episode, snapshot.Episode);
        Assert.Null(tracking.GetType().GetProperty("StructuralPrice"));
        Assert.Same(candidate.LastProcessedCandle, candidate.TerminalCandle);
        Assert.Equal(candidate.CandidateGeometry.ProtectionAnchor, side == StructuralCandidateExtremeSide.Lower ? 60m : 130m);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 50, 50)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 50, 90)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 60, 100)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 60, 110)]
    public void DojiAndOppositeBodyDoNotStartTracking(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(Candidate(side), Candle(20, open, high, low, close)));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 50, 131)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 140, 150, 50, 131)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 60, 69)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 60, 140, 60, 69)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 60, 100)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 130, 60, 100)]
    public void RejectsBreakoutAndNonMigratingRows(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(Candidate(side), Candle(20, open, high, low, close)));
    }

    [Fact]
    public void FrozenTerminalEqualityStillStartsTrackingWhenMigrationAndBodyAreStrict()
    {
        var lower = calculator.Evaluate(Candidate(StructuralCandidateExtremeSide.Lower), Candle(20, 100, 140, 50, 130));
        var upper = calculator.Evaluate(Candidate(StructuralCandidateExtremeSide.Upper), Candle(20, 100, 140, 60, 70));

        Assert.Equal(50m, lower.KnownProtectionAnchor);
        Assert.Equal(140m, upper.KnownProtectionAnchor);
    }

    [Fact]
    public void DirectTrackingIsCompatibleWithLaterTrackingTransitionAndCursorSafety()
    {
        var candidate = Candidate(StructuralCandidateExtremeSide.Lower);
        var first = Candle(20, 100, 140, 50, 101);
        var tracking = calculator.Evaluate(candidate, first);
        var later = Candle(24, 100, 140, 50, 100);
        var continued = new NasdaqPostInvalidationRebuiltCandidateTrackingTransitionCalculator().Evaluate(tracking, later);
        var wrongProvider = new Candle(new MarketDataProviderId("other"), Symbol, H4,
            Start.AddHours(24), Start.AddHours(28), 100, 140, 50, 100, null);

        Assert.Same(later, continued.Tracking!.LastProcessedCandle);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, candidate.LastProcessedCandle));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, Candle(12, 100, 140, 50, 100)));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, wrongProvider));
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

using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqDirectCandidateBreakoutCompletionCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqDirectCandidateBreakoutCompletionCalculator calculator = new();

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 130, 140, 60, 131)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 70, 130, 60, 69)]
    public void StrictNonMigratingBreakoutPreservesExistingCandidateGeometry(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var candidate = Candidate(side);
        var validating = Candle(20, open, high, low, close);

        var result = calculator.Evaluate(candidate, validating);

        Assert.Same(candidate, result.Candidate);
        Assert.Same(candidate.Episode, result.Candidate.Episode);
        Assert.Same(candidate.CandidateGeometry, result.ValidatedCandidate.CandidateGeometry);
        Assert.Equal(candidate.CandidateGeometry.StructuralPrice, result.ValidatedCandidate.StructuralPrice);
        Assert.Equal(candidate.CandidateGeometry.ProtectionAnchor, result.ValidatedCandidate.ProtectionAnchor);
        Assert.Same(validating, result.ValidatingCandle);
        Assert.Same(validating, result.LastProcessedCandle);
        Assert.True(result.ValidatedCandidate.IsValidated);
        Assert.Equal(side, result.ValidatedCandidate.CandidateSide);
        Assert.Same(candidate.LastProcessedCandle, candidate.TerminalCandle);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 140, 150, 60, 131)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 60, 130, 60, 69)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 131, 140, 60, 131)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 69, 130, 60, 69)]
    public void BodyDirectionDoesNotChangeStrictNonMigratingBreakoutEligibility(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var result = calculator.Evaluate(Candidate(side), Candle(20, open, high, low, close));

        Assert.True(result.ValidatedCandidate.IsValidated);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 60, 130)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 130, 60, 70)]
    public void FrozenTerminalEqualityDoesNotComplete(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(Candidate(side), Candle(20, open, high, low, close)));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 130, 140, 59, 131)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 70, 131, 60, 69)]
    public void StrictMigrationRejectsDirectCompletion(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(Candidate(side), Candle(20, open, high, low, close)));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 130, 140, 60, 131)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 70, 130, 60, 69)]
    public void EqualityAtCandidateAnchorDoesNotBlockDirectCompletion(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var result = calculator.Evaluate(Candidate(side), Candle(20, open, high, low, close));

        Assert.True(result.ValidatedCandidate.IsValidated);
    }

    [Fact]
    public void RejectsSameEarlierAndWrongSeriesCandles()
    {
        var candidate = Candidate(StructuralCandidateExtremeSide.Lower);
        var earlier = Candle(12, 130, 140, 60, 131);
        var wrongSymbol = new Candle(Provider, new MarketSymbol("OTHER"), H4,
            Start.AddHours(20), Start.AddHours(24), 130, 140, 60, 131, null);
        var wrongProvider = new Candle(new MarketDataProviderId("other"), Symbol, H4,
            Start.AddHours(20), Start.AddHours(24), 130, 140, 60, 131, null);
        var wrongTimeframe = new Candle(Provider, Symbol, new Timeframe(1, TimeframeUnit.Hour),
            Start.AddHours(20), Start.AddHours(21), 130, 140, 60, 131, null);

        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, candidate.LastProcessedCandle));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, earlier));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, wrongProvider));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, wrongSymbol));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, wrongTimeframe));
    }

    [Fact]
    public void CompletedDirectCandidatePreservesBranchSpecificProvenance()
    {
        var candidate = Candidate(StructuralCandidateExtremeSide.Lower);
        var completion = calculator.Evaluate(candidate, Candle(20, 130, 140, 60, 131));
        var snapshot = new NasdaqH4ReconstructionSnapshot.Completed.DirectCandidate(completion);

        Assert.Equal(NasdaqH4ReconstructionSnapshotKind.Completed, snapshot.Kind);
        Assert.Same(completion, snapshot.Result);
        Assert.Same(candidate.Episode, snapshot.Episode);
        Assert.Same(completion.ValidatingCandle, snapshot.MarketCursor);
        Assert.Same(completion.ValidatedCandidate, snapshot.ValidatedCandidate);
        Assert.Null(snapshot.GetType().GetProperty("Breakout"));
    }

    private static NasdaqPostInvalidationCandidateState Candidate(StructuralCandidateExtremeSide side)
    {
        var correction = InitialCorrection(side);
        var terminal = side == StructuralCandidateExtremeSide.Lower
            ? Candle(16, 80, 100, 60, 90)
            : Candle(16, 100, 130, 90, 99);
        return new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, terminal).Candidate!;
    }

    private static NasdaqPostInvalidationCorrectionState InitialCorrection(StructuralCandidateExtremeSide side)
    {
        var oldSide = side == StructuralCandidateExtremeSide.Upper
            ? StructuralCandidateExtremeSide.Lower
            : StructuralCandidateExtremeSide.Upper;
        var bullish = oldSide == StructuralCandidateExtremeSide.Lower;
        var origin = bullish ? Candle(0, 100, 110, 95, 110) : Candle(0, 100, 110, 85, 90);
        var oldCandidate = bullish ? Candle(4, 95, 100, 90, 96) : Candle(4, 105, 115, 100, 110);
        var invalidating = bullish ? Candle(8, 100, 110, 80, 90) : Candle(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version,
            Provider, Symbol, invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, [origin, oldCandidate, invalidating])]);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;
        var context = new CreateStrategyReplayContextUseCase().Execute(definition, frame!, [observation]);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(
            context, H4, bullish ? 130 : 70, bullish ? 95 : 105, bullish ? 140 : 75, [oldCandidate], oldSide);
        var originGeometry = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, originGeometry);
        var start = bullish ? Candle(12, 70, 105, 65, 95) : Candle(12, 130, 145, 95, 115);
        var transition = new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, start);
        return new NasdaqPostInvalidationCorrectionInitializer().Initialize(transition);
    }

    private static Candle Candle(int openHour, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, H4, Start.AddHours(openHour), Start.AddHours(openHour + 4), open, high, low, close, null);
}

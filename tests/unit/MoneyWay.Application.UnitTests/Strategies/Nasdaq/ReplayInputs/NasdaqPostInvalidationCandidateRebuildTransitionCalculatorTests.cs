using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostInvalidationCandidateRebuildTransitionCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostInvalidationCandidateRebuildTransitionCalculator calculator = new();

    [Fact]
    public void LowerCandidateStrictLowWithoutBreakoutCreatesPendingStateAndPreservesContext()
    {
        var candidate = Candidate(StructuralCandidateExtremeSide.Lower);
        var migration = Candle(20, 100, 120, 50, 90);

        var result = calculator.Evaluate(candidate, migration);

        Assert.Equal(50m, result.KnownProtectionAnchor);
        Assert.Same(migration, result.MigrationCandle);
        Assert.Same(migration, result.LastProcessedCandle);
        Assert.Same(candidate.InvalidatingCandle, result.InvalidatingCandle);
        Assert.Same(candidate.OriginGeometry, result.OriginGeometry);
        Assert.Same(candidate.FrozenImpulseTerminal, result.FrozenImpulseTerminal);
        Assert.Equal(candidate.CandidateSide, result.CandidateSide);
        Assert.Equal(candidate.ImpulseTerminalSide, result.ImpulseTerminalSide);
        Assert.Null(result.GetType().GetProperty("CandidateGeometry"));
        Assert.Null(result.GetType().GetProperty("CorrectionTurnCandles"));
        Assert.Equal(60m, candidate.CandidateGeometry.ProtectionAnchor);
        Assert.Equal(115m, candidate.CandidateGeometry.StructuralPrice);
        Assert.Same(candidate.LastProcessedCandle, candidate.TerminalCandle);
    }

    [Fact]
    public void UpperCandidateStrictHighWithoutBreakoutCreatesPendingState()
    {
        var candidate = Candidate(StructuralCandidateExtremeSide.Upper);
        var migration = Candle(20, 100, 140, 80, 100);

        var result = calculator.Evaluate(candidate, migration);

        Assert.Equal(140m, result.KnownProtectionAnchor);
        Assert.Same(migration, result.MigrationCandle);
        Assert.Same(candidate.FrozenImpulseTerminal, result.FrozenImpulseTerminal);
        Assert.Equal(StructuralCandidateExtremeSide.Upper, result.CandidateSide);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 120, 60, 90)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 130, 80, 100)]
    public void EqualCandidateAnchorDoesNotMigrate(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var candidate = Candidate(side);

        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, Candle(20, open, high, low, close)));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 50, 130)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 50, 70)]
    public void FrozenTerminalEqualityDoesNotValidateAndPermitsMigrationOnlyPath(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var candidate = Candidate(side);

        var result = calculator.Evaluate(candidate, Candle(20, open, high, low, close));

        Assert.Equal(side == StructuralCandidateExtremeSide.Lower ? 50m : 140m, result.KnownProtectionAnchor);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 120, 50, 90)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 120, 50, 100)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 120, 50, 110)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 80, 90)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 80, 110)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 80, 100)]
    public void MigrationWithoutBreakoutDoesNotDependOnBodyDirection(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        var candidate = Candidate(side);

        var result = calculator.Evaluate(candidate, Candle(20, open, high, low, close));

        Assert.Equal(side == StructuralCandidateExtremeSide.Lower ? low : high, result.KnownProtectionAnchor);
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 100, 140, 50, 131)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, 50, 69)]
    public void ValidatingBreakoutIsNotHandledByThisMigrationOnlyPrimitive(
        StructuralCandidateExtremeSide side, decimal open, decimal high, decimal low, decimal close)
    {
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(current: Candidate(side), candle: Candle(20, open, high, low, close)));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower)]
    [InlineData(StructuralCandidateExtremeSide.Upper)]
    public void SameEarlierAndWrongSeriesCandlesAreRejectedAndRepeatedEvaluationIsDeterministic(
        StructuralCandidateExtremeSide side)
    {
        var candidate = Candidate(side);
        var migration = side == StructuralCandidateExtremeSide.Lower
            ? Candle(20, 100, 120, 50, 90)
            : Candle(20, 100, 140, 80, 100);

        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, candidate.LastProcessedCandle));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, Candle(8, 100, 140, 50, 100)));
        var wrongSymbol = new Candle(Provider, new MarketSymbol("OTHER"), H4,
            Start.AddHours(20), Start.AddHours(24), 100, 140, 50, 100, null);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(candidate, wrongSymbol));

        var first = calculator.Evaluate(candidate, migration);
        var repeated = calculator.Evaluate(candidate, migration);
        Assert.Equal(first.KnownProtectionAnchor, repeated.KnownProtectionAnchor);
        Assert.Same(migration, repeated.LastProcessedCandle);
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
        var originCandle = bullish ? Candle(0, 100, 110, 95, 110) : Candle(0, 100, 110, 85, 90);
        var oldCandidate = bullish ? Candle(4, 95, 100, 90, 96) : Candle(4, 105, 115, 100, 110);
        var invalidating = bullish ? Candle(8, 100, 110, 80, 90) : Candle(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version,
            Provider, Symbol, invalidating.OpenTimeUtc, [originCandle.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4,
            [originCandle, oldCandidate, invalidating])]);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;
        var context = new CreateStrategyReplayContextUseCase().Execute(definition, frame!, [observation]);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(
            context, H4, bullish ? 130 : 70, bullish ? 95 : 105, bullish ? 140 : 75, [oldCandidate], oldSide);
        var origin = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, origin);
        var start = bullish ? Candle(12, 70, 105, 65, 95) : Candle(12, 130, 145, 95, 115);
        var transition = new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, start);
        return new NasdaqPostInvalidationCorrectionInitializer().Initialize(transition);
    }

    private static Candle Candle(int openHour, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, H4, Start.AddHours(openHour), Start.AddHours(openHour + 4), open, high, low, close, null);
}

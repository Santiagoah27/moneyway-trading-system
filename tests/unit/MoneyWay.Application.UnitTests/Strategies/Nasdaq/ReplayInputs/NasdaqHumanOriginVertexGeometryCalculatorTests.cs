using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanOriginVertexGeometryCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqHumanOriginVertexGeometryCalculator calculator = new();

    [Fact]
    public void BullishInvalidationUsesHighSideBodyAndWickFromDifferentReviewedMembers()
    {
        var bodySource = Candle(0, 100, 110, 95, 110);
        var wickSource = Candle(4, 107, 120, 100, 108);
        var unselected = Candle(8, 95, 200, 90, 96);
        var invalidating = Candle(12, 100, 250, 80, 89);
        var (members, boundary) = Prepare([bodySource, wickSource, unselected, invalidating], [4, 0], 16,
            StructuralCandidateExtremeSide.Lower);

        var result = calculator.Evaluate(members, boundary);

        Assert.Equal(StructuralCandidateTurnBoundaryKind.StructureInvalidated, boundary.Kind);
        Assert.Equal(StructuralTurnBodyCoordinateSide.Upper, result.Side);
        Assert.Equal(110m, result.StructuralPrice);
        Assert.Equal(120m, result.ProtectionAnchor);
        Assert.Equal(result, calculator.Evaluate(members, boundary));
    }

    [Fact]
    public void BearishInvalidationUsesLowSideBodyAndWickFromDifferentReviewedMembers()
    {
        var bodySource = Candle(0, 100, 110, 85, 90);
        var wickSource = Candle(4, 92, 105, 80, 95);
        var unselected = Candle(8, 105, 115, 20, 110);
        var invalidating = Candle(12, 100, 130, 10, 111);
        var candles = new[] { bodySource, wickSource, unselected, invalidating };
        var (members, boundary) = Prepare(candles, [0, 4], 16,
            StructuralCandidateExtremeSide.Upper);
        var (reversedMembers, reversedBoundary) = Prepare(candles, [4, 0], 16,
            StructuralCandidateExtremeSide.Upper);

        var result = calculator.Evaluate(members, boundary);

        Assert.Equal(StructuralCandidateTurnBoundaryKind.StructureInvalidated, boundary.Kind);
        Assert.Equal(StructuralTurnBodyCoordinateSide.Lower, result.Side);
        Assert.Equal(90m, result.StructuralPrice);
        Assert.Equal(80m, result.ProtectionAnchor);
        Assert.Equal(result, calculator.Evaluate(reversedMembers, reversedBoundary));
    }

    [Fact]
    public void ExactEqualExtremaAndReversedHumanInputOrderProduceOneEquivalentGeometry()
    {
        var first = Candle(0, 100, 120, 95, 110);
        var second = Candle(4, 110, 120, 100, 108);
        var candidate = Candle(8, 95, 100, 90, 96);
        var invalidating = Candle(12, 100, 150, 80, 89);
        var candles = new[] { first, second, candidate, invalidating };
        var (oneOrder, firstBoundary) = Prepare(candles, [0, 4], 16, StructuralCandidateExtremeSide.Lower);
        var (reverseOrder, secondBoundary) = Prepare(candles, [4, 0], 16, StructuralCandidateExtremeSide.Lower);

        var firstResult = calculator.Evaluate(oneOrder, firstBoundary);
        var secondResult = calculator.Evaluate(reverseOrder, secondBoundary);

        Assert.Equal(firstResult, secondResult);
        Assert.Equal(110m, firstResult.StructuralPrice);
        Assert.Equal(120m, firstResult.ProtectionAnchor);
    }

    [Fact]
    public void RejectsUnrelatedInvalidationAndNonInvalidatingBoundary()
    {
        var first = Candle(0, 100, 110, 95, 110);
        var second = Candle(4, 107, 120, 100, 108);
        var candidate = Candle(8, 95, 100, 90, 96);
        var invalidating = Candle(12, 100, 150, 80, 89);
        var laterInvalidating = Candle(16, 100, 160, 70, 89);
        var (members, boundary) = Prepare([first, second, candidate, invalidating], [0, 4], 16,
            StructuralCandidateExtremeSide.Lower);
        var (_, laterBoundary) = Prepare([first, second, candidate, invalidating, laterInvalidating], [0, 4], 20,
            StructuralCandidateExtremeSide.Lower);
        var nonInvalidating = Candle(12, 100, 120, 90, 95);
        var (_, nonInvalidatingBoundary) = Prepare([first, second, candidate, nonInvalidating], [0, 4], 16,
            StructuralCandidateExtremeSide.Lower);

        Assert.Equal(StructuralCandidateTurnBoundaryKind.StructureInvalidated, boundary.Kind);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(members, laterBoundary));
        Assert.NotEqual(StructuralCandidateTurnBoundaryKind.StructureInvalidated, nonInvalidatingBoundary.Kind);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(members, nonInvalidatingBoundary));
    }

    [Fact]
    public void LaterUnselectedCandlesCannotChangeTheGeometryAtTheSameReplayBoundary()
    {
        var first = Candle(0, 100, 110, 95, 110);
        var second = Candle(4, 107, 120, 100, 108);
        var candidate = Candle(8, 95, 100, 90, 96);
        var invalidating = Candle(12, 100, 150, 80, 89);
        var future = Candle(16, 100, 999, 1, 150);
        var (before, firstBoundary) = Prepare([first, second, candidate, invalidating], [0, 4], 16,
            StructuralCandidateExtremeSide.Lower);
        var (extended, secondBoundary) = Prepare([first, second, candidate, invalidating, future], [0, 4], 16,
            StructuralCandidateExtremeSide.Lower);

        Assert.Equal(calculator.Evaluate(before, firstBoundary), calculator.Evaluate(extended, secondBoundary));
    }

    private static (NasdaqHumanOriginVertexMemberResolution Members, StructuralCandidateTurnBoundaryResult Boundary)
        Prepare(Candle[] candles, int[] selectedHours, int asOfHour, StructuralCandidateExtremeSide candidateSide)
    {
        var observation = new NasdaqHumanOriginVertexObservation(
            MoneyWayNasdaqStrategyDefinition.Instance.StrategyId,
            MoneyWayNasdaqStrategyDefinition.Instance.Version,
            Provider, Symbol, Start.AddHours(asOfHour - 4), selectedHours.Select(hour => Start.AddHours(hour)),
            Start.AddHours(asOfHour), "review:origin");
        var context = Context(candles, asOfHour, observation);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var bullish = candidateSide == StructuralCandidateExtremeSide.Lower;
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(
            context, H4, bullish ? 130 : 70, bullish ? 90 : 110, bullish ? 140 : 75,
            [candles[2]], candidateSide);
        return (members, boundary);
    }

    private static StrategyReplayContext Context(Candle[] candles, int asOfHour,
        NasdaqHumanOriginVertexObservation observation)
    {
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, candles)]);
        while (cursor.TryAdvance(out var frame))
        {
            if (frame!.AsOfUtc == Start.AddHours(asOfHour))
                return new CreateStrategyReplayContextUseCase().Execute(
                    MoneyWayNasdaqStrategyDefinition.Instance, frame, new IStrategyReplayInputObservation[] { observation });
        }
        throw new InvalidOperationException("Test replay boundary was not found.");
    }

    private static Candle Candle(int openHour, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, H4, Start.AddHours(openHour), Start.AddHours(openHour + 4),
            open, high, low, close, null);
}

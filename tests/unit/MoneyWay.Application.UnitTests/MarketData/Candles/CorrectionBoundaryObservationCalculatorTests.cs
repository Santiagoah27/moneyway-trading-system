using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.MarketData.Candles;

public sealed class CorrectionBoundaryObservationCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("provider");
    private static readonly MarketSymbol Symbol = new("NQ");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly CorrectionBoundaryObservationCalculator _calculator = new();

    [Fact]
    public void Evaluate_FloorWithLowerLowAndBullishBody_ReportsUpdatedFloorAndBullishDirection()
    {
        var result = _calculator.Evaluate(
            100m,
            Candle(open: 100m, high: 120m, low: 90m, close: 110m),
            CorrectionOriginExtremeSide.Floor);

        Assert.Equal(CorrectionOriginExtremeSide.Floor, result.Side);
        Assert.Equal(100m, result.PreviousExtreme);
        Assert.Equal(90m, result.ResultingExtreme);
        Assert.True(result.WasExtremeUpdated);
        Assert.Equal(CandleBodyDirection.Bullish, result.BodyDirection);
    }

    [Fact]
    public void Evaluate_FloorWithUnchangedLowAndBearishBody_ReportsUnchangedFloorAndBearishDirection()
    {
        var result = _calculator.Evaluate(
            90m,
            Candle(open: 110m, high: 120m, low: 90m, close: 100m),
            CorrectionOriginExtremeSide.Floor);

        Assert.Equal(90m, result.ResultingExtreme);
        Assert.False(result.WasExtremeUpdated);
        Assert.Equal(CandleBodyDirection.Bearish, result.BodyDirection);
    }

    [Fact]
    public void Evaluate_FloorWithLowerLowAndExactDoji_ReportsUpdatedFloorAndNeutralDirection()
    {
        var result = _calculator.Evaluate(
            100m,
            Candle(open: 100m, high: 120m, low: 90m, close: 100m),
            CorrectionOriginExtremeSide.Floor);

        Assert.Equal(90m, result.ResultingExtreme);
        Assert.True(result.WasExtremeUpdated);
        Assert.Equal(CandleBodyDirection.Neutral, result.BodyDirection);
    }

    [Fact]
    public void Evaluate_CeilingWithHigherHighAndBearishBody_ReportsUpdatedCeilingAndBearishDirection()
    {
        var result = _calculator.Evaluate(
            100m,
            Candle(open: 110m, high: 120m, low: 90m, close: 100m),
            CorrectionOriginExtremeSide.Ceiling);

        Assert.Equal(CorrectionOriginExtremeSide.Ceiling, result.Side);
        Assert.Equal(100m, result.PreviousExtreme);
        Assert.Equal(120m, result.ResultingExtreme);
        Assert.True(result.WasExtremeUpdated);
        Assert.Equal(CandleBodyDirection.Bearish, result.BodyDirection);
    }

    [Fact]
    public void Evaluate_CeilingWithUnchangedHighAndBullishBody_ReportsUnchangedCeilingAndBullishDirection()
    {
        var result = _calculator.Evaluate(
            120m,
            Candle(open: 100m, high: 120m, low: 90m, close: 110m),
            CorrectionOriginExtremeSide.Ceiling);

        Assert.Equal(120m, result.ResultingExtreme);
        Assert.False(result.WasExtremeUpdated);
        Assert.Equal(CandleBodyDirection.Bullish, result.BodyDirection);
    }

    [Fact]
    public void Evaluate_CeilingWithHigherHighAndExactDoji_ReportsUpdatedCeilingAndNeutralDirection()
    {
        var result = _calculator.Evaluate(
            100m,
            Candle(open: 100m, high: 120m, low: 90m, close: 100m),
            CorrectionOriginExtremeSide.Ceiling);

        Assert.Equal(120m, result.ResultingExtreme);
        Assert.True(result.WasExtremeUpdated);
        Assert.Equal(CandleBodyDirection.Neutral, result.BodyDirection);
    }

    [Fact]
    public void Evaluate_ExactExtremeEquality_PreservesCurrentExtreme()
    {
        var floor = _calculator.Evaluate(
            90m,
            Candle(open: 100m, high: 120m, low: 90m, close: 110m),
            CorrectionOriginExtremeSide.Floor);
        var ceiling = _calculator.Evaluate(
            120m,
            Candle(open: 110m, high: 120m, low: 90m, close: 100m),
            CorrectionOriginExtremeSide.Ceiling);

        Assert.False(floor.WasExtremeUpdated);
        Assert.Equal(90m, floor.ResultingExtreme);
        Assert.False(ceiling.WasExtremeUpdated);
        Assert.Equal(120m, ceiling.ResultingExtreme);
    }

    [Fact]
    public void Evaluate_WickChangesCanUpdateExtremeWithoutChangingBodyDirection()
    {
        var baseline = _calculator.Evaluate(
            100m,
            Candle(open: 100m, high: 110m, low: 100m, close: 105m),
            CorrectionOriginExtremeSide.Floor);
        var changedWick = _calculator.Evaluate(
            100m,
            Candle(open: 100m, high: 110m, low: 90m, close: 105m),
            CorrectionOriginExtremeSide.Floor);

        Assert.Equal(CandleBodyDirection.Bullish, baseline.BodyDirection);
        Assert.Equal(baseline.BodyDirection, changedWick.BodyDirection);
        Assert.False(baseline.WasExtremeUpdated);
        Assert.True(changedWick.WasExtremeUpdated);
    }

    [Fact]
    public void Evaluate_BodyChangesCanChangeDirectionWithoutChangingTrackedExtreme()
    {
        var bullish = _calculator.Evaluate(
            90m,
            Candle(open: 100m, high: 120m, low: 90m, close: 110m),
            CorrectionOriginExtremeSide.Floor);
        var bearish = _calculator.Evaluate(
            90m,
            Candle(open: 110m, high: 120m, low: 90m, close: 100m),
            CorrectionOriginExtremeSide.Floor);

        Assert.Equal(90m, bullish.ResultingExtreme);
        Assert.Equal(bullish.ResultingExtreme, bearish.ResultingExtreme);
        Assert.False(bullish.WasExtremeUpdated);
        Assert.False(bearish.WasExtremeUpdated);
        Assert.Equal(CandleBodyDirection.Bullish, bullish.BodyDirection);
        Assert.Equal(CandleBodyDirection.Bearish, bearish.BodyDirection);
    }

    [Fact]
    public void Evaluate_RepeatedEvaluation_ReturnsIdenticalResult()
    {
        var candle = Candle(open: 100m, high: 120m, low: 90m, close: 110m);

        var first = _calculator.Evaluate(100m, candle, CorrectionOriginExtremeSide.Floor);
        var second = _calculator.Evaluate(100m, candle, CorrectionOriginExtremeSide.Floor);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Result_DoesNotExposeCorrectionStartOrStructuralValidationOutcome()
    {
        var propertyNames = typeof(CorrectionBoundaryObservationResult)
            .GetProperties()
            .Select(property => property.Name);

        Assert.DoesNotContain("StartsCorrection", propertyNames);
        Assert.DoesNotContain("CorrectionStarted", propertyNames);
        Assert.DoesNotContain("StructuralValidation", propertyNames);
    }

    [Fact]
    public void Evaluate_NullCandleOrUnsupportedSide_UsesInputValidationConventions()
    {
        Assert.Throws<ArgumentNullException>(() => _calculator.Evaluate(
            100m,
            null!,
            CorrectionOriginExtremeSide.Floor));
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Evaluate(
            100m,
            Candle(open: 100m, high: 120m, low: 90m, close: 110m),
            (CorrectionOriginExtremeSide)99));
    }

    private static Candle Candle(decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, Minute, Start, Start.AddMinutes(1), open, high, low, close, null);
}

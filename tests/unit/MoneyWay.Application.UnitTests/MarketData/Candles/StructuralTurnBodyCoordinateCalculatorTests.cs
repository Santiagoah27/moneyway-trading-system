using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.MarketData.Candles;

public sealed class StructuralTurnBodyCoordinateCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("provider");
    private static readonly MarketSymbol Symbol = new("NQ");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly StructuralTurnBodyCoordinateCalculator _calculator = new();

    [Fact]
    public void Evaluate_LowerSideWithSingleBullishCandle_ReturnsOpen()
    {
        var result = _calculator.Evaluate(
            [CandleAt(0, open: 100m, high: 120m, low: 90m, close: 110m)],
            StructuralTurnBodyCoordinateSide.Lower);

        Assert.Equal(100m, result.StructuralPrice);
        Assert.Equal(StructuralTurnBodyCoordinateSide.Lower, result.Side);
    }

    [Fact]
    public void Evaluate_UpperSideWithSingleBearishCandle_ReturnsOpen()
    {
        var result = _calculator.Evaluate(
            [CandleAt(0, open: 110m, high: 120m, low: 90m, close: 100m)],
            StructuralTurnBodyCoordinateSide.Upper);

        Assert.Equal(110m, result.StructuralPrice);
        Assert.Equal(StructuralTurnBodyCoordinateSide.Upper, result.Side);
    }

    [Fact]
    public void Evaluate_LowerSideAcrossMultipleCandles_ReturnsLowestBodyLowerEdge()
    {
        var result = _calculator.Evaluate(
            [
                CandleAt(0, open: 110m, high: 120m, low: 90m, close: 100m),
                CandleAt(1, open: 99m, high: 130m, low: 80m, close: 120m)
            ],
            StructuralTurnBodyCoordinateSide.Lower);

        Assert.Equal(99m, result.StructuralPrice);
    }

    [Fact]
    public void Evaluate_UpperSideAcrossMultipleCandles_ReturnsHighestBodyUpperEdge()
    {
        var result = _calculator.Evaluate(
            [
                CandleAt(0, open: 110m, high: 120m, low: 90m, close: 100m),
                CandleAt(1, open: 99m, high: 130m, low: 80m, close: 120m)
            ],
            StructuralTurnBodyCoordinateSide.Upper);

        Assert.Equal(120m, result.StructuralPrice);
    }

    [Fact]
    public void Evaluate_LowerSide_IgnoresHighWickGeometry()
    {
        var baseline = _calculator.Evaluate(
            [CandleAt(0, open: 100m, high: 110m, low: 90m, close: 105m)],
            StructuralTurnBodyCoordinateSide.Lower);
        var alteredWick = _calculator.Evaluate(
            [CandleAt(0, open: 100m, high: 1000m, low: 90m, close: 105m)],
            StructuralTurnBodyCoordinateSide.Lower);

        Assert.Equal(baseline.StructuralPrice, alteredWick.StructuralPrice);
    }

    [Fact]
    public void Evaluate_UpperSide_IgnoresLowWickGeometry()
    {
        var baseline = _calculator.Evaluate(
            [CandleAt(0, open: 105m, high: 110m, low: 90m, close: 100m)],
            StructuralTurnBodyCoordinateSide.Upper);
        var alteredWick = _calculator.Evaluate(
            [CandleAt(0, open: 105m, high: 110m, low: 1m, close: 100m)],
            StructuralTurnBodyCoordinateSide.Upper);

        Assert.Equal(baseline.StructuralPrice, alteredWick.StructuralPrice);
    }

    [Fact]
    public void Evaluate_MixedBodyDirections_UsesOnlyOpenAndClose()
    {
        var candles = new[]
        {
            CandleAt(0, open: 100m, high: 150m, low: 50m, close: 130m),
            CandleAt(1, open: 140m, high: 200m, low: 10m, close: 90m),
            CandleAt(2, open: 95m, high: 500m, low: 1m, close: 110m)
        };

        var lower = _calculator.Evaluate(candles, StructuralTurnBodyCoordinateSide.Lower);
        var upper = _calculator.Evaluate(candles, StructuralTurnBodyCoordinateSide.Upper);

        Assert.Equal(90m, lower.StructuralPrice);
        Assert.Equal(140m, upper.StructuralPrice);
    }

    [Fact]
    public void Evaluate_TiedBodyCoordinate_DoesNotExposeCandleIdentityTieBreaking()
    {
        var result = _calculator.Evaluate(
            [
                CandleAt(0, open: 100m, high: 120m, low: 90m, close: 110m),
                CandleAt(1, open: 110m, high: 130m, low: 80m, close: 100m)
            ],
            StructuralTurnBodyCoordinateSide.Upper);

        Assert.Equal(110m, result.StructuralPrice);
        Assert.DoesNotContain(
            typeof(StructuralTurnBodyCoordinateResult).GetProperties(),
            property => property.PropertyType == typeof(Candle));
    }

    [Fact]
    public void Evaluate_RepeatedEvaluation_ReturnsIdenticalResult()
    {
        var candles = new[]
        {
            CandleAt(0, open: 100m, high: 110m, low: 90m, close: 105m),
            CandleAt(1, open: 95m, high: 120m, low: 80m, close: 115m)
        };

        var first = _calculator.Evaluate(candles, StructuralTurnBodyCoordinateSide.Lower);
        var second = _calculator.Evaluate(candles, StructuralTurnBodyCoordinateSide.Lower);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Evaluate_NullEmptyOrUnsupportedSide_UsesInputValidationConventions()
    {
        Assert.Throws<ArgumentNullException>(() => _calculator.Evaluate(null!, StructuralTurnBodyCoordinateSide.Lower));
        Assert.Throws<ArgumentException>(() => _calculator.Evaluate([], StructuralTurnBodyCoordinateSide.Lower));
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Evaluate(
            [CandleAt(0, open: 100m, high: 110m, low: 90m, close: 105m)],
            (StructuralTurnBodyCoordinateSide)99));
    }

    private static Candle CandleAt(int minuteOffset, decimal open, decimal high, decimal low, decimal close) =>
        new(
            Provider,
            Symbol,
            Minute,
            Start.AddMinutes(minuteOffset),
            Start.AddMinutes(minuteOffset + 1),
            open,
            high,
            low,
            close,
            null);
}

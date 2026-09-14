using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.MarketData.Candles;

public sealed class StructuralTurnProtectionAnchorCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("provider");
    private static readonly MarketSymbol Symbol = new("NQ");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly StructuralTurnProtectionAnchorCalculator _calculator = new();

    [Fact]
    public void Evaluate_LowerSideWithSingleCandle_ReturnsLow()
    {
        var result = _calculator.Evaluate(
            [CandleAt(0, open: 100m, high: 120m, low: 90m, close: 110m)],
            StructuralTurnProtectionSide.Lower);

        Assert.Equal(90m, result.ProtectionAnchor);
        Assert.Equal(StructuralTurnProtectionSide.Lower, result.Side);
    }

    [Fact]
    public void Evaluate_UpperSideWithSingleCandle_ReturnsHigh()
    {
        var result = _calculator.Evaluate(
            [CandleAt(0, open: 110m, high: 120m, low: 90m, close: 100m)],
            StructuralTurnProtectionSide.Upper);

        Assert.Equal(120m, result.ProtectionAnchor);
        Assert.Equal(StructuralTurnProtectionSide.Upper, result.Side);
    }

    [Fact]
    public void Evaluate_LowerSideAcrossMultipleCandles_ReturnsMinimumLow()
    {
        var result = _calculator.Evaluate(
            [
                CandleAt(0, open: 110m, high: 120m, low: 90m, close: 100m),
                CandleAt(1, open: 99m, high: 130m, low: 80m, close: 120m)
            ],
            StructuralTurnProtectionSide.Lower);

        Assert.Equal(80m, result.ProtectionAnchor);
    }

    [Fact]
    public void Evaluate_UpperSideAcrossMultipleCandles_ReturnsMaximumHigh()
    {
        var result = _calculator.Evaluate(
            [
                CandleAt(0, open: 110m, high: 120m, low: 90m, close: 100m),
                CandleAt(1, open: 99m, high: 130m, low: 80m, close: 120m)
            ],
            StructuralTurnProtectionSide.Upper);

        Assert.Equal(130m, result.ProtectionAnchor);
    }

    [Fact]
    public void Evaluate_LowerSide_IgnoresOpenAndCloseWhenLowIsUnchanged()
    {
        var baseline = _calculator.Evaluate(
            [CandleAt(0, open: 100m, high: 110m, low: 90m, close: 105m)],
            StructuralTurnProtectionSide.Lower);
        var alteredBody = _calculator.Evaluate(
            [CandleAt(0, open: 95m, high: 120m, low: 90m, close: 115m)],
            StructuralTurnProtectionSide.Lower);

        Assert.Equal(baseline.ProtectionAnchor, alteredBody.ProtectionAnchor);
    }

    [Fact]
    public void Evaluate_UpperSide_IgnoresOpenAndCloseWhenHighIsUnchanged()
    {
        var baseline = _calculator.Evaluate(
            [CandleAt(0, open: 105m, high: 110m, low: 90m, close: 100m)],
            StructuralTurnProtectionSide.Upper);
        var alteredBody = _calculator.Evaluate(
            [CandleAt(0, open: 95m, high: 110m, low: 80m, close: 105m)],
            StructuralTurnProtectionSide.Upper);

        Assert.Equal(baseline.ProtectionAnchor, alteredBody.ProtectionAnchor);
    }

    [Fact]
    public void Evaluate_ProtectionAnchorCanDifferFromBodyCoordinate()
    {
        var candles = new[]
        {
            CandleAt(0, open: 100m, high: 125m, low: 80m, close: 110m),
            CandleAt(1, open: 105m, high: 130m, low: 95m, close: 115m)
        };
        var bodyCoordinate = new StructuralTurnBodyCoordinateCalculator().Evaluate(
            candles,
            StructuralTurnBodyCoordinateSide.Lower);
        var protectionAnchor = _calculator.Evaluate(candles, StructuralTurnProtectionSide.Lower);

        Assert.Equal(100m, bodyCoordinate.StructuralPrice);
        Assert.Equal(80m, protectionAnchor.ProtectionAnchor);
        Assert.NotEqual(bodyCoordinate.StructuralPrice, protectionAnchor.ProtectionAnchor);
    }

    [Fact]
    public void Evaluate_TiedWickExtreme_DoesNotExposeCandleIdentityTieBreaking()
    {
        var result = _calculator.Evaluate(
            [
                CandleAt(0, open: 100m, high: 120m, low: 90m, close: 110m),
                CandleAt(1, open: 110m, high: 120m, low: 80m, close: 100m)
            ],
            StructuralTurnProtectionSide.Upper);

        Assert.Equal(120m, result.ProtectionAnchor);
        Assert.DoesNotContain(
            typeof(StructuralTurnProtectionAnchorResult).GetProperties(),
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

        var first = _calculator.Evaluate(candles, StructuralTurnProtectionSide.Lower);
        var second = _calculator.Evaluate(candles, StructuralTurnProtectionSide.Lower);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Evaluate_DoesNotApplyBufferOrOffset()
    {
        var result = _calculator.Evaluate(
            [CandleAt(0, open: 100m, high: 120m, low: 90m, close: 110m)],
            StructuralTurnProtectionSide.Lower);

        Assert.Equal(90m, result.ProtectionAnchor);
    }

    [Fact]
    public void Evaluate_NullEmptyOrUnsupportedSide_UsesInputValidationConventions()
    {
        Assert.Throws<ArgumentNullException>(() => _calculator.Evaluate(null!, StructuralTurnProtectionSide.Lower));
        Assert.Throws<ArgumentException>(() => _calculator.Evaluate([], StructuralTurnProtectionSide.Lower));
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Evaluate(
            [CandleAt(0, open: 100m, high: 110m, low: 90m, close: 105m)],
            (StructuralTurnProtectionSide)99));
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

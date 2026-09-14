using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.MarketData.Candles;

public sealed class StructuralTurnGeometryCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("provider");
    private static readonly MarketSymbol Symbol = new("NQ");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly StructuralTurnGeometryCalculator _calculator = new();

    [Fact]
    public void Evaluate_SingleBullishLowerTurn_ReturnsOpenAndLow()
    {
        var result = _calculator.Evaluate(
            [CandleAt(0, open: 100m, high: 120m, low: 90m, close: 110m)],
            StructuralTurnBodyCoordinateSide.Lower);

        Assert.Equal(StructuralTurnBodyCoordinateSide.Lower, result.Side);
        Assert.Equal(100m, result.StructuralPrice);
        Assert.Equal(90m, result.ProtectionAnchor);
    }

    [Fact]
    public void Evaluate_SingleBearishUpperTurn_ReturnsOpenAndHigh()
    {
        var result = _calculator.Evaluate(
            [CandleAt(0, open: 110m, high: 120m, low: 90m, close: 100m)],
            StructuralTurnBodyCoordinateSide.Upper);

        Assert.Equal(StructuralTurnBodyCoordinateSide.Upper, result.Side);
        Assert.Equal(110m, result.StructuralPrice);
        Assert.Equal(120m, result.ProtectionAnchor);
    }

    [Fact]
    public void Evaluate_MultipleLowerTurnCandles_ComputesBodyPriceAndWickAnchorIndependently()
    {
        var result = _calculator.Evaluate(
            [
                CandleAt(0, open: 110m, high: 120m, low: 90m, close: 100m),
                CandleAt(1, open: 99m, high: 130m, low: 80m, close: 120m)
            ],
            StructuralTurnBodyCoordinateSide.Lower);

        Assert.Equal(99m, result.StructuralPrice);
        Assert.Equal(80m, result.ProtectionAnchor);
    }

    [Fact]
    public void Evaluate_MultipleUpperTurnCandles_ComputesBodyPriceAndWickAnchorIndependently()
    {
        var result = _calculator.Evaluate(
            [
                CandleAt(0, open: 110m, high: 120m, low: 90m, close: 100m),
                CandleAt(1, open: 99m, high: 130m, low: 80m, close: 120m)
            ],
            StructuralTurnBodyCoordinateSide.Upper);

        Assert.Equal(120m, result.StructuralPrice);
        Assert.Equal(130m, result.ProtectionAnchor);
    }

    [Fact]
    public void Evaluate_MoreExtremeWick_ChangesProtectionAnchorWithoutChangingStructuralPrice()
    {
        var baseline = _calculator.Evaluate(
            [CandleAt(0, open: 100m, high: 110m, low: 90m, close: 105m)],
            StructuralTurnBodyCoordinateSide.Lower);
        var changedWick = _calculator.Evaluate(
            [CandleAt(0, open: 100m, high: 110m, low: 80m, close: 105m)],
            StructuralTurnBodyCoordinateSide.Lower);

        Assert.Equal(baseline.StructuralPrice, changedWick.StructuralPrice);
        Assert.NotEqual(baseline.ProtectionAnchor, changedWick.ProtectionAnchor);
    }

    [Fact]
    public void Evaluate_BodyChanges_ChangeStructuralPriceWithoutChangingProtectionAnchor()
    {
        var baseline = _calculator.Evaluate(
            [CandleAt(0, open: 100m, high: 120m, low: 90m, close: 105m)],
            StructuralTurnBodyCoordinateSide.Lower);
        var changedBody = _calculator.Evaluate(
            [CandleAt(0, open: 95m, high: 120m, low: 90m, close: 110m)],
            StructuralTurnBodyCoordinateSide.Lower);

        Assert.NotEqual(baseline.StructuralPrice, changedBody.StructuralPrice);
        Assert.Equal(baseline.ProtectionAnchor, changedBody.ProtectionAnchor);
    }

    [Fact]
    public void Evaluate_ExactTies_DoNotExposeCandleIdentity()
    {
        var result = _calculator.Evaluate(
            [
                CandleAt(0, open: 100m, high: 120m, low: 90m, close: 110m),
                CandleAt(1, open: 110m, high: 120m, low: 80m, close: 100m)
            ],
            StructuralTurnBodyCoordinateSide.Upper);

        Assert.Equal(110m, result.StructuralPrice);
        Assert.Equal(120m, result.ProtectionAnchor);
        Assert.DoesNotContain(
            typeof(StructuralTurnGeometryResult).GetProperties(),
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

    [Fact]
    public void Result_DoesNotExposeExecutableStopLossOrStrategyOutcomes()
    {
        var propertyNames = typeof(StructuralTurnGeometryResult)
            .GetProperties()
            .Select(property => property.Name);

        Assert.DoesNotContain("SlPrice", propertyNames);
        Assert.DoesNotContain("Offset", propertyNames);
        Assert.DoesNotContain("StrategyVerdict", propertyNames);
        Assert.DoesNotContain("ValidationStatus", propertyNames);
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

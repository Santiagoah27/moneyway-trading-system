using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.MarketData.Candles;

public sealed class CorrectionOriginExtremeCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private readonly CorrectionOriginExtremeCalculator calculator = new();

    [Fact]
    public void LowerLowUpdatesFloor()
    {
        var result = calculator.Evaluate(100m, Candle(95m, 110m, 90m, 105m), CorrectionOriginExtremeSide.Floor);

        Assert.Equal((100m, 90m, CorrectionOriginExtremeSide.Floor, true),
            (result.PreviousExtreme, result.ResultingExtreme, result.Side, result.WasUpdated));
    }

    [Fact]
    public void EqualLowPreservesFloor()
    {
        var result = calculator.Evaluate(100m, Candle(100m, 110m, 100m, 105m), CorrectionOriginExtremeSide.Floor);

        Assert.Equal(100m, result.ResultingExtreme);
        Assert.False(result.WasUpdated);
    }

    [Fact]
    public void HigherLowPreservesFloor()
    {
        var result = calculator.Evaluate(100m, Candle(105m, 110m, 101m, 108m), CorrectionOriginExtremeSide.Floor);

        Assert.Equal(100m, result.ResultingExtreme);
        Assert.False(result.WasUpdated);
    }

    [Fact]
    public void HigherHighUpdatesCeiling()
    {
        var result = calculator.Evaluate(100m, Candle(95m, 110m, 90m, 105m), CorrectionOriginExtremeSide.Ceiling);

        Assert.Equal((100m, 110m, CorrectionOriginExtremeSide.Ceiling, true),
            (result.PreviousExtreme, result.ResultingExtreme, result.Side, result.WasUpdated));
    }

    [Fact]
    public void EqualHighPreservesCeiling()
    {
        var result = calculator.Evaluate(100m, Candle(95m, 100m, 90m, 98m), CorrectionOriginExtremeSide.Ceiling);

        Assert.Equal(100m, result.ResultingExtreme);
        Assert.False(result.WasUpdated);
    }

    [Fact]
    public void LowerHighPreservesCeiling()
    {
        var result = calculator.Evaluate(100m, Candle(95m, 99m, 90m, 98m), CorrectionOriginExtremeSide.Ceiling);

        Assert.Equal(100m, result.ResultingExtreme);
        Assert.False(result.WasUpdated);
    }

    [Fact]
    public void OpenAndCloseDoNotChangeFloorUpdateWhenLowIsUnchanged()
    {
        var first = calculator.Evaluate(100m, Candle(95m, 110m, 90m, 105m), CorrectionOriginExtremeSide.Floor);
        var second = calculator.Evaluate(100m, Candle(109m, 110m, 90m, 91m), CorrectionOriginExtremeSide.Floor);

        Assert.Equal(first, second);
    }

    [Fact]
    public void OpenAndCloseDoNotChangeCeilingUpdateWhenHighIsUnchanged()
    {
        var first = calculator.Evaluate(100m, Candle(95m, 110m, 90m, 105m), CorrectionOriginExtremeSide.Ceiling);
        var second = calculator.Evaluate(100m, Candle(109m, 110m, 90m, 91m), CorrectionOriginExtremeSide.Ceiling);

        Assert.Equal(first, second);
    }

    [Fact]
    public void RepeatedEvaluationIsDeterministic()
    {
        var candle = Candle(95m, 110m, 90m, 105m);

        Assert.Equal(
            calculator.Evaluate(100m, candle, CorrectionOriginExtremeSide.Floor),
            calculator.Evaluate(100m, candle, CorrectionOriginExtremeSide.Floor));
    }

    [Fact]
    public void ResultDoesNotClaimStructuralValidation()
    {
        var result = calculator.Evaluate(100m, Candle(95m, 110m, 90m, 105m), CorrectionOriginExtremeSide.Floor);

        Assert.DoesNotContain(
            typeof(CorrectionOriginExtremeResult).GetProperties(),
            property => property.Name.Contains("Structural", StringComparison.Ordinal));
        Assert.Equal(90m, result.ResultingExtreme);
    }

    [Fact]
    public void RejectsNullCandleAndUnknownSide()
    {
        var candle = Candle(95m, 110m, 90m, 105m);

        Assert.Throws<ArgumentNullException>(() => calculator.Evaluate(100m, null!, CorrectionOriginExtremeSide.Floor));
        Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Evaluate(100m, candle, (CorrectionOriginExtremeSide)99));
    }

    private static Candle Candle(decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, Minute, Start, Start.AddMinutes(1), open, high, low, close, null);
}

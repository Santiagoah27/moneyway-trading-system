using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.MarketData.Candles;

public sealed class CandleBodyDirectionCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private readonly CandleBodyDirectionCalculator calculator = new();

    [Fact]
    public void CloseStrictlyAboveOpenIsBullish()
    {
        var result = calculator.Evaluate(Candle(100m, 101m, 99m, 100.0000001m));

        Assert.Equal(CandleBodyDirection.Bullish, result);
    }

    [Fact]
    public void CloseStrictlyBelowOpenIsBearish()
    {
        var result = calculator.Evaluate(Candle(100m, 101m, 99m, 99.9999999m));

        Assert.Equal(CandleBodyDirection.Bearish, result);
    }

    [Fact]
    public void ExactOpenCloseEqualityIsNeutral()
    {
        var result = calculator.Evaluate(Candle(100m, 101m, 99m, 100m));

        Assert.Equal(CandleBodyDirection.Neutral, result);
    }

    [Fact]
    public void MinimumPositiveDecimalDifferenceIsBullishNotNeutral()
    {
        var result = calculator.Evaluate(Candle(100m, 101m, 99m, 100.0000001m));

        Assert.NotEqual(CandleBodyDirection.Neutral, result);
        Assert.Equal(CandleBodyDirection.Bullish, result);
    }

    [Fact]
    public void MinimumNegativeDecimalDifferenceIsBearishNotNeutral()
    {
        var result = calculator.Evaluate(Candle(100m, 101m, 99m, 99.9999999m));

        Assert.NotEqual(CandleBodyDirection.Neutral, result);
        Assert.Equal(CandleBodyDirection.Bearish, result);
    }

    [Fact]
    public void RepeatedEvaluationIsDeterministic()
    {
        var candle = Candle(100m, 150m, 50m, 100.0000001m);

        Assert.Equal(calculator.Evaluate(candle), calculator.Evaluate(candle));
    }

    [Fact]
    public void HighAndLowDoNotChangeBodyDirection()
    {
        var narrowWick = Candle(100m, 100.0000001m, 100m, 100.0000001m);
        var wideWick = Candle(100m, 1000m, 1m, 100.0000001m);

        Assert.Equal(CandleBodyDirection.Bullish, calculator.Evaluate(narrowWick));
        Assert.Equal(CandleBodyDirection.Bullish, calculator.Evaluate(wideWick));
    }

    [Fact]
    public void WickGeometryCannotConvertNeutralBodyIntoDirectionalBody()
    {
        var candle = Candle(100m, 1000m, 1m, 100m);

        Assert.Equal(CandleBodyDirection.Neutral, calculator.Evaluate(candle));
    }

    [Fact]
    public void RejectsNullCandle()
    {
        Assert.Throws<ArgumentNullException>(() => calculator.Evaluate(null!));
    }

    private static Candle Candle(decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, Minute, Start, Start.AddMinutes(1), open, high, low, close, null);
}

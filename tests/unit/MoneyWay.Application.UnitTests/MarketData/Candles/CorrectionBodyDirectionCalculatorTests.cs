using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.MarketData.Candles;

public sealed class CorrectionBodyDirectionCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("provider");
    private static readonly MarketSymbol Symbol = new("NQ");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly CorrectionBodyDirectionCalculator _calculator = new();

    [Fact]
    public void Evaluate_FloorWithBullishDirection_ReturnsTrue()
    {
        Assert.True(_calculator.Evaluate(CorrectionOriginExtremeSide.Floor, CandleBodyDirection.Bullish));
    }

    [Fact]
    public void Evaluate_FloorWithBearishDirection_ReturnsFalse()
    {
        Assert.False(_calculator.Evaluate(CorrectionOriginExtremeSide.Floor, CandleBodyDirection.Bearish));
    }

    [Fact]
    public void Evaluate_FloorWithNeutralDirection_ReturnsFalse()
    {
        Assert.False(_calculator.Evaluate(CorrectionOriginExtremeSide.Floor, CandleBodyDirection.Neutral));
    }

    [Fact]
    public void Evaluate_CeilingWithBearishDirection_ReturnsTrue()
    {
        Assert.True(_calculator.Evaluate(CorrectionOriginExtremeSide.Ceiling, CandleBodyDirection.Bearish));
    }

    [Fact]
    public void Evaluate_CeilingWithBullishDirection_ReturnsFalse()
    {
        Assert.False(_calculator.Evaluate(CorrectionOriginExtremeSide.Ceiling, CandleBodyDirection.Bullish));
    }

    [Fact]
    public void Evaluate_CeilingWithNeutralDirection_ReturnsFalse()
    {
        Assert.False(_calculator.Evaluate(CorrectionOriginExtremeSide.Ceiling, CandleBodyDirection.Neutral));
    }

    [Fact]
    public void Evaluate_RepeatedEvaluation_ReturnsIdenticalResult()
    {
        var first = _calculator.Evaluate(CorrectionOriginExtremeSide.Floor, CandleBodyDirection.Bullish);
        var second = _calculator.Evaluate(CorrectionOriginExtremeSide.Floor, CandleBodyDirection.Bullish);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Evaluate_FloorUpdatedByLowerLowWithBullishBody_ReturnsTrue()
    {
        var observation = new CorrectionBoundaryObservationCalculator().Evaluate(
            100m,
            Candle(open: 100m, high: 120m, low: 90m, close: 110m),
            CorrectionOriginExtremeSide.Floor);

        Assert.True(observation.WasExtremeUpdated);
        Assert.True(_calculator.Evaluate(observation.Side, observation.BodyDirection));
    }

    [Fact]
    public void Evaluate_CeilingUpdatedByHigherHighWithBearishBody_ReturnsTrue()
    {
        var observation = new CorrectionBoundaryObservationCalculator().Evaluate(
            100m,
            Candle(open: 110m, high: 120m, low: 90m, close: 100m),
            CorrectionOriginExtremeSide.Ceiling);

        Assert.True(observation.WasExtremeUpdated);
        Assert.True(_calculator.Evaluate(observation.Side, observation.BodyDirection));
    }

    [Fact]
    public void Evaluate_UpdatedExtremeWithNeutralBody_ReturnsFalse()
    {
        var observation = new CorrectionBoundaryObservationCalculator().Evaluate(
            100m,
            Candle(open: 100m, high: 120m, low: 90m, close: 100m),
            CorrectionOriginExtremeSide.Floor);

        Assert.True(observation.WasExtremeUpdated);
        Assert.False(_calculator.Evaluate(observation.Side, observation.BodyDirection));
    }

    [Fact]
    public void Evaluate_UnsupportedEnums_UsesInputValidationConventions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Evaluate(
            (CorrectionOriginExtremeSide)99,
            CandleBodyDirection.Bullish));
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Evaluate(
            CorrectionOriginExtremeSide.Floor,
            (CandleBodyDirection)99));
    }

    private static Candle Candle(decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, Minute, Start, Start.AddMinutes(1), open, high, low, close, null);
}

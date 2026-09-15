using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.MarketData.Candles;

public sealed class BearishCorrectionTerminalObservationCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private readonly BearishCorrectionTerminalObservationCalculator calculator = new();

    [Fact]
    public void LowerLowReportsResetAndUpdatesFloor()
    {
        var result = Evaluate(Candle(90, 99, 79, 85), floor: 80, priorLh: 105);

        Assert.True(result.WasNewLowResetObserved);
        Assert.Equal((80m, 79m), (result.PreviousFloor, result.ResultingFloor));
    }

    [Theory]
    [InlineData(80)]
    [InlineData(81)]
    public void EqualOrHigherLowDoesNotReportReset(decimal low)
    {
        var result = Evaluate(Candle(90, 99, low, 85), floor: 80, priorLh: 105);

        Assert.False(result.WasNewLowResetObserved);
        Assert.Equal(80m, result.ResultingFloor);
    }

    [Fact]
    public void StrictCloseAbovePriorLhReportsInvalidation()
    {
        var result = Evaluate(Candle(100, 111, 90, 106), floor: 80, priorLh: 105);

        Assert.True(result.WasPriorLhInvalidationObserved);
    }

    [Fact]
    public void CloseEqualToPriorLhDoesNotReportInvalidation()
    {
        var result = Evaluate(Candle(100, 111, 90, 105), floor: 80, priorLh: 105);

        Assert.False(result.WasPriorLhInvalidationObserved);
    }

    [Fact]
    public void WickOnlyPenetrationDoesNotReportInvalidation()
    {
        var result = Evaluate(Candle(100, 111, 90, 104), floor: 80, priorLh: 105);

        Assert.True(result.Candle!.High > 105m);
        Assert.True(result.Candle.Close <= 105m);
        Assert.False(result.WasPriorLhInvalidationObserved);
    }

    [Theory]
    [InlineData(90, 95, CandleBodyDirection.Bullish)]
    [InlineData(90, 90, CandleBodyDirection.Neutral)]
    [InlineData(95, 90, CandleBodyDirection.Bearish)]
    public void ResetReportsExactBodyDirection(decimal open, decimal close, CandleBodyDirection expected)
    {
        var result = Evaluate(Candle(open, 99, 79, close), floor: 80, priorLh: 110);

        Assert.True(result.WasNewLowResetObserved);
        Assert.Equal(expected, result.BodyDirection);
    }

    [Fact]
    public void SameCandleCanReportResetAndInvalidationWithoutPrecedence()
    {
        var result = Evaluate(Candle(95, 111, 79, 106), floor: 80, priorLh: 105);

        Assert.True(result.WasNewLowResetObserved);
        Assert.True(result.WasPriorLhInvalidationObserved);
        var properties = typeof(BearishCorrectionTerminalObservationResult)
            .GetProperties()
            .Select(property => property.Name);
        Assert.DoesNotContain("Precedence", properties);
        Assert.DoesNotContain("Transition", properties);
        Assert.DoesNotContain("CorrectionStarted", properties);
        Assert.DoesNotContain("ValidatedStructuralLl", properties);
        Assert.DoesNotContain("FinalState", properties);
    }

    [Fact]
    public void ResultingFloorIsOnlyAPriceExtreme()
    {
        var result = Evaluate(Candle(90, 99, 79, 85), floor: 80, priorLh: 105);
        var properties = typeof(BearishCorrectionTerminalObservationResult)
            .GetProperties()
            .Select(property => property.Name);

        Assert.Equal(79m, result.ResultingFloor);
        Assert.DoesNotContain("ValidatedLl", properties);
        Assert.DoesNotContain("StructuralLow", properties);
        Assert.DoesNotContain("StopLoss", properties);
    }

    [Fact]
    public void ResultPreservesReplayBoundaryIdentity()
    {
        var context = ContextAt(Start.AddMinutes(1), Series(Candle(90, 99, 79, 85)));

        var result = calculator.EvaluateCurrentBoundary(context, Minute, 80, 105);

        Assert.Equal((Provider, Symbol, Minute, context.AsOfUtc),
            (result.ProviderId, result.Symbol, result.Timeframe, result.AsOfUtc));
        Assert.Same(CurrentCandle(context), result.Candle);
    }

    [Fact]
    public void RepeatedEvaluationIsDeterministic()
    {
        var context = ContextAt(Start.AddMinutes(1), Series(Candle(95, 111, 79, 106)));

        var first = calculator.EvaluateCurrentBoundary(context, Minute, 80, 105);
        var second = calculator.EvaluateCurrentBoundary(context, Minute, 80, 105);

        Assert.Equal(first, second);
    }

    [Fact]
    public void FutureCandleCannotChangeEarlierBoundaryResult()
    {
        var current = Candle(90, 99, 81, 85);
        var future = Candle(Start.AddMinutes(1), Start.AddMinutes(2), 95, 111, 70, 106);
        var before = ContextAt(Start.AddMinutes(1), Series(current));
        var withFuture = ContextAt(Start.AddMinutes(1), Series(current, future));

        var expected = calculator.EvaluateCurrentBoundary(before, Minute, 80, 105);
        var actual = calculator.EvaluateCurrentBoundary(withFuture, Minute, 80, 105);

        Assert.Equal(expected, actual);
        Assert.False(actual.WasNewLowResetObserved);
        Assert.False(actual.WasPriorLhInvalidationObserved);
    }

    [Fact]
    public void NotUpdatedTimeframeReportsNoCurrentBoundaryFacts()
    {
        var fourHours = new Timeframe(4, TimeframeUnit.Hour);
        var minute = Series(Candle(90, 99, 81, 85));
        var fourHour = new CandleSeries(Provider, Symbol, fourHours,
            [new Candle(Provider, Symbol, fourHours, Start, Start.AddHours(4), 100, 130, 70, 85, null)]);
        var context = ContextAt(Start.AddMinutes(1), minute, fourHour);

        var result = calculator.EvaluateCurrentBoundary(context, fourHours, 80, 105);

        Assert.Null(result.Candle);
        Assert.Null(result.BodyDirection);
        Assert.False(result.WasNewLowResetObserved);
        Assert.False(result.WasPriorLhInvalidationObserved);
        Assert.Equal(80m, result.ResultingFloor);
    }

    [Fact]
    public void NullInputsFollowRepositoryValidationConventions()
    {
        var context = ContextAt(Start.AddMinutes(1), Series(Candle(90, 99, 81, 85)));

        Assert.Throws<ArgumentNullException>(() => calculator.EvaluateCurrentBoundary(null!, Minute, 80, 105));
        Assert.Throws<ArgumentNullException>(() => calculator.EvaluateCurrentBoundary(context, null!, 80, 105));
    }

    private BearishCorrectionTerminalObservationResult Evaluate(Candle candle, decimal floor, decimal priorLh)
    {
        var context = ContextAt(candle.CloseTimeUtc, Series(candle));
        return calculator.EvaluateCurrentBoundary(context, candle.Timeframe, floor, priorLh);
    }

    private static StrategyReplayContext ContextAt(DateTimeOffset asOfUtc, params CandleSeries[] series)
    {
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        var factory = new CreateStrategyReplayContextUseCase();
        while (cursor.TryAdvance(out var frame))
        {
            if (frame!.AsOfUtc == asOfUtc) return factory.Execute(Definition, frame);
        }

        throw new InvalidOperationException("Requested replay boundary was not produced.");
    }

    private static Candle CurrentCandle(StrategyReplayContext context)
    {
        Assert.True(context.TryGetFrame(Minute, out var frame));
        return frame!.CurrentCandle;
    }

    private static CandleSeries Series(params Candle[] candles) => new(Provider, Symbol, candles[0].Timeframe, candles);

    private static Candle Candle(decimal open, decimal high, decimal low, decimal close) =>
        Candle(Start, Start.AddMinutes(1), open, high, low, close);

    private static Candle Candle(
        DateTimeOffset openTime,
        DateTimeOffset closeTime,
        decimal open,
        decimal high,
        decimal low,
        decimal close) =>
        new(Provider, Symbol, Minute, openTime, closeTime, open, high, low, close, null);

    private static readonly StrategyDefinition Definition = new(
        new("synthetic"),
        new("v1"),
        "Synthetic",
        "test",
        [new(new("R-1"), "Rule", "stage", 10, true, RuleDefinitionStatus.Confirmed, "description", "source")]);
}

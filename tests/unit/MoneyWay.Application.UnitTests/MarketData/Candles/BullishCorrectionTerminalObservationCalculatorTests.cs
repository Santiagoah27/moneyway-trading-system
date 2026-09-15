using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.MarketData.Candles;

public sealed class BullishCorrectionTerminalObservationCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private readonly BullishCorrectionTerminalObservationCalculator calculator = new();

    [Fact]
    public void HigherHighReportsResetAndUpdatesCeiling()
    {
        var result = Evaluate(Candle(110, 121, 100, 115), ceiling: 120, priorHl: 90);

        Assert.True(result.WasNewHighResetObserved);
        Assert.Equal((120m, 121m), (result.PreviousCeiling, result.ResultingCeiling));
    }

    [Theory]
    [InlineData(120)]
    [InlineData(119)]
    public void EqualOrLowerHighDoesNotReportReset(decimal high)
    {
        var result = Evaluate(Candle(110, high, 100, 115), ceiling: 120, priorHl: 90);

        Assert.False(result.WasNewHighResetObserved);
        Assert.Equal(120m, result.ResultingCeiling);
    }

    [Fact]
    public void StrictCloseBelowPriorHlReportsInvalidation()
    {
        var result = Evaluate(Candle(100, 110, 80, 89), ceiling: 120, priorHl: 90);

        Assert.True(result.WasPriorHlInvalidationObserved);
    }

    [Fact]
    public void CloseEqualToPriorHlDoesNotReportInvalidation()
    {
        var result = Evaluate(Candle(100, 110, 80, 90), ceiling: 120, priorHl: 90);

        Assert.False(result.WasPriorHlInvalidationObserved);
    }

    [Fact]
    public void WickOnlyPenetrationDoesNotReportInvalidation()
    {
        var result = Evaluate(Candle(100, 110, 80, 95), ceiling: 120, priorHl: 90);

        Assert.True(result.Candle!.Low < 90m);
        Assert.True(result.Candle.Close >= 90m);
        Assert.False(result.WasPriorHlInvalidationObserved);
    }

    [Theory]
    [InlineData(110, 100, CandleBodyDirection.Bearish)]
    [InlineData(100, 100, CandleBodyDirection.Neutral)]
    [InlineData(100, 110, CandleBodyDirection.Bullish)]
    public void ResetReportsExactBodyDirection(decimal open, decimal close, CandleBodyDirection expected)
    {
        var result = Evaluate(Candle(open, 121, 90, close), ceiling: 120, priorHl: 80);

        Assert.True(result.WasNewHighResetObserved);
        Assert.Equal(expected, result.BodyDirection);
    }

    [Fact]
    public void SameCandleCanReportResetAndInvalidationWithoutPrecedence()
    {
        var result = Evaluate(Candle(110, 121, 80, 89), ceiling: 120, priorHl: 90);

        Assert.True(result.WasNewHighResetObserved);
        Assert.True(result.WasPriorHlInvalidationObserved);
        var properties = typeof(BullishCorrectionTerminalObservationResult)
            .GetProperties()
            .Select(property => property.Name);
        Assert.DoesNotContain("Precedence", properties);
        Assert.DoesNotContain("Transition", properties);
        Assert.DoesNotContain("CorrectionStarted", properties);
        Assert.DoesNotContain("ValidatedStructuralHh", properties);
        Assert.DoesNotContain("FinalState", properties);
    }

    [Fact]
    public void ResultPreservesReplayBoundaryIdentity()
    {
        var context = ContextAt(Start.AddMinutes(1), Series(Candle(110, 121, 100, 115)));

        var result = calculator.EvaluateCurrentBoundary(context, Minute, 120, 90);

        Assert.Equal((Provider, Symbol, Minute, context.AsOfUtc),
            (result.ProviderId, result.Symbol, result.Timeframe, result.AsOfUtc));
        Assert.Same(CurrentCandle(context), result.Candle);
    }

    [Fact]
    public void RepeatedEvaluationIsDeterministic()
    {
        var context = ContextAt(Start.AddMinutes(1), Series(Candle(110, 121, 80, 89)));

        var first = calculator.EvaluateCurrentBoundary(context, Minute, 120, 90);
        var second = calculator.EvaluateCurrentBoundary(context, Minute, 120, 90);

        Assert.Equal(first, second);
    }

    [Fact]
    public void FutureCandleCannotChangeEarlierBoundaryResult()
    {
        var current = Candle(100, 110, 90, 105);
        var future = Candle(Start.AddMinutes(1), Start.AddMinutes(2), 110, 130, 80, 85);
        var before = ContextAt(Start.AddMinutes(1), Series(current));
        var withFuture = ContextAt(Start.AddMinutes(1), Series(current, future));

        var expected = calculator.EvaluateCurrentBoundary(before, Minute, 120, 90);
        var actual = calculator.EvaluateCurrentBoundary(withFuture, Minute, 120, 90);

        Assert.Equal(expected, actual);
        Assert.False(actual.WasNewHighResetObserved);
        Assert.False(actual.WasPriorHlInvalidationObserved);
    }

    [Fact]
    public void NotUpdatedTimeframeReportsNoCurrentBoundaryFacts()
    {
        var fourHours = new Timeframe(4, TimeframeUnit.Hour);
        var minute = Series(Candle(100, 110, 90, 105));
        var fourHour = new CandleSeries(Provider, Symbol, fourHours,
            [new Candle(Provider, Symbol, fourHours, Start, Start.AddHours(4), 100, 130, 80, 85, null)]);
        var context = ContextAt(Start.AddMinutes(1), minute, fourHour);

        var result = calculator.EvaluateCurrentBoundary(context, fourHours, 120, 90);

        Assert.Null(result.Candle);
        Assert.Null(result.BodyDirection);
        Assert.False(result.WasNewHighResetObserved);
        Assert.False(result.WasPriorHlInvalidationObserved);
        Assert.Equal(120m, result.ResultingCeiling);
    }

    [Fact]
    public void NullInputsFollowRepositoryValidationConventions()
    {
        var context = ContextAt(Start.AddMinutes(1), Series(Candle(100, 110, 90, 105)));

        Assert.Throws<ArgumentNullException>(() => calculator.EvaluateCurrentBoundary(null!, Minute, 120, 90));
        Assert.Throws<ArgumentNullException>(() => calculator.EvaluateCurrentBoundary(context, null!, 120, 90));
    }

    private BullishCorrectionTerminalObservationResult Evaluate(
        Candle candle,
        decimal ceiling,
        decimal priorHl)
    {
        var context = ContextAt(candle.CloseTimeUtc, Series(candle));
        return calculator.EvaluateCurrentBoundary(context, candle.Timeframe, ceiling, priorHl);
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

using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.MarketData.StructuralBreaks;

public sealed class StructuralBodyCloseBreakCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly Timeframe FourHours = new(4, TimeframeUnit.Hour);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private readonly StructuralBodyCloseBreakCalculator calculator = new();

    [Fact]
    public void UpperCloseStrictlyAboveReferenceConfirmsBreak()
    {
        var context = ContextAt(Start.AddHours(4), Series(Candle(FourHours, Start, Start.AddHours(4), 99, 102, 98, 101)));

        var result = calculator.EvaluateCurrentBoundary(context, FourHours, 100, StructuralBreakDirection.Upper);

        Assert.True(result.IsConfirmed);
        Assert.Same(CurrentCandle(context, FourHours), result.Candle);
        Assert.Equal((Provider, Symbol, FourHours, 100m, StructuralBreakDirection.Upper, context.AsOfUtc),
            (result.ProviderId, result.Symbol, result.Timeframe, result.ReferenceLevel, result.Direction, result.AsOfUtc));
    }

    [Fact]
    public void UpperEqualityDoesNotConfirmBreak()
    {
        var context = ContextAt(Start.AddHours(4), Series(Candle(FourHours, Start, Start.AddHours(4), 99, 101, 98, 100)));

        var result = calculator.EvaluateCurrentBoundary(context, FourHours, 100, StructuralBreakDirection.Upper);

        Assert.False(result.IsConfirmed);
        Assert.Equal(100, result.Candle!.Close);
    }

    [Fact]
    public void UpperWickOnlyPenetrationDoesNotConfirmBreak()
    {
        var context = ContextAt(Start.AddHours(4), Series(Candle(FourHours, Start, Start.AddHours(4), 99, 101, 98, 100)));

        var result = calculator.EvaluateCurrentBoundary(context, FourHours, 100, StructuralBreakDirection.Upper);

        Assert.True(result.Candle!.High > result.ReferenceLevel);
        Assert.True(result.Candle.Close <= result.ReferenceLevel);
        Assert.False(result.IsConfirmed);
    }

    [Fact]
    public void LowerCloseStrictlyBelowReferenceConfirmsBreak()
    {
        var context = ContextAt(Start.AddHours(4), Series(Candle(FourHours, Start, Start.AddHours(4), 101, 102, 98, 99)));

        var result = calculator.EvaluateCurrentBoundary(context, FourHours, 100, StructuralBreakDirection.Lower);

        Assert.True(result.IsConfirmed);
        Assert.Equal(99, result.Candle!.Close);
    }

    [Fact]
    public void LowerEqualityDoesNotConfirmBreak()
    {
        var context = ContextAt(Start.AddHours(4), Series(Candle(FourHours, Start, Start.AddHours(4), 101, 102, 99, 100)));

        var result = calculator.EvaluateCurrentBoundary(context, FourHours, 100, StructuralBreakDirection.Lower);

        Assert.False(result.IsConfirmed);
        Assert.Equal(100, result.Candle!.Close);
    }

    [Fact]
    public void LowerWickOnlyPenetrationDoesNotConfirmBreak()
    {
        var context = ContextAt(Start.AddHours(4), Series(Candle(FourHours, Start, Start.AddHours(4), 101, 102, 99, 100)));

        var result = calculator.EvaluateCurrentBoundary(context, FourHours, 100, StructuralBreakDirection.Lower);

        Assert.True(result.Candle!.Low < result.ReferenceLevel);
        Assert.True(result.Candle.Close >= result.ReferenceLevel);
        Assert.False(result.IsConfirmed);
    }

    [Fact]
    public void NotYetObservableCandleCannotConfirmBreak()
    {
        var context = ContextAt(
            Start.AddMinutes(1),
            Series(Candle(Minute, Start, Start.AddMinutes(1), 99, 101, 98, 99)),
            Series(Candle(FourHours, Start, Start.AddHours(4), 99, 102, 98, 101)));

        var result = calculator.EvaluateCurrentBoundary(context, FourHours, 100, StructuralBreakDirection.Upper);

        Assert.True(context.AsOfUtc < Start.AddHours(4));
        Assert.False(context.WasUpdated(FourHours));
        Assert.False(result.IsConfirmed);
        Assert.Null(result.Candle);
    }

    [Fact]
    public void ConfirmationBecomesAvailableAtCausalClosedCandleBoundary()
    {
        var series = new[]
        {
            Series(Candle(Minute, Start, Start.AddMinutes(1), 99, 101, 98, 99)),
            Series(Candle(FourHours, Start, Start.AddHours(4), 99, 102, 98, 101)),
        };
        var before = ContextAt(Start.AddMinutes(1), series);
        var atClose = ContextAt(Start.AddHours(4), series);

        var beforeResult = calculator.EvaluateCurrentBoundary(before, FourHours, 100, StructuralBreakDirection.Upper);
        var atCloseResult = calculator.EvaluateCurrentBoundary(atClose, FourHours, 100, StructuralBreakDirection.Upper);

        Assert.False(beforeResult.IsConfirmed);
        Assert.True(atCloseResult.IsConfirmed);
        Assert.Equal(Start.AddHours(4), atCloseResult.Candle!.CloseTimeUtc);
        Assert.Equal(atClose.AsOfUtc, atCloseResult.AsOfUtc);
    }

    [Fact]
    public void FutureCandleCannotChangeEarlierBoundaryResult()
    {
        var first = Candle(Minute, Start, Start.AddMinutes(1), 99, 100, 98, 99);
        var future = Candle(Minute, Start.AddMinutes(1), Start.AddMinutes(2), 99, 102, 98, 101);
        var before = ContextAt(Start.AddMinutes(1), Series(first));
        var withFuture = ContextAt(Start.AddMinutes(1), Series(first, future));

        var expected = calculator.EvaluateCurrentBoundary(before, Minute, 100, StructuralBreakDirection.Upper);
        var actual = calculator.EvaluateCurrentBoundary(withFuture, Minute, 100, StructuralBreakDirection.Upper);

        Assert.False(actual.IsConfirmed);
        Assert.Equal(expected, actual);
        Assert.DoesNotContain(future, CurrentFrame(withFuture, Minute).AvailableCandles);
    }

    [Fact]
    public void RepeatedEvaluationIsDeterministic()
    {
        var context = ContextAt(Start.AddHours(4), Series(Candle(FourHours, Start, Start.AddHours(4), 99, 102, 98, 101)));

        var first = calculator.EvaluateCurrentBoundary(context, FourHours, 100, StructuralBreakDirection.Upper);
        var second = calculator.EvaluateCurrentBoundary(context, FourHours, 100, StructuralBreakDirection.Upper);

        Assert.Equal(first, second);
    }

    [Fact]
    public void RejectsNullInputsAndUnknownDirection()
    {
        var context = ContextAt(Start.AddHours(4), Series(Candle(FourHours, Start, Start.AddHours(4), 99, 102, 98, 101)));

        Assert.Throws<ArgumentNullException>(() => calculator.EvaluateCurrentBoundary(null!, FourHours, 100, StructuralBreakDirection.Upper));
        Assert.Throws<ArgumentNullException>(() => calculator.EvaluateCurrentBoundary(context, null!, 100, StructuralBreakDirection.Upper));
        Assert.Throws<ArgumentOutOfRangeException>(() => calculator.EvaluateCurrentBoundary(context, FourHours, 100, (StructuralBreakDirection)99));
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

    private static ReplayFrame CurrentFrame(StrategyReplayContext context, Timeframe timeframe)
    {
        Assert.True(context.TryGetFrame(timeframe, out var frame));
        return frame!;
    }

    private static Candle CurrentCandle(StrategyReplayContext context, Timeframe timeframe) =>
        CurrentFrame(context, timeframe).CurrentCandle;

    private static CandleSeries Series(params Candle[] candles) => new(Provider, Symbol, candles[0].Timeframe, candles);

    private static Candle Candle(
        Timeframe timeframe,
        DateTimeOffset open,
        DateTimeOffset close,
        decimal openPrice,
        decimal high,
        decimal low,
        decimal closePrice) =>
        new(Provider, Symbol, timeframe, open, close, openPrice, high, low, closePrice, null);

    private static readonly StrategyDefinition Definition = new(
        new("synthetic"),
        new("v1"),
        "Synthetic",
        "test",
        [new(new("R-1"), "Rule", "stage", 10, true, RuleDefinitionStatus.Confirmed, "description", "source")]);
}

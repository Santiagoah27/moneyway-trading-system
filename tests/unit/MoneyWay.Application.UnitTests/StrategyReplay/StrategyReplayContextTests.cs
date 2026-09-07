using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.StrategyReplay;

public sealed class StrategyReplayContextTests
{
    private static readonly MarketDataProviderId Provider = new("fixture"); private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute); private static readonly Timeframe Five = new(5, TimeframeUnit.Minute); private static readonly Timeframe Hour = new(1, TimeframeUnit.Hour);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private readonly CreateStrategyReplayContextUseCase useCase = new();

    [Fact]
    public void NullInputsAreRejected()
    {
        var frame = Frames(Series(Minute, Candle(Minute, 0, 1))).Single();
        Assert.Throws<ArgumentNullException>(() => useCase.Execute(null!, frame)); Assert.Throws<ArgumentNullException>(() => useCase.Execute(Definition(), null!));
    }

    [Fact]
    public void IdentityAndEarlyTimeframeStatesAreProjectedExactly()
    {
        var minute = Series(Minute, Candle(Minute, 0, 1)); var later = Series(Five, Candle(Five, 60, 65)); var empty = Series(Hour);
        var frame = Frames(minute, later, empty)[0]; var context = useCase.Execute(Definition(), frame);
        Assert.Equal(new StrategyId("test"), context.StrategyId); Assert.Equal(new StrategyVersion("v1"), context.StrategyVersion);
        Assert.Equal(Provider, context.ProviderId); Assert.Equal(Symbol, context.Symbol); Assert.Equal(frame.Step, context.Step); Assert.Equal(frame.AsOfUtc, context.AsOfUtc);
        Assert.Equal(frame.ConfiguredTimeframes, context.ConfiguredTimeframes); Assert.Equal([Minute], context.AvailableTimeframes); Assert.Equal([Minute], context.UpdatedTimeframes);
        Assert.True(context.IsConfigured(Five)); Assert.False(context.IsAvailable(Five)); Assert.False(context.WasUpdated(Five)); Assert.False(context.TryGetFrame(Five, out var unavailable)); Assert.Null(unavailable);
        var day = new Timeframe(1, TimeframeUnit.Day); Assert.False(context.IsConfigured(day)); Assert.False(context.IsAvailable(day)); Assert.False(context.WasUpdated(day)); Assert.False(context.TryGetFrame(day, out _));
    }

    [Fact]
    public void SimultaneousAndRetainedFramesAreQueriedWithoutFallback()
    {
        var minute = Series(Minute, Candle(Minute, 0, 5), Candle(Minute, 5, 6)); var five = Series(Five, Candle(Five, 0, 5));
        var frames = Frames(minute, five); var atomic = useCase.Execute(Definition(), frames[0]);
        Assert.True(atomic.WasUpdated(Minute)); Assert.True(atomic.WasUpdated(Five)); Assert.True(atomic.TryGetFrame(Minute, out var minuteFrame)); Assert.True(atomic.TryGetFrame(Five, out var fiveFrame));
        Assert.Equal(atomic.AsOfUtc, minuteFrame!.AsOfUtc); Assert.Equal(atomic.AsOfUtc, fiveFrame!.AsOfUtc);
        var later = useCase.Execute(Definition(), frames[1]); Assert.False(later.WasUpdated(Five)); Assert.True(later.IsAvailable(Five)); Assert.True(later.TryGetFrame(Five, out var retained)); Assert.Equal(Start.AddMinutes(5), retained!.AsOfUtc);
        Assert.False(later.TryGetFrame(new(60, TimeframeUnit.Minute), out _));
    }

    [Fact]
    public void ChildHistoryContainsOnlyClosedCandlesAndContextRemainsStable()
    {
        var series = Series(Minute, Candle(Minute, 0, 1), Candle(Minute, 1, 2), Candle(Minute, 2, 3)); var cursor = new MultiTimeframeCandleReplayCursor([series]);
        cursor.TryAdvance(out _); cursor.TryAdvance(out var second); var context = useCase.Execute(Definition(), second!);
        Assert.True(context.TryGetFrame(Minute, out var child)); Assert.Equal(2, child!.AvailableCandles.Count); Assert.DoesNotContain(series.Candles[2], child.AvailableCandles);
        var available = context.AvailableTimeframes.ToArray(); cursor.TryAdvance(out _); Assert.Equal(available, context.AvailableTimeframes); Assert.Equal(2, child.AvailableCandles.Count);
    }

    [Fact]
    public void ExactEquivalentDurationsStayIndependentAndRulesDoNotFilterMarketData()
    {
        var sixty = new Timeframe(60, TimeframeUnit.Minute); var frames = Frames(Series(Hour, Candle(Hour, 0, 60)), Series(sixty, Candle(sixty, 0, 60)));
        var first = useCase.Execute(Definition("a", "v1"), frames[0]); var second = useCase.Execute(Definition("b", "v2"), frames[0]);
        Assert.True(first.TryGetFrame(Hour, out var hour)); Assert.True(first.TryGetFrame(sixty, out var minutes)); Assert.Equal(Hour, hour!.Timeframe); Assert.Equal(sixty, minutes!.Timeframe);
        Assert.Equal(first.ConfiguredTimeframes, second.ConfiguredTimeframes); Assert.NotEqual(first.StrategyId, second.StrategyId);
    }

    private static StrategyDefinition Definition(string id = "test", string version = "v1") => new(new(id), new(version), "Test", "test", [new(new("R"), "Rule", "stage", 1, true, RuleDefinitionStatus.Confirmed, "d", "s")]);
    private static List<MultiTimeframeReplayFrame> Frames(params CandleSeries[] series) { var cursor = new MultiTimeframeCandleReplayCursor(series); var result = new List<MultiTimeframeReplayFrame>(); while (cursor.TryAdvance(out var frame)) result.Add(frame!); return result; }
    private static CandleSeries Series(Timeframe timeframe, params Candle[] candles) => new(Provider, Symbol, timeframe, candles);
    private static Candle Candle(Timeframe timeframe, int open, int close, decimal price = 100) => new(Provider, Symbol, timeframe, Start.AddMinutes(open), Start.AddMinutes(close), price, price + 1, price - 1, price, null);
}

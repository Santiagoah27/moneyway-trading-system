using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Backtesting.Tests;

public sealed class MultiTimeframeStrategyReplayContextScenarioTests
{
    [Fact]
    public void ContextGrowsOnlyFromClosedDataAndPublishesThreeSimultaneousTimeframesAtomically()
    {
        var provider = new MarketDataProviderId("fixture"); var symbol = new MarketSymbol("DEMO"); var start = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var minute = new Timeframe(1, TimeframeUnit.Minute); var five = new Timeframe(5, TimeframeUnit.Minute); var hour = new Timeframe(1, TimeframeUnit.Hour);
        Candle Make(Timeframe timeframe, int open, int close) => new(provider, symbol, timeframe, start.AddMinutes(open), start.AddMinutes(close), 100, 101, 99, 100, null);
        var cursor = new MultiTimeframeCandleReplayCursor([
            new(provider, symbol, minute, [Make(minute, 0, 1), Make(minute, 1, 60), Make(minute, 60, 61)]),
            new(provider, symbol, five, [Make(five, 0, 60)]),
            new(provider, symbol, hour, [Make(hour, 0, 60)])]);
        var definition = new StrategyDefinition(new("test"), new("v1"), "Test", "test", [new(new("R"), "Rule", "stage", 1, true, RuleDefinitionStatus.Confirmed, "d", "s")]);
        var contexts = new List<StrategyReplayContext>(); var factory = new CreateStrategyReplayContextUseCase();
        while (cursor.TryAdvance(out var frame)) contexts.Add(factory.Execute(definition, frame!));
        Assert.Equal([1, 2, 3], contexts.Select(x => x.Step)); Assert.Equal([minute], contexts[0].AvailableTimeframes);
        var atomic = contexts[1]; Assert.Equal(3, atomic.UpdatedTimeframes.Count); Assert.All(atomic.UpdatedTimeframes, timeframe => { Assert.True(atomic.TryGetFrame(timeframe, out var child)); Assert.Equal(atomic.AsOfUtc, child!.AsOfUtc); });
        Assert.False(contexts[2].WasUpdated(five)); Assert.True(contexts[2].TryGetFrame(five, out var retained)); Assert.Equal(atomic.AsOfUtc, retained!.AsOfUtc);
    }
}

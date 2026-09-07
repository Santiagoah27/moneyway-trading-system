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

    [Fact]
    public void CanonicalEvaluatorProducesOneObservationPerGlobalStepWithoutPartialSimultaneousCloses()
    {
        var provider = new MarketDataProviderId("fixture"); var symbol = new MarketSymbol("DEMO"); var start = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var minute = new Timeframe(1, TimeframeUnit.Minute); var five = new Timeframe(5, TimeframeUnit.Minute);
        Candle Make(Timeframe timeframe, int open, int close) => new(provider, symbol, timeframe, start.AddMinutes(open), start.AddMinutes(close), 100, 101, 99, 100, null);
        var cursor = new MultiTimeframeCandleReplayCursor([
            new(provider, symbol, minute, [Make(minute, 0, 1), Make(minute, 1, 5), Make(minute, 5, 6)]),
            new(provider, symbol, five, [Make(five, 0, 5)])]);
        var rule = new RuleId("R"); var definition = new StrategyDefinition(new("test"), new("v1"), "Test", "test", [new(rule, "Rule", "stage", 1, true, RuleDefinitionStatus.Confirmed, "d", "s")]);
        var evaluator = new ContextFake(definition, rule, minute, five); var evaluate = new EvaluateStrategyReplayContextUseCase([evaluator]); var create = new CreateStrategyReplayContextUseCase();
        var observations = new List<StrategyReplayContextObservation>();
        while (cursor.TryAdvance(out var frame)) observations.Add(evaluate.Execute(definition, create.Execute(definition, frame!)));
        Assert.Equal([1, 2, 3], observations.Select(x => x.Step)); Assert.Equal([start.AddMinutes(1), start.AddMinutes(5), start.AddMinutes(6)], observations.Select(x => x.AsOfUtc));
        Assert.All(observations, observation => Assert.Equal(observation.AsOfUtc, Assert.Single(observation.Evaluations).EvaluatedAtUtc));
        Assert.Equal(["1/0/False", "2/1/True", "3/1/False"], observations.Select(x => x.Evaluations[0].Reason));
    }

    private sealed class ContextFake(StrategyDefinition definition, RuleId ruleId, Timeframe minute, Timeframe five) : IReplayRuleEvaluator
    {
        public StrategyId StrategyId => definition.StrategyId; public StrategyVersion StrategyVersion => definition.Version; public RuleId RuleId => ruleId;
        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
        {
            context.TryGetFrame(minute, out var minuteFrame); context.TryGetFrame(five, out var fiveFrame);
            if (context.WasUpdated(five)) { Assert.True(context.WasUpdated(minute)); Assert.Equal(context.AsOfUtc, minuteFrame!.AsOfUtc); Assert.Equal(context.AsOfUtc, fiveFrame!.AsOfUtc); }
            return new(RuleEvaluationResult.Passed, $"{minuteFrame!.AvailableCandles.Count}/{fiveFrame?.AvailableCandles.Count ?? 0}/{context.WasUpdated(five)}", null);
        }
    }
}

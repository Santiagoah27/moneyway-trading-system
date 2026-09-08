using MoneyWay.Application.Backtesting;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Backtesting.Tests;

public sealed class CanonicalMultiTimeframeStrategyBacktestScenarioTests
{
    private static readonly MarketDataProviderId Provider = new("fixture"); private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute); private static readonly Timeframe Five = new(5, TimeframeUnit.Minute); private static readonly Timeframe Hour = new(1, TimeframeUnit.Hour);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FullCanonicalPipelinePreservesGlobalEventsAtomicityGapsStartsAndRetainedFrames()
    {
        var evaluator = new Fake();
        var run = Execute([Series(Minute, 1, 2, 4, 5, 6, 9), Series(Five, 5), Series(Hour, 8)], evaluator);
        Assert.Equal(7, run.ObservationCount); Assert.Equal(Enumerable.Range(1, 7), run.MarketObservations.Select(x => x.Step));
        Assert.Equal([1, 2, 4, 5, 6, 8, 9], run.MarketObservations.Select(x => (int)(x.AsOfUtc - Start).TotalMinutes).Distinct());
        Assert.Equal(7, evaluator.Contexts.Count); Assert.Equal(run.MarketObservations.Select(x => (x.Step, x.AsOfUtc)), run.StrategyObservations.Select(x => (x.Step, x.AsOfUtc)));
        var simultaneous = run.MarketObservations.Single(x => x.AsOfUtc == Start.AddMinutes(5));
        Assert.Equal([Minute, Five], simultaneous.UpdatedTimeframes); Assert.Equal([Minute, Five], simultaneous.AvailableTimeframes);
        Assert.Single(run.StrategyObservations, x => x.AsOfUtc == Start.AddMinutes(5));
        var retained = evaluator.Contexts.Single(x => x.AsOfUtc == Start.AddMinutes(6)); Assert.False(retained.WasUpdated(Five)); Assert.True(retained.TryGetFrame(Five, out var five)); Assert.Equal(Start.AddMinutes(5), five!.AsOfUtc);
        var early = evaluator.Contexts[0]; Assert.True(early.IsConfigured(Hour)); Assert.False(early.IsAvailable(Hour));
        Assert.All(run.StrategyObservations, x => Assert.Equal(x.AsOfUtc, Assert.Single(x.Evaluations).EvaluatedAtUtc));
    }

    [Fact]
    public void InputOrderReproducibilityAndFutureIsolationHoldWithExactTimeframeIdentity()
    {
        var sixtyMinutes = new Timeframe(60, TimeframeUnit.Minute);
        var common = new[] { Series(Minute, 1, 5), Series(Hour, 5), Series(sixtyMinutes, 5) };
        var first = Execute(common, new Fake()); var reordered = Execute([common[2], common[0], common[1]], new Fake());
        Assert.Equal([Minute, sixtyMinutes, Hour], first.ConfiguredTimeframes); Assert.Equal(Signatures(first), Signatures(reordered));
        var alteredFuture = Execute([Series(Minute, (1, 100m), (5, 100m), (6, 999m)), Series(Hour, 5), Series(sixtyMinutes, 5)], new Fake());
        Assert.Equal(Signatures(first).Take(2), Signatures(alteredFuture).Take(2));
    }

    private static MultiTimeframeStrategyBacktestRun Execute(IEnumerable<CandleSeries> series, Fake evaluator) =>
        new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new([evaluator])).Execute(Definition(), series);
    private static IEnumerable<object> Signatures(MultiTimeframeStrategyBacktestRun run) => run.MarketObservations.Zip(run.StrategyObservations, (market, strategy) =>
        (object)(market.Step, market.AsOfUtc, Updated: string.Join(',', market.UpdatedTimeframes), Available: string.Join(',', market.AvailableTimeframes), Result: strategy.Evaluations[0].Reason));
    private static StrategyDefinition Definition() => new(new("synthetic"), new("v1"), "Synthetic", "test", [new(new("A"), "A", "stage", 10, true, RuleDefinitionStatus.Confirmed, "description", "source")]);
    private static CandleSeries Series(Timeframe timeframe, params int[] closes) => Series(timeframe, closes.Select(close => (close, 100m)).ToArray());
    private static CandleSeries Series(Timeframe timeframe, params (int Close, decimal Price)[] values) => new(Provider, Symbol, timeframe, values.Select(value => new Candle(Provider, Symbol, timeframe, Start.AddMinutes(value.Close - 1), Start.AddMinutes(value.Close), value.Price, value.Price + 1, value.Price - 1, value.Price, null)));
    private sealed class Fake : IReplayRuleEvaluator
    {
        public StrategyId StrategyId { get; } = new("synthetic"); public StrategyVersion StrategyVersion { get; } = new("v1"); public RuleId RuleId { get; } = new("A"); public List<StrategyReplayContext> Contexts { get; } = [];
        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
        {
            Contexts.Add(context); var counts = context.AvailableTimeframes.Select(timeframe => { context.TryGetFrame(timeframe, out var frame); return frame!.AvailableCandles.Count; });
            return new(RuleEvaluationResult.Passed, string.Join('/', counts), null);
        }
    }
}

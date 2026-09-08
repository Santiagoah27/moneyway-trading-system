using MoneyWay.Application.Backtesting;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Backtesting.Tests;

public sealed class CanonicalMultiTimeframeBacktestBoundaryScenarioTests
{
    private static readonly StrategyId Strategy = new("synthetic");
    private static readonly StrategyVersion Version = new("v1");
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly Timeframe FiveMinutes = new(5, TimeframeUnit.Minute);
    private static readonly Timeframe Hour = new(1, TimeframeUnit.Hour);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FacadePreservesAtomicClosesUnavailableAndRetainedTimeframesWithOneReplay()
    {
        var evaluator = new Fake("A", _ => RuleEvaluationResult.Passed);
        var minute = Series(Minute, (1, 100m), (5, 101m), (6, 102m));
        var five = Series(FiveMinutes, (5, 100m));
        var hour = Series(Hour, (5, 100m));
        var snapshots = new[] { minute.Candles.ToArray(), five.Candles.ToArray(), hour.Candles.ToArray() };
        var facade = Facade([evaluator]);

        var report = facade.Execute(Definition("A"), [minute, five, hour]);

        Assert.Equal(3, evaluator.Invocations);
        Assert.Equal(3, report.FrameCount);
        Assert.Equal([Minute, FiveMinutes, Hour], report.ConfiguredTimeframes);
        Assert.Equal([Minute], report.Frames[0].AvailableTimeframes);
        var simultaneous = Assert.Single(report.Frames, frame => frame.AsOfUtc == Start.AddMinutes(5));
        Assert.Equal([Minute, FiveMinutes, Hour], simultaneous.UpdatedTimeframes);
        Assert.Equal([Minute, FiveMinutes, Hour], simultaneous.AvailableTimeframes);
        Assert.Single(report.OutcomeRun.Outcomes, outcome => outcome.AsOfUtc == Start.AddMinutes(5));
        Assert.Equal([Minute], report.Frames[2].UpdatedTimeframes);
        Assert.Equal([Minute, FiveMinutes, Hour], report.Frames[2].AvailableTimeframes);
        Assert.Equal(snapshots[0], minute.Candles);
        Assert.Equal(snapshots[1], five.Candles);
        Assert.Equal(snapshots[2], hour.Candles);
    }

    [Fact]
    public void FacadeIsReproducibleInputOrderIndependentAndFutureIsolatedAcrossGapsAndBounds()
    {
        var definition = Definition("A");
        var minuteA = Series(Minute, (1, 100m), (3, 101m), (6, 200m));
        var minuteB = Series(Minute, (1, 100m), (3, 101m), (6, 900m));
        var fiveA = Series(FiveMinutes, (5, 100m), (10, 200m));
        var fiveB = Series(FiveMinutes, (5, 100m), (10, 900m));
        var first = Facade([new Fake("A", EvaluateCurrentMinute)]).Execute(definition, [minuteA, fiveA]);
        var repeated = Facade([new Fake("A", EvaluateCurrentMinute)]).Execute(definition, [minuteA, fiveA]);
        var reordered = Facade([new Fake("A", EvaluateCurrentMinute)]).Execute(definition, [fiveA, minuteA]);
        var changedFuture = Facade([new Fake("A", EvaluateCurrentMinute)]).Execute(definition, [fiveB, minuteB]);

        Assert.Equal(Signatures(first), Signatures(repeated));
        Assert.Equal(Signatures(first), Signatures(reordered));
        Assert.Equal(Signatures(first).Take(3), Signatures(changedFuture).Take(3));
        Assert.NotEqual(first.Frames[3].Verdict, changedFuture.Frames[3].Verdict);

        var sixtyMinutes = new Timeframe(60, TimeframeUnit.Minute);
        var exact = Facade([new Fake("A", _ => RuleEvaluationResult.Passed)]).Execute(definition,
            [Series(Minute, (1, 100m)), Series(sixtyMinutes, (1, 100m)), Series(Hour, (1, 100m))]);
        Assert.Equal([Minute, sixtyMinutes, Hour], exact.ConfiguredTimeframes);
        Assert.Equal([Minute, sixtyMinutes, Hour], Assert.Single(exact.Frames).UpdatedTimeframes);
    }

    private static RuleEvaluationResult EvaluateCurrentMinute(StrategyReplayContext context)
    {
        context.TryGetFrame(Minute, out var frame);
        return frame!.CurrentCandle.Close < 500 ? RuleEvaluationResult.Passed : RuleEvaluationResult.Failed;
    }

    private static IEnumerable<object> Signatures(MoneyWay.Application.Backtesting.Diagnostics.MultiTimeframeStrategyBacktestDiagnosticsReport report) =>
        report.Frames.Select((frame, index) => (object)(frame.Step, frame.AsOfUtc, frame.Verdict,
            frame.HasCompleteRequiredCoverage, frame.Reason, frame.BlockingRuleId?.Value, frame.BlockingSequence,
            frame.BlockingResult, Missing: string.Join(',', frame.MissingRequiredRuleIds),
            Updated: string.Join(',', frame.UpdatedTimeframes), Available: string.Join(',', frame.AvailableTimeframes),
            Evaluations: string.Join('|', report.OutcomeRun.StrategyRun.StrategyObservations[index].Evaluations
                .Select(evaluation => $"{evaluation.RuleId}:{evaluation.Result}:{evaluation.Reason}"))));

    private static GenerateCanonicalMultiTimeframeBacktestUseCase Facade(IEnumerable<IReplayRuleEvaluator> evaluators) =>
        new(new(new(new(), new(), new(evaluators)), new()), new());

    private static StrategyDefinition Definition(params string[] rules) => new(
        Strategy, Version, "Synthetic", "test",
        rules.Select((id, index) => new StrategyRuleDefinition(new(id), id, "stage", (index + 1) * 10, true,
            RuleDefinitionStatus.Confirmed, "description", "source")));

    private static CandleSeries Series(Timeframe timeframe, params (int Close, decimal Price)[] values) => new(
        Provider, Symbol, timeframe,
        values.Select(value => new Candle(Provider, Symbol, timeframe, Start.AddMinutes(value.Close - 1),
            Start.AddMinutes(value.Close), value.Price, value.Price + 1, value.Price - 1, value.Price, null)));

    private sealed class Fake(string ruleId, Func<StrategyReplayContext, RuleEvaluationResult> result) : IReplayRuleEvaluator
    {
        public StrategyId StrategyId => Strategy;
        public StrategyVersion StrategyVersion => Version;
        public RuleId RuleId { get; } = new(ruleId);
        public int Invocations { get; private set; }

        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
        {
            Invocations++;
            return new(result(context), "Synthetic.", null);
        }
    }
}

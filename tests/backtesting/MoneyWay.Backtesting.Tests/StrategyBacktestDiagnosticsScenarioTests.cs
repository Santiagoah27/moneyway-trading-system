using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Backtesting.Tests;

public sealed class StrategyBacktestDiagnosticsScenarioTests
{
    private static readonly StrategyId Strategy = new("synthetic"); private static readonly StrategyVersion Version = new("v1");
    private static readonly MarketDataProviderId Provider = new("fixture"); private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Frame = new(5, TimeframeUnit.Minute); private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FullPipelineProjectsAndAggregatesRealBlockersInStableOrder()
    {
        var definition = Definition(Rule("A", 10), Rule("B", 20));
        var report = Pipeline(definition, Series(100, 101, 102, 103, 104), new Fake("A", _ => RuleEvaluationResult.Passed),
            new Fake("B", frame => frame.Step switch { 1 => RuleEvaluationResult.Waiting, 2 => RuleEvaluationResult.Passed, 3 => RuleEvaluationResult.Failed, 4 => RuleEvaluationResult.HumanValidationRequired, _ => RuleEvaluationResult.DataUnavailable }));
        Assert.Equal(5, report.FrameCount); Assert.Equal(1, report.ReadyCount); Assert.Equal(1, report.WaitCount); Assert.Equal(1, report.NoTradeCount); Assert.Equal(1, report.HumanValidationRequiredCount); Assert.Equal(1, report.DataUnavailableCount);
        Assert.Equal([StrategyVerdict.Wait, StrategyVerdict.NoTrade, StrategyVerdict.HumanValidationRequired, StrategyVerdict.DataUnavailable], report.BlockingRules.Select(x => x.Verdict));
        Assert.All(report.BlockingRules, item => Assert.Equal(new RuleId("B"), item.RuleId)); Assert.Equal([1, 2, 3, 4, 5], report.Frames.Select(x => x.Step));
    }

    [Fact]
    public void IncompleteCoverageCountsEachRequiredMissingRuleAndNeverCreatesBlockers()
    {
        var definition = Definition(Rule("A", 10), Rule("B", 20), Rule("C", 30));
        var report = Pipeline(definition, Series(100, 101, 102, 103, 104), new Fake("A", _ => RuleEvaluationResult.Passed));
        Assert.Equal(5, report.IncompleteRequiredCoverageCount); Assert.Equal(0, report.ReadyCount); Assert.Empty(report.BlockingRules);
        Assert.Equal([new RuleId("B"), new RuleId("C")], report.MissingRequiredRules.Select(x => x.RuleId)); Assert.All(report.MissingRequiredRules, item => Assert.Equal(5, item.Count));
    }

    [Fact]
    public void FutureValuesDoNotChangeEarlierFrameDiagnostics()
    {
        var definition = Definition(Rule("A", 10)); var evaluator = new Fake("A", frame => frame.AvailableCandles[^1].Close >= 500 ? RuleEvaluationResult.Failed : RuleEvaluationResult.Passed);
        var first = Pipeline(definition, Series(100, 101, 200), evaluator); var second = Pipeline(definition, Series(100, 101, 500), evaluator);
        Assert.Equal(first.Frames.Take(2).Select(x => (x.Step, x.AsOfUtc, x.Verdict, x.Reason)), second.Frames.Take(2).Select(x => (x.Step, x.AsOfUtc, x.Verdict, x.Reason)));
        Assert.NotEqual(first.Frames[2].Verdict, second.Frames[2].Verdict);
    }

    private static StrategyBacktestDiagnosticsReport Pipeline(StrategyDefinition definition, CandleSeries series, params IReplayRuleEvaluator[] evaluators)
    {
        var strategyRun = new GenerateStrategyBacktestRunUseCase(new RunCandleReplayUseCase(), new(evaluators));
        var outcomeRun = new GenerateStrategyOutcomeBacktestRunUseCase(strategyRun, new()).Execute(definition, series);
        return new GenerateStrategyBacktestDiagnosticsReportUseCase().Execute(definition, outcomeRun);
    }
    private static StrategyDefinition Definition(params StrategyRuleDefinition[] rules) => new(Strategy, Version, "Synthetic", "test", rules);
    private static StrategyRuleDefinition Rule(string id, int sequence) => new(new(id), id, "stage", sequence, true, RuleDefinitionStatus.Confirmed, "d", "s");
    private static CandleSeries Series(params decimal[] closes) => new(Provider, Symbol, Frame, closes.Select((close, index) => { var open = Start.AddMinutes(index * 5); return new Candle(Provider, Symbol, Frame, open, open.AddMinutes(5), 100, Math.Max(101, close), Math.Min(99, close), close, null); }));
    private sealed class Fake(string id, Func<ReplayFrame, RuleEvaluationResult> result) : IReplayRuleEvaluator
    {
        public StrategyId StrategyId => Strategy; public StrategyVersion StrategyVersion => Version; public RuleId RuleId { get; } = new(id);
        public ReplayRuleEvaluationDecision Evaluate(ReplayFrame frame) => new(result(frame), "Synthetic.", null);
    }
}

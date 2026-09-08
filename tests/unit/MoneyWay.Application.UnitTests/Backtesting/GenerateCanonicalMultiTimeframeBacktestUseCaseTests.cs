using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Backtesting;

public sealed class GenerateCanonicalMultiTimeframeBacktestUseCaseTests
{
    private static readonly StrategyId Strategy = new("synthetic");
    private static readonly StrategyVersion Version = new("v1");
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly Timeframe FiveMinutes = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConstructorAndExecuteRejectNullInputs()
    {
        var outcome = OutcomeUseCase([]);
        var diagnostics = new GenerateMultiTimeframeStrategyBacktestDiagnosticsReportUseCase();
        Assert.Throws<ArgumentNullException>(() => new GenerateCanonicalMultiTimeframeBacktestUseCase(null!, diagnostics));
        Assert.Throws<ArgumentNullException>(() => new GenerateCanonicalMultiTimeframeBacktestUseCase(outcome, null!));
        var useCase = new GenerateCanonicalMultiTimeframeBacktestUseCase(outcome, diagnostics);
        Assert.Throws<ArgumentNullException>(() => useCase.Execute(null!, [Series(Minute, 1)]));
        Assert.Throws<ArgumentNullException>(() => useCase.Execute(Definition("A"), null!));
    }

    [Fact]
    public void EmptySeriesProduceEmptyCanonicalReportAndPreserveConfiguration()
    {
        var report = Facade([]).Execute(Definition("A"), [Series(FiveMinutes), Series(Minute)]);
        Assert.IsType<MultiTimeframeStrategyBacktestDiagnosticsReport>(report);
        Assert.Equal([Minute, FiveMinutes], report.ConfiguredTimeframes);
        Assert.Equal(0, report.FrameCount);
        Assert.Empty(report.Frames);
        Assert.Empty(report.BlockingRules);
        Assert.Empty(report.MissingRequiredRules);
        Assert.Equal((0, 0, 0, 0, 0),
            (report.ReadyCount, report.WaitCount, report.NoTradeCount, report.HumanValidationRequiredCount, report.DataUnavailableCount));
        Assert.Equal((0, 0, 0),
            (report.CompleteRequiredCoverageCount, report.IncompleteRequiredCoverageCount, report.CompleteCoverageDataUnavailableCount));
    }

    [Fact]
    public void SingleTimeframeUsesCanonicalPipelineAndPreservesCompleteAuditTree()
    {
        var evaluator = new Fake("A", _ => RuleEvaluationResult.Passed);
        var source = Series(Minute, 1, 2, 3);
        var candleSnapshot = source.Candles.ToArray();
        var report = Facade([evaluator]).Execute(Definition("A"), [source]);

        Assert.Equal(3, evaluator.Invocations);
        Assert.Equal(3, report.FrameCount);
        Assert.Equal(3, report.ReadyCount);
        Assert.Same(report.OutcomeRun.StrategyRun.StrategyObservations[0], report.OutcomeRun.Outcomes[0].Observation);
        Assert.Equal(3, report.OutcomeRun.Outcomes.Count);
        Assert.Equal(3, report.OutcomeRun.StrategyRun.MarketObservations.Count);
        Assert.Equal(3, report.OutcomeRun.StrategyRun.StrategyObservations.Count);
        Assert.Equal(candleSnapshot, source.Candles);
    }

    [Fact]
    public void CompleteCoverageFlowsEveryVerdictThroughOneCanonicalEntryPoint()
    {
        var definition = Definition("A", "B");
        var first = new Fake("A", _ => RuleEvaluationResult.Passed);
        var second = new Fake("B", context => context.Step switch
        {
            1 => RuleEvaluationResult.Waiting,
            2 => RuleEvaluationResult.Passed,
            3 => RuleEvaluationResult.Failed,
            4 => RuleEvaluationResult.HumanValidationRequired,
            _ => RuleEvaluationResult.DataUnavailable,
        });
        var report = Facade([first, second]).Execute(definition, [Series(Minute, 1, 2, 3, 4, 5)]);

        Assert.Equal(5, first.Invocations);
        Assert.Equal(5, second.Invocations);
        Assert.Equal((1, 1, 1, 1, 1),
            (report.WaitCount, report.ReadyCount, report.NoTradeCount, report.HumanValidationRequiredCount, report.DataUnavailableCount));
        Assert.Equal(5, report.CompleteRequiredCoverageCount);
        Assert.Equal(1, report.CompleteCoverageDataUnavailableCount);
        Assert.Empty(report.MissingRequiredRules);
        Assert.Equal(4, report.BlockingRules.Count);
    }

    [Fact]
    public void PartialCoverageRemainsDataUnavailableWithoutLegacyFallbackOrInferredBlockers()
    {
        var report = Facade([new Fake("A", _ => RuleEvaluationResult.Passed)])
            .Execute(Definition("A", "B"), [Series(Minute, 1, 2, 3)]);
        Assert.Equal(0, report.ReadyCount);
        Assert.Equal(report.FrameCount, report.DataUnavailableCount);
        Assert.Equal(report.FrameCount, report.IncompleteRequiredCoverageCount);
        Assert.Empty(report.BlockingRules);
        var missing = Assert.Single(report.MissingRequiredRules);
        Assert.Equal(new RuleId("B"), missing.RuleId);
        Assert.Equal(report.FrameCount, missing.Count);
    }

    private static GenerateCanonicalMultiTimeframeBacktestUseCase Facade(IEnumerable<IReplayRuleEvaluator> evaluators) =>
        new(OutcomeUseCase(evaluators), new());

    private static GenerateMultiTimeframeStrategyOutcomeBacktestRunUseCase OutcomeUseCase(IEnumerable<IReplayRuleEvaluator> evaluators) =>
        new(new(new(), new(), new(evaluators)), new());

    private static StrategyDefinition Definition(params string[] rules) => new(
        Strategy, Version, "Synthetic", "test",
        rules.Select((id, index) => new StrategyRuleDefinition(new(id), id, "stage", (index + 1) * 10, true,
            RuleDefinitionStatus.Confirmed, "description", "source")));

    private static CandleSeries Series(Timeframe timeframe, params int[] closes) => new(
        Provider, Symbol, timeframe,
        closes.Select(close => new Candle(Provider, Symbol, timeframe, Start.AddMinutes(close - 1),
            Start.AddMinutes(close), 100, 101, 99, 100, null)));

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

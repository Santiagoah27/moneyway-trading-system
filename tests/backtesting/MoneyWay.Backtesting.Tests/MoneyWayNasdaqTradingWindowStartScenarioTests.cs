using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Backtesting.Tests;

public sealed class MoneyWayNasdaqTradingWindowStartScenarioTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NQ");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);

    [Fact]
    public void CanonicalEvaluationPreservesDefinitionMetadataAndRuntimeDecision()
    {
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var rule = definition.Rules.Single(item => item.RuleId.Value == "NQ-TIME-001");
        var context = Context(definition, AtUtc(13, 30));

        var observation = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll())
            .Execute(definition, context);

        var evaluation = observation.Evaluations.Single(item => item.RuleId == rule.RuleId);
        Assert.Equal(rule.RuleId, evaluation.RuleId);
        Assert.Equal(rule.Sequence, evaluation.Sequence);
        Assert.Equal(rule.IsRequired, evaluation.IsRequired);
        Assert.Equal(rule.DefinitionStatus, evaluation.DefinitionStatus);
        Assert.Equal(context.AsOfUtc, evaluation.EvaluatedAtUtc);
        Assert.Equal(RuleEvaluationResult.Passed, evaluation.Result);
        Assert.Null(evaluation.EvidenceReference);
    }

    [Fact]
    public void CanonicalBacktestEvaluatesStartBoundaryWhileRequiredCoverageRemainsIncomplete()
    {
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var selectedRuleId = new RuleId("NQ-TIME-001");
        var missingContextRuleId = new RuleId("NQ-H4-001");
        var missingLiquidityRuleId = new RuleId("NQ-LIQ-001");
        var report = Facade().Execute(definition,
        [
            new CandleSeries(Provider, Symbol, Minute,
            [
                Candle(AtUtc(13, 28, 59), AtUtc(13, 29, 59), 100),
                Candle(AtUtc(13, 29, 59), AtUtc(13, 30), 101),
            ]),
        ]);

        Assert.Equal(2, report.FrameCount);
        Assert.Equal(0, report.ReadyCount);
        Assert.Equal(2, report.DataUnavailableCount);
        Assert.Equal(2, report.IncompleteRequiredCoverageCount);
        Assert.Contains(report.MissingRequiredRules, item => item.RuleId == missingContextRuleId);
        Assert.Contains(report.MissingRequiredRules, item => item.RuleId == missingLiquidityRuleId);
        Assert.DoesNotContain(report.MissingRequiredRules, item => item.RuleId == selectedRuleId);
        Assert.All(report.OutcomeRun.Outcomes, outcome =>
        {
            Assert.False(outcome.HasCompleteRequiredCoverage);
            Assert.DoesNotContain(selectedRuleId, outcome.MissingRequiredRuleIds);
            Assert.Contains(missingContextRuleId, outcome.MissingRequiredRuleIds);
            Assert.Contains(missingLiquidityRuleId, outcome.MissingRequiredRuleIds);
        });

        var evaluations = report.OutcomeRun.StrategyRun.StrategyObservations
            .Select(observation => observation.Evaluations.Single(evaluation => evaluation.RuleId == selectedRuleId))
            .ToArray();
        Assert.Equal([RuleEvaluationResult.Waiting, RuleEvaluationResult.Passed], evaluations.Select(item => item.Result));
        Assert.All(evaluations, evaluation => Assert.Equal(selectedRuleId, evaluation.RuleId));
    }

    private static GenerateCanonicalMultiTimeframeBacktestUseCase Facade() =>
        new(new(new(new RunMultiTimeframeReplayUseCase(), new(), new([new MoneyWayNasdaqTradingWindowStartEvaluator()])), new()),
            new GenerateMultiTimeframeStrategyBacktestDiagnosticsReportUseCase());

    private static StrategyReplayContext Context(StrategyDefinition definition, DateTimeOffset at)
    {
        StrategyReplayContext? context = null;
        new RunMultiTimeframeReplayUseCase().Execute(
            [new CandleSeries(Provider, Symbol, Minute, [Candle(at.AddMinutes(-1), at, 100)])],
            frame => context = new CreateStrategyReplayContextUseCase().Execute(definition, frame));
        return context!;
    }

    private static DateTimeOffset AtUtc(int hour, int minute = 0, int second = 0) =>
        new(2026, 1, 15, hour, minute, second, TimeSpan.Zero);

    private static Candle Candle(DateTimeOffset open, DateTimeOffset close, decimal value) =>
        new(Provider, Symbol, Minute, open, close, value, value + 1, value - 1, value, null);
}

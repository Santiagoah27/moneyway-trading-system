using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Backtesting;

public sealed class CanonicalReplayPrerequisiteGatingTests
{
    private static readonly StrategyId Strategy = new("synthetic-workflow");
    private static readonly StrategyVersion Version = new("v1");
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StrategyWithoutWorkflowMetadataPreservesCanonicalBehaviorExactly()
    {
        var definition = Definition("A", "B");
        var series = Series(Provider, Symbol, 1, 2, 3);
        var evaluators = new IReplayRuleEvaluator[]
        {
            new Fake("A", _ => RuleEvaluationResult.Passed),
            new Fake("B", context => context.Step == 2 ? RuleEvaluationResult.Waiting : RuleEvaluationResult.Passed),
        };

        var baseline = Report(definition, series, evaluators);
        var explicitEmpty = Report(definition, series, evaluators, StrategyReplayWorkflowCatalog.Empty);

        Assert.Equal(Signatures(baseline), Signatures(explicitEmpty));
        Assert.All(baseline.OutcomeRun.StrategyRun.StrategyObservations, observation => Assert.Null(observation.WorkflowProgression));
        Assert.All(baseline.Frames, frame => Assert.Null(frame.WorkflowProgression));
    }

    [Fact]
    public void CanonicalRunnerPreservesRawEvaluationsAndProjectsChronologicalEligibilityDiagnostics()
    {
        var definition = Definition("A", "B", "C", "D");
        var workflow = new StrategyReplayWorkflowDefinition(
            Strategy,
            Version,
            [new(new("B"), [new("A")]), new(new("C"), [new("A"), new("B")])]);
        var catalog = new StrategyReplayWorkflowCatalog([definition], [workflow]);
        var evaluators = new IReplayRuleEvaluator[]
        {
            new Fake("A", context => context.Step == 2 ? RuleEvaluationResult.Passed : RuleEvaluationResult.Failed),
            new Fake("B", context => context.Step == 4 ? RuleEvaluationResult.Failed : RuleEvaluationResult.Passed),
            new Fake("C", _ => RuleEvaluationResult.Passed),
            new Fake("D", _ => RuleEvaluationResult.Passed),
        };

        var report = Report(definition, Series(Provider, Symbol, 1, 2, 3, 4), evaluators, catalog);

        Assert.Equal(4, report.FrameCount);
        var first = report.OutcomeRun.StrategyRun.StrategyObservations[0];
        Assert.Equal(RuleEvaluationResult.Passed, first.Evaluations.Single(item => item.RuleId == new RuleId("B")).Result);
        Assert.False(Eligibility(first, "B").IsEligible);
        Assert.Equal([new RuleId("A")], Eligibility(first, "B").MissingPrerequisiteRuleIds);
        Assert.True(Eligibility(first, "D").IsEligible);

        var second = report.OutcomeRun.StrategyRun.StrategyObservations[1];
        Assert.Contains(new RuleId("A"), second.WorkflowProgression!.EstablishedRuleIds);
        Assert.DoesNotContain(new RuleId("B"), second.WorkflowProgression.EstablishedRuleIds);
        Assert.False(Eligibility(second, "B").IsEligible);

        var third = report.OutcomeRun.StrategyRun.StrategyObservations[2];
        Assert.True(Eligibility(third, "B").IsEligible);
        Assert.False(Eligibility(third, "C").IsEligible);
        Assert.Contains(new RuleId("B"), third.WorkflowProgression!.EstablishedRuleIds);

        var fourth = report.OutcomeRun.StrategyRun.StrategyObservations[3];
        Assert.Equal(RuleEvaluationResult.Failed, fourth.Evaluations.Single(item => item.RuleId == new RuleId("B")).Result);
        Assert.True(Eligibility(fourth, "C").IsEligible);
        Assert.Contains(new RuleId("C"), fourth.WorkflowProgression!.EstablishedRuleIds);
        Assert.All(report.Frames, frame => Assert.Same(
            report.OutcomeRun.StrategyRun.StrategyObservations[frame.Step - 1].WorkflowProgression,
            frame.WorkflowProgression));
    }

    [Fact]
    public void ReusingCanonicalUseCaseDoesNotShareProgressionAcrossReplayExecutions()
    {
        var definition = Definition("A", "B");
        var workflow = new StrategyReplayWorkflowDefinition(Strategy, Version, [new(new("B"), [new("A")])]);
        var catalog = new StrategyReplayWorkflowCatalog([definition], [workflow]);
        var useCase = Runner(
            [new Fake("A", _ => RuleEvaluationResult.Passed), new Fake("B", _ => RuleEvaluationResult.Passed)],
            catalog);

        var firstRun = useCase.Execute(definition, [Series(Provider, Symbol, 1, 2)]);
        var otherIdentityRun = useCase.Execute(definition, [Series(new("other-provider"), new("OTHER"), 1)]);

        Assert.False(Eligibility(firstRun.StrategyObservations[0], "B").IsEligible);
        Assert.True(Eligibility(firstRun.StrategyObservations[1], "B").IsEligible);
        Assert.False(Eligibility(otherIdentityRun.StrategyObservations[0], "B").IsEligible);
        Assert.Equal(new MarketDataProviderId("other-provider"), otherIdentityRun.ProviderId);
        Assert.Equal(new MarketSymbol("OTHER"), otherIdentityRun.Symbol);
    }

    private static StrategyReplayRuleEligibility Eligibility(StrategyReplayContextObservation observation, string ruleId) =>
        observation.WorkflowProgression!.RuleEligibility.Single(item => item.RuleId == new RuleId(ruleId));

    private static MultiTimeframeStrategyBacktestDiagnosticsReport Report(
        StrategyDefinition definition,
        CandleSeries series,
        IEnumerable<IReplayRuleEvaluator> evaluators,
        StrategyReplayWorkflowCatalog? workflowCatalog = null)
    {
        var strategyRun = workflowCatalog is null
            ? new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(evaluators))
            : Runner(evaluators, workflowCatalog);
        var outcomes = new GenerateMultiTimeframeStrategyOutcomeBacktestRunUseCase(strategyRun, new()).Execute(definition, [series]);
        return new GenerateMultiTimeframeStrategyBacktestDiagnosticsReportUseCase().Execute(definition, outcomes);
    }

    private static GenerateMultiTimeframeStrategyBacktestRunUseCase Runner(
        IEnumerable<IReplayRuleEvaluator> evaluators,
        StrategyReplayWorkflowCatalog workflowCatalog) => new(
            new RunMultiTimeframeReplayUseCase(),
            new CreateStrategyReplayContextUseCase(),
            new EvaluateStrategyReplayContextUseCase(evaluators),
            workflowCatalog,
            new AdvanceStrategyReplayProgressionUseCase());

    private static string[] Signatures(MultiTimeframeStrategyBacktestDiagnosticsReport report) => report.Frames.Select((frame, index) =>
    {
        var observation = report.OutcomeRun.StrategyRun.StrategyObservations[index];
        return string.Join('|',
            frame.Step,
            frame.AsOfUtc,
            frame.Verdict,
            frame.HasCompleteRequiredCoverage,
            frame.Reason,
            string.Join(';', observation.Evaluations.Select(evaluation => $"{evaluation.RuleId}:{evaluation.Result}:{evaluation.Reason}")));
    }).ToArray();

    private static StrategyDefinition Definition(params string[] ruleIds) => new(
        Strategy,
        Version,
        "Synthetic",
        "test",
        ruleIds.Select((ruleId, index) => new StrategyRuleDefinition(
            new(ruleId),
            ruleId,
            "stage",
            (index + 1) * 10,
            true,
            RuleDefinitionStatus.Confirmed,
            "description",
            "source")));

    private static CandleSeries Series(
        MarketDataProviderId provider,
        MarketSymbol symbol,
        params int[] closes) => new(
            provider,
            symbol,
            Minute,
            closes.Select(close => new Candle(
                provider,
                symbol,
                Minute,
                Start.AddMinutes(close - 1),
                Start.AddMinutes(close),
                100,
                101,
                99,
                100,
                null)));

    private sealed class Fake(string ruleId, Func<StrategyReplayContext, RuleEvaluationResult> evaluate) : IReplayRuleEvaluator
    {
        public StrategyId StrategyId => Strategy;
        public StrategyVersion StrategyVersion => Version;
        public RuleId RuleId { get; } = new(ruleId);

        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context) =>
            new(evaluate(context), "Synthetic.", null);
    }
}

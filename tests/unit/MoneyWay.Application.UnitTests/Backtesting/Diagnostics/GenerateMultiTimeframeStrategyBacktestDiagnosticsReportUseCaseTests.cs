using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;
using MoneyWay.Domain.Strategies.Evaluation;

namespace MoneyWay.Application.UnitTests.Backtesting.Diagnostics;

public sealed class GenerateMultiTimeframeStrategyBacktestDiagnosticsReportUseCaseTests
{
    private static readonly StrategyId Strategy = new("synthetic");
    private static readonly StrategyVersion Version = new("v1");
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly Timeframe FiveMinutes = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ExecuteRejectsNullAndMismatchedStrategyIdentity()
    {
        var useCase = new GenerateMultiTimeframeStrategyBacktestDiagnosticsReportUseCase();
        var run = Run([Ready(1)], [Market(1)]);
        Assert.Throws<ArgumentNullException>(() => useCase.Execute(null!, run));
        Assert.Throws<ArgumentNullException>(() => useCase.Execute(Definition(), null!));
        Assert.Throws<InvalidOperationException>(() => useCase.Execute(Definition(strategy: new("other")), run));
        Assert.Throws<InvalidOperationException>(() => useCase.Execute(Definition(version: new("v2")), run));
    }

    [Fact]
    public void EmptyRunPreservesMetadataAndDelegatesAllZeroCounts()
    {
        var report = Execute(Definition(), Run([], [], [Minute, FiveMinutes]));
        Assert.Equal(Strategy, report.StrategyId);
        Assert.Equal(Version, report.StrategyVersion);
        Assert.Equal(Provider, report.ProviderId);
        Assert.Equal(Symbol, report.Symbol);
        Assert.Equal([Minute, FiveMinutes], report.ConfiguredTimeframes);
        Assert.Equal(0, report.FrameCount);
        Assert.Null(report.FirstAsOfUtc);
        Assert.Null(report.LastAsOfUtc);
        Assert.Empty(report.Frames);
        Assert.Empty(report.BlockingRules);
        Assert.Empty(report.MissingRequiredRules);
        Assert.Equal((0, 0, 0, 0, 0), (report.ReadyCount, report.WaitCount, report.NoTradeCount, report.HumanValidationRequiredCount, report.DataUnavailableCount));
        Assert.Equal((0, 0, 0), (report.CompleteRequiredCoverageCount, report.IncompleteRequiredCoverageCount, report.CompleteCoverageDataUnavailableCount));
    }

    [Fact]
    public void OneReadyOutcomeCopiesGlobalAndRetainedTimeframeStateWithoutAggregates()
    {
        var outcome = Ready(1);
        var report = Execute(Definition(), Run([outcome], [Market(1, [Minute], [Minute, FiveMinutes])], [Minute, FiveMinutes]));
        var frame = Assert.Single(report.Frames);
        Assert.Equal((outcome.Step, outcome.AsOfUtc, outcome.Verdict, outcome.HasCompleteRequiredCoverage, outcome.Reason),
            (frame.Step, frame.AsOfUtc, frame.Verdict, frame.HasCompleteRequiredCoverage, frame.Reason));
        Assert.Equal([Minute], frame.UpdatedTimeframes);
        Assert.Equal([Minute, FiveMinutes], frame.AvailableTimeframes);
        Assert.Empty(report.BlockingRules);
        Assert.Empty(report.MissingRequiredRules);
        Assert.Equal(report.OutcomeRun.ReadyCount, report.ReadyCount);
        Assert.Equal(report.OutcomeRun.CompleteRequiredCoverageCount, report.CompleteRequiredCoverageCount);
    }

    [Fact]
    public void RealBlockersAggregateByRuleVerdictAndResultInStableOrder()
    {
        var outcomes = new[]
        {
            Blocked(1, "B", 20, StrategyVerdict.NoTrade, RuleEvaluationResult.Failed),
            Blocked(2, "B", 20, StrategyVerdict.Wait, RuleEvaluationResult.Waiting),
            Blocked(3, "C", 30, StrategyVerdict.HumanValidationRequired, RuleEvaluationResult.HumanValidationRequired),
            Blocked(4, "B", 20, StrategyVerdict.Wait, RuleEvaluationResult.Waiting),
            Blocked(5, "A", 10, StrategyVerdict.DataUnavailable, RuleEvaluationResult.DataUnavailable),
            Blocked(6, "B", 20, StrategyVerdict.Wait, RuleEvaluationResult.Waiting),
        };
        var report = Execute(Definition(), Run(outcomes, Markets(6)));

        Assert.Equal(4, report.BlockingRules.Count);
        Assert.Equal(
            [("B", StrategyVerdict.Wait, RuleEvaluationResult.Waiting, 3), ("B", StrategyVerdict.NoTrade, RuleEvaluationResult.Failed, 1), ("C", StrategyVerdict.HumanValidationRequired, RuleEvaluationResult.HumanValidationRequired, 1), ("A", StrategyVerdict.DataUnavailable, RuleEvaluationResult.DataUnavailable, 1)],
            report.BlockingRules.Select(item => (item.RuleId.Value, item.Verdict, item.BlockingResult, item.Count)));
    }

    [Fact]
    public void FrameProjectionPreservesConfiguredUnavailableSimultaneousAndRetainedTimeframes()
    {
        var outcomes = new[] { Ready(1), Ready(2), Ready(3) };
        var markets = new[]
        {
            Market(1, [Minute], [Minute]),
            Market(2, [Minute, FiveMinutes], [Minute, FiveMinutes]),
            Market(3, [Minute], [Minute, FiveMinutes]),
        };
        var report = Execute(Definition(), Run(outcomes, markets, [Minute, FiveMinutes, new(1, TimeframeUnit.Hour)]));
        Assert.Equal([Minute], report.Frames[0].UpdatedTimeframes);
        Assert.Equal([Minute], report.Frames[0].AvailableTimeframes);
        Assert.Equal([Minute, FiveMinutes], report.Frames[1].UpdatedTimeframes);
        Assert.Equal([Minute, FiveMinutes], report.Frames[1].AvailableTimeframes);
        Assert.Equal([Minute], report.Frames[2].UpdatedTimeframes);
        Assert.Equal([Minute, FiveMinutes], report.Frames[2].AvailableTimeframes);
        Assert.Equal(3, report.ConfiguredTimeframes.Count);
    }

    [Fact]
    public void MissingRequiredRulesAggregateOncePerGlobalStepInDefinitionOrder()
    {
        var outcomes = new[]
        {
            Incomplete(1, "A", "B"),
            Incomplete(2, "B"),
            Incomplete(3, "A", "B", "C"),
        };
        var report = Execute(Definition(), Run(outcomes, Markets(3)));
        Assert.Empty(report.BlockingRules);
        Assert.Equal([("A", 10, 2), ("B", 20, 3), ("C", 30, 1)],
            report.MissingRequiredRules.Select(item => (item.RuleId.Value, item.Sequence, item.Count)));
        Assert.Equal(0, report.ReadyCount);
        Assert.Equal(3, report.IncompleteRequiredCoverageCount);
        Assert.Equal(3, report.DataUnavailableCount);
    }

    [Theory]
    [InlineData("unknown-missing")]
    [InlineData("optional-missing")]
    [InlineData("unknown-blocker")]
    [InlineData("blocker-sequence")]
    public void ExecuteRejectsRuleMetadataNotAuthorizedByDefinition(string scenario)
    {
        var definition = Definition(optionalRuleId: "O");
        var outcome = scenario switch
        {
            "unknown-missing" => Incomplete(1, "UNKNOWN"),
            "optional-missing" => Incomplete(1, "O"),
            "unknown-blocker" => Blocked(1, "UNKNOWN", 40, StrategyVerdict.Wait, RuleEvaluationResult.Waiting),
            "blocker-sequence" => Blocked(1, "B", 999, StrategyVerdict.Wait, RuleEvaluationResult.Waiting),
            _ => throw new InvalidOperationException(),
        };
        Assert.Throws<InvalidOperationException>(() => Execute(definition, Run([outcome], [Market(1)])));
    }

    [Fact]
    public void CompleteAndIncompleteDataUnavailableRemainDistinct()
    {
        var outcomes = new[]
        {
            Incomplete(1, "B"),
            Incomplete(2, "C"),
            Blocked(3, "A", 10, StrategyVerdict.DataUnavailable, RuleEvaluationResult.DataUnavailable),
            Ready(4),
            Blocked(5, "B", 20, StrategyVerdict.Wait, RuleEvaluationResult.Waiting),
        };
        var report = Execute(Definition(), Run(outcomes, Markets(5)));
        Assert.Equal(3, report.DataUnavailableCount);
        Assert.Equal(2, report.IncompleteRequiredCoverageCount);
        Assert.Equal(1, report.CompleteCoverageDataUnavailableCount);
        Assert.Equal([("B", 1), ("C", 1)], report.MissingRequiredRules.Select(item => (item.RuleId.Value, item.Count)));
        Assert.Contains(report.BlockingRules, item => item.RuleId == new RuleId("A") && item.Verdict == StrategyVerdict.DataUnavailable);
    }

    private static MultiTimeframeStrategyBacktestDiagnosticsReport Execute(StrategyDefinition definition, MultiTimeframeStrategyOutcomeBacktestRun run) =>
        new GenerateMultiTimeframeStrategyBacktestDiagnosticsReportUseCase().Execute(definition, run);

    private static StrategyDefinition Definition(
        StrategyId? strategy = null,
        StrategyVersion? version = null,
        string? optionalRuleId = null)
    {
        var rules = new List<StrategyRuleDefinition>
        {
            Rule("A", 10, true), Rule("B", 20, true), Rule("C", 30, true),
        };
        if (optionalRuleId is not null) rules.Add(Rule(optionalRuleId, 40, false));
        return new(strategy ?? Strategy, version ?? Version, "Synthetic", "test", rules);
    }

    private static StrategyRuleDefinition Rule(string id, int sequence, bool required) =>
        new(new(id), id, "stage", sequence, required, RuleDefinitionStatus.Confirmed, "description", "source");

    private static MultiTimeframeStrategyOutcomeBacktestRun Run(
        IReadOnlyList<StrategyReplayContextOutcome> outcomes,
        IReadOnlyList<MultiTimeframeBacktestObservation> markets,
        IReadOnlyList<Timeframe>? configured = null)
    {
        var strategyRun = new MultiTimeframeStrategyBacktestRun(
            Strategy, Version, Provider, Symbol, configured ?? [Minute], markets, outcomes.Select(item => item.Observation));
        return new(strategyRun, outcomes);
    }

    private static MultiTimeframeBacktestObservation[] Markets(int count) =>
        Enumerable.Range(1, count).Select(step => Market(step)).ToArray();

    private static MultiTimeframeBacktestObservation Market(
        int step,
        IEnumerable<Timeframe>? updated = null,
        IEnumerable<Timeframe>? available = null) =>
        new(step, Start.AddMinutes(step), updated ?? [Minute], available ?? [Minute]);

    private static StrategyReplayContextOutcome Ready(int step)
    {
        var at = Start.AddMinutes(step);
        var observation = Observation(step, [new(new("A"), RuleDefinitionStatus.Confirmed, RuleEvaluationResult.Passed, 10, true, "Ready.", at, null)]);
        var evaluation = new StrategyEvaluationOutcome(StrategyVerdict.Ready, null, null, null, "Ready.", 1, 1);
        return new(observation, true, [], evaluation.Verdict, evaluation.Reason, evaluation);
    }

    private static StrategyReplayContextOutcome Blocked(
        int step,
        string ruleId,
        int sequence,
        StrategyVerdict verdict,
        RuleEvaluationResult result)
    {
        var at = Start.AddMinutes(step);
        var reason = $"Blocked by {ruleId}.";
        var observation = Observation(step, [new(new(ruleId), RuleDefinitionStatus.Confirmed, result, sequence, true, reason, at, null)]);
        var evaluation = new StrategyEvaluationOutcome(verdict, new(ruleId), sequence, result, reason, 1, 1);
        return new(observation, true, [], verdict, reason, evaluation);
    }

    private static StrategyReplayContextOutcome Incomplete(int step, params string[] missing)
    {
        var observation = Observation(step, []);
        return new(observation, false, missing.Select(id => new RuleId(id)), StrategyVerdict.DataUnavailable,
            StrategyReplayContextOutcome.IncompleteCoverageReason, null);
    }

    private static StrategyReplayContextObservation Observation(int step, IEnumerable<RuleEvaluation> evaluations) =>
        new(Strategy, Version, Provider, Symbol, step, Start.AddMinutes(step), evaluations);
}

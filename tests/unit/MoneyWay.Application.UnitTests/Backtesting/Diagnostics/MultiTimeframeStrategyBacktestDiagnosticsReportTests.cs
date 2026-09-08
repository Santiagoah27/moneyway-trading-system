using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;
using MoneyWay.Domain.Strategies.Evaluation;

namespace MoneyWay.Application.UnitTests.Backtesting.Diagnostics;

public sealed class MultiTimeframeStrategyBacktestDiagnosticsReportTests
{
    private static readonly StrategyId Strategy = new("synthetic");
    private static readonly StrategyVersion Version = new("v1");
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly Timeframe FiveMinutes = new(5, TimeframeUnit.Minute);
    private static readonly Timeframe Hour = new(1, TimeframeUnit.Hour);
    private static readonly DateTimeOffset At = new(2026, 1, 1, 0, 1, 0, TimeSpan.Zero);

    [Fact]
    public void EmptyReportPreservesOutcomeRunMetadataAndZeroCounts()
    {
        var run = Run([], []);
        var report = new MultiTimeframeStrategyBacktestDiagnosticsReport(run, [], [], []);
        Assert.Same(run, report.OutcomeRun);
        Assert.Equal(Strategy, report.StrategyId);
        Assert.Equal(Version, report.StrategyVersion);
        Assert.Equal(Provider, report.ProviderId);
        Assert.Equal(Symbol, report.Symbol);
        Assert.Equal([Minute, FiveMinutes], report.ConfiguredTimeframes);
        Assert.Equal(0, report.FrameCount);
        Assert.Null(report.FirstAsOfUtc);
        Assert.Null(report.LastAsOfUtc);
        Assert.Equal((0, 0, 0, 0, 0), (report.ReadyCount, report.WaitCount, report.NoTradeCount, report.HumanValidationRequiredCount, report.DataUnavailableCount));
        Assert.Equal((0, 0, 0), (report.CompleteRequiredCoverageCount, report.IncompleteRequiredCoverageCount, report.CompleteCoverageDataUnavailableCount));
    }

    [Fact]
    public void ConstructorRejectsNullCollectionsAndItems()
    {
        var run = Run([], []);
        Assert.Throws<ArgumentNullException>(() => new MultiTimeframeStrategyBacktestDiagnosticsReport(null!, [], [], []));
        Assert.Throws<ArgumentNullException>(() => new MultiTimeframeStrategyBacktestDiagnosticsReport(run, null!, [], []));
        Assert.Throws<ArgumentNullException>(() => new MultiTimeframeStrategyBacktestDiagnosticsReport(run, [], null!, []));
        Assert.Throws<ArgumentNullException>(() => new MultiTimeframeStrategyBacktestDiagnosticsReport(run, [], [], null!));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeStrategyBacktestDiagnosticsReport(run, new MultiTimeframeStrategyFrameDiagnostic[] { null! }, [], []));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeStrategyBacktestDiagnosticsReport(run, [], new MultiTimeframeStrategyBlockingRuleCount[] { null! }, []));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeStrategyBacktestDiagnosticsReport(run, [], [], new MultiTimeframeMissingRequiredRuleCount[] { null! }));
    }

    [Theory]
    [InlineData("count")]
    [InlineData("step")]
    [InlineData("time")]
    [InlineData("verdict")]
    [InlineData("coverage")]
    [InlineData("reason")]
    [InlineData("blocking")]
    [InlineData("missing")]
    [InlineData("updated")]
    [InlineData("available")]
    [InlineData("unknown-updated")]
    [InlineData("unknown-available")]
    [InlineData("timeframe-order")]
    public void ConstructorRejectsFrameMisalignment(string scenario)
    {
        var outcome = BlockedOutcome();
        var market = new MultiTimeframeBacktestObservation(1, At, [Minute], [Minute, FiveMinutes]);
        var run = Run([outcome], [market]);
        var frame = scenario switch
        {
            "count" => null,
            "step" => Frame(step: 2),
            "time" => Frame(asOf: At.AddMinutes(1)),
            "verdict" => Frame(verdict: StrategyVerdict.NoTrade, blockingResult: RuleEvaluationResult.Waiting),
            "coverage" => new MultiTimeframeStrategyFrameDiagnostic(1, At, StrategyVerdict.DataUnavailable, false, StrategyReplayContextOutcome.IncompleteCoverageReason, null, null, null, [new("A")], [Minute], [Minute, FiveMinutes]),
            "reason" => Frame(reason: "Different."),
            "blocking" => Frame(blockingRuleId: new("C")),
            "missing" => new MultiTimeframeStrategyFrameDiagnostic(1, At, StrategyVerdict.DataUnavailable, false, StrategyReplayContextOutcome.IncompleteCoverageReason, null, null, null, [new("A")], [Minute], [Minute, FiveMinutes]),
            "updated" => Frame(updated: [FiveMinutes]),
            "available" => Frame(available: [Minute]),
            "unknown-updated" => Frame(updated: [Hour], available: [Minute, FiveMinutes, Hour]),
            "unknown-available" => Frame(available: [Minute, FiveMinutes, Hour]),
            "timeframe-order" => Frame(updated: [FiveMinutes, Minute], available: [FiveMinutes, Minute]),
            _ => throw new InvalidOperationException(),
        };
        var frames = frame is null ? [] : new[] { frame };
        Assert.Throws<ArgumentException>(() => new MultiTimeframeStrategyBacktestDiagnosticsReport(run, frames, [], []));
    }

    [Fact]
    public void ConstructorRejectsIncorrectOrUnstableAggregateCollections()
    {
        var firstOutcome = BlockedOutcome();
        var secondOutcome = IncompleteOutcome(2);
        var run = Run([firstOutcome, secondOutcome],
            [new(1, At, [Minute], [Minute]), new(2, At.AddMinutes(1), [Minute], [Minute])]);
        var frames = new[]
        {
            Frame(available: [Minute]),
            new MultiTimeframeStrategyFrameDiagnostic(2, At.AddMinutes(1), StrategyVerdict.DataUnavailable, false,
                StrategyReplayContextOutcome.IncompleteCoverageReason, null, null, null, [new("A")], [Minute], [Minute]),
        };
        var blocker = new MultiTimeframeStrategyBlockingRuleCount(new("B"), 20, StrategyVerdict.Wait, RuleEvaluationResult.Waiting, 1);
        var missing = new MultiTimeframeMissingRequiredRuleCount(new("A"), 10, 1);
        Assert.Throws<ArgumentException>(() => new MultiTimeframeStrategyBacktestDiagnosticsReport(run, frames, [], [missing]));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeStrategyBacktestDiagnosticsReport(run, frames, [blocker], []));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeStrategyBacktestDiagnosticsReport(run, frames,
            [new(new("B"), 20, StrategyVerdict.Wait, RuleEvaluationResult.Waiting, 2)], [missing]));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeStrategyBacktestDiagnosticsReport(run, frames, [blocker],
            [new(new("A"), 10, 2)]));
    }

    [Fact]
    public void ConstructorRejectsMissingRuleIdentifierMismatch()
    {
        var outcome = IncompleteOutcome(1);
        var run = Run([outcome], [new(1, At, [Minute], [Minute])]);
        var frame = new MultiTimeframeStrategyFrameDiagnostic(1, At, StrategyVerdict.DataUnavailable, false,
            StrategyReplayContextOutcome.IncompleteCoverageReason, null, null, null, [new("B")], [Minute], [Minute]);
        Assert.Throws<ArgumentException>(() => new MultiTimeframeStrategyBacktestDiagnosticsReport(run, [frame], [],
            [new(new("B"), 20, 1)]));
    }

    [Fact]
    public void ConstructorRejectsDuplicateMissingAggregatesWithDisguisedSequences()
    {
        var observation = new StrategyReplayContextObservation(Strategy, Version, Provider, Symbol, 1, At, []);
        var outcome = new StrategyReplayContextOutcome(observation, false, [new("A"), new("B")], StrategyVerdict.DataUnavailable,
            StrategyReplayContextOutcome.IncompleteCoverageReason, null);
        var run = Run([outcome], [new(1, At, [Minute], [Minute])]);
        var frame = new MultiTimeframeStrategyFrameDiagnostic(1, At, StrategyVerdict.DataUnavailable, false,
            StrategyReplayContextOutcome.IncompleteCoverageReason, null, null, null, [new("A"), new("B")], [Minute], [Minute]);
        Assert.Throws<ArgumentException>(() => new MultiTimeframeStrategyBacktestDiagnosticsReport(run, [frame], [],
            [new(new("A"), 10, 1), new(new("A"), 20, 1)]));
    }

    [Fact]
    public void ConstructorDefensivelyCopiesAllCollectionsAndDelegatesNonEmptyMetadata()
    {
        var outcome = BlockedOutcome();
        var run = Run([outcome], [new(1, At, [Minute], [Minute])]);
        var frames = new List<MultiTimeframeStrategyFrameDiagnostic> { Frame(available: [Minute]) };
        var blockers = new List<MultiTimeframeStrategyBlockingRuleCount> { new(new("B"), 20, StrategyVerdict.Wait, RuleEvaluationResult.Waiting, 1) };
        var missing = new List<MultiTimeframeMissingRequiredRuleCount>();
        var report = new MultiTimeframeStrategyBacktestDiagnosticsReport(run, frames, blockers, missing);
        frames.Clear();
        blockers.Clear();
        missing.Add(new(new("A"), 10, 1));
        Assert.Single(report.Frames);
        Assert.Single(report.BlockingRules);
        Assert.Empty(report.MissingRequiredRules);
        Assert.Equal(At, report.FirstAsOfUtc);
        Assert.Equal(At, report.LastAsOfUtc);
        Assert.Equal(run.WaitCount, report.WaitCount);
        Assert.Equal(run.CompleteRequiredCoverageCount, report.CompleteRequiredCoverageCount);
    }

    private static MultiTimeframeStrategyFrameDiagnostic Frame(
        int step = 1,
        DateTimeOffset? asOf = null,
        StrategyVerdict verdict = StrategyVerdict.Wait,
        string reason = "Blocked.",
        RuleId? blockingRuleId = null,
        RuleEvaluationResult blockingResult = RuleEvaluationResult.Waiting,
        IEnumerable<Timeframe>? updated = null,
        IEnumerable<Timeframe>? available = null) => new(
            step, asOf ?? At, verdict, true, reason, blockingRuleId ?? new("B"), 20, blockingResult, [],
            updated ?? [Minute], available ?? [Minute, FiveMinutes]);

    private static MultiTimeframeStrategyOutcomeBacktestRun Run(
        IReadOnlyList<StrategyReplayContextOutcome> outcomes,
        IReadOnlyList<MultiTimeframeBacktestObservation> markets)
    {
        var strategyRun = new MultiTimeframeStrategyBacktestRun(
            Strategy, Version, Provider, Symbol, [Minute, FiveMinutes], markets, outcomes.Select(item => item.Observation));
        return new(strategyRun, outcomes);
    }

    private static StrategyReplayContextOutcome BlockedOutcome()
    {
        var observation = new StrategyReplayContextObservation(Strategy, Version, Provider, Symbol, 1, At,
            [new(new("B"), RuleDefinitionStatus.Confirmed, RuleEvaluationResult.Waiting, 20, true, "Blocked.", At, null)]);
        var evaluation = new StrategyEvaluationOutcome(StrategyVerdict.Wait, new("B"), 20, RuleEvaluationResult.Waiting, "Blocked.", 1, 1);
        return new(observation, true, [], evaluation.Verdict, evaluation.Reason, evaluation);
    }

    private static StrategyReplayContextOutcome IncompleteOutcome(int step)
    {
        var at = At.AddMinutes(step - 1);
        var observation = new StrategyReplayContextObservation(Strategy, Version, Provider, Symbol, step, at, []);
        return new(observation, false, [new("A")], StrategyVerdict.DataUnavailable,
            StrategyReplayContextOutcome.IncompleteCoverageReason, null);
    }
}

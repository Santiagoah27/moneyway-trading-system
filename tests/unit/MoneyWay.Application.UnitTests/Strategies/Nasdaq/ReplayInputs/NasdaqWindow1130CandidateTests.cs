using System.Text.Json;
using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqWindow1130CandidateTests
{
    [Fact]
    public void CompositionIsOptInAndChangesOnlyTheParentCutoffRule()
    {
        var parent = MoneyWayNasdaqSessionCandidate.Definition;
        var candidate = MoneyWayNasdaqWindow1130Candidate.Definition;
        Assert.Equal(parent.StrategyId, candidate.StrategyId);
        Assert.NotEqual(parent.Version, candidate.Version);
        Assert.DoesNotContain(candidate, new StrategyDefinitionCatalog().GetAll());
        Assert.Equal(parent.Rules.Count, candidate.Rules.Count);
        foreach (var (oldRule, newRule) in parent.Rules.Zip(candidate.Rules))
        {
            Assert.Equal(oldRule.RuleId, newRule.RuleId);
            if (oldRule.RuleId.Value != "NQ-TIME-002") Assert.Same(oldRule, newRule);
        }
        Assert.Equal(MoneyWayNasdaqSessionCandidate.Evaluators().Select(e => (e.RuleId, e.GetType())),
            MoneyWayNasdaqWindow1130Candidate.Evaluators().Select(e => (e.RuleId, e.GetType())));
    }

    [Theory]
    [InlineData(3, 6)]
    [InlineData(3, 9)]
    [InlineData(10, 30)]
    [InlineData(11, 2)]
    public void CanonicalWindowHasExactBogotaBoundariesAcrossNewYorkDstChanges(int month, int day)
    {
        var date = new DateTimeOffset(2026, month, day, 0, 0, 0, TimeSpan.Zero);
        var times = new[] { (13, 29, 59), (13, 30, 0), (15, 59, 59), (16, 0, 0),
            (16, 15, 0), (16, 29, 59), (16, 30, 0) };
        var closes = times.Select(t => date.AddHours(t.Item1).AddMinutes(t.Item2).AddSeconds(t.Item3)).ToArray();
        // Distinct timeframes let the replay expose adjacent second/minute cutoffs without overlapping a series.
        var series = new[]
        {
            new CandleSeries(LiquidityFixture.Provider, LiquidityFixture.Symbol, M5Fixture.Minute,
                closes.Where(t => t.Second == 0).Select(Minute)),
            new CandleSeries(LiquidityFixture.Provider, LiquidityFixture.Symbol, NasdaqHumanM5TriggerEvent.M5,
                closes.Where(t => t.Second != 0).Select(t => M5Fixture.Five(t))),
        };
        var parent = Observations(MoneyWayNasdaqSessionCandidate.CreateBacktest().Execute(MoneyWayNasdaqSessionCandidate.Definition, series));
        var candidate = Observations(MoneyWayNasdaqWindow1130Candidate.CreateBacktest().Execute(MoneyWayNasdaqWindow1130Candidate.Definition, series));
        var baseline = Observations(new M5CandidateFixture(baseline: true).Run([], series));
        for (var i = 0; i < times.Length; i++)
        {
            AssertWindow(parent[i], date, i >= 1, i < 3, 0);
            AssertWindow(baseline[i], date, i >= 1, i < 3, 0);
            AssertWindow(candidate[i], date, i >= 1, i < 6, 30);
        }
    }

    [Theory]
    [InlineData(NasdaqStrategyPath.Standard, 30)]
    [InlineData(NasdaqStrategyPath.WickfillScenario2, 0)]
    public void IncompleteSetupExpiresOnceAtTheApplicableCutoff(NasdaqStrategyPath path, int cutoffMinute)
    {
        var parent = new M5CandidateFixture(false, path: path, sessionCandidate: true);
        var candidate = new M5CandidateFixture(false, path: path, window1130Candidate: true);
        var series = Extend(candidate);
        var comparison = new[] { (Report: parent.Run(series: series), Cutoff: At(0)),
            (Report: candidate.Run(series: series), Cutoff: At(cutoffMinute)) };
        foreach (var (report, cutoff) in comparison)
        {
            var frame = Frame(report, cutoff);
            Assert.Equal(RuleEvaluationResult.Failed, Result(frame, "NQ-TIME-002"));
            Assert.Equal(cutoff, Window(frame).EndUtc);
            Assert.Null(frame.LifecycleProgression!.ActiveInstance);
            var ended = Assert.Single(Frame(report, At(31)).LifecycleProgression!.Instances);
            Assert.Equal(cutoff, ended.TerminatedAtUtc);
            Assert.Equal(StrategyReplayProgressionTerminationKind.Expired, ended.TerminationKind);
            Assert.Equal(0, report.ReadyCount);
        }
        var priorCandidate = Frame(comparison[1].Report, cutoffMinute == 0 ? At(-5) : At(29));
        Assert.NotNull(priorCandidate.LifecycleProgression!.ActiveInstance);
        Assert.Equal(RuleEvaluationResult.Passed, Result(priorCandidate, "NQ-TIME-002"));
        if (path == NasdaqStrategyPath.WickfillScenario2)
        {
            Assert.Equal(At(0), Window(priorCandidate).EndUtc);
            Assert.Equal(RuleEvaluationResult.Failed, Result(Frame(comparison[1].Report, At(0)), "NQ-WF2-001"));
            Assert.Equal(RuleEvaluationResult.Failed, Result(Frame(comparison[1].Report, At(0)), "NQ-WF2-002"));
        }
    }

    [Fact]
    public void CanonicalStandardAcquiresLateM5FvgAndM1AndRetainsPriorCompletionAtCutoff()
    {
        var candidate = new M5CandidateFixture(window1130Candidate: true);
        var parent = new M5CandidateFixture(sessionCandidate: true);
        var series = Extend(candidate);
        var (inputs, report) = RunLateStandard(candidate, series);
        var (_, parentReport) = RunLateStandard(parent, series, complete: false);

        var prerequisites = new[] { ("NQ-M5-001", 5), ("NQ-FVG-001", 10), ("NQ-FVG-002", 15),
            ("NQ-M1-001", 16), ("NQ-M1-002", 17), ("NQ-M1-003", 18) };
        foreach (var (rule, minute) in prerequisites)
        {
            var candidateFrame = Frame(report, At(minute));
            Assert.Equal(RuleEvaluationResult.Passed, Result(candidateFrame, rule));
            Assert.True(candidateFrame.WorkflowProgression!.RuleEligibility.Single(e => e.RuleId.Value == rule).EstablishesProgression);
            Assert.NotEqual(RuleEvaluationResult.Passed, Result(Frame(parentReport, At(minute)), rule));
            Assert.NotEqual(RuleEvaluationResult.Passed, Result(Frame(report, At(0)), rule));
        }
        Assert.Equal(RuleEvaluationResult.Failed, Result(Frame(report, At(30)), "NQ-TIME-002"));
        var completed = Frame(report, At(31)).LifecycleProgression!.ActiveInstance;
        Assert.NotNull(completed);
        Assert.Null(completed.TerminationKind);
        Assert.Contains(new RuleId("NQ-M1-003"), completed.WorkflowProgression.EstablishedRuleIds);
        Assert.Equal(0, report.ReadyCount);
        Assert.Equal(0, parentReport.ReadyCount);

        var shortSeries = series.Select(s => new CandleSeries(s.ProviderId, s.Symbol, s.Timeframe,
            s.Candles.Where(c => c.CloseTimeUtc <= At(16)))).ToArray();
        var prefix = candidate.Run(inputs.Where(i => i.ObservedAtUtc <= At(16)), shortSeries);
        Assert.Equal(JsonSerializer.Serialize(Observations(prefix)),
            JsonSerializer.Serialize(Observations(report).Where(o => o.AsOfUtc <= At(16))));
        var noBranch = candidate.Run(inputs.Where(i => i is not NasdaqHumanBranchApplicabilityObservation), series);
        var noReview = candidate.Run(inputs.Where(i => i is not NasdaqHumanH4ContextObservation), series);
        var noPreparation = candidate.Run(inputs.Where(i => i is not NasdaqPreparationCompletionObservation), series);
        foreach (var blocked in new[] { noBranch, noReview, noPreparation })
        {
            Assert.Equal(0, blocked.ReadyCount);
            Assert.NotEqual(RuleEvaluationResult.Passed, Result(Frame(blocked, At(18)), "NQ-M1-003"));
        }
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(30, false)]
    public void LateTakeStartsOnlyWithinStandardCandidateWindow(int minute, bool expectedStart)
    {
        var fixture = new M5CandidateFixture(window1130Candidate: true);
        var series = Extend(fixture);
        var at = At(minute);
        var source = series.Single(s => s.Timeframe == NasdaqHumanM5TriggerEvent.M5).Candles.Single(c => c.CloseTimeUtc == at);
        var take = new NasdaqHumanLiquidityTakeObservation(fixture.Take.Reference,
            new NasdaqHumanLiquidityTakeEvent.Documented(source.ProviderId, source.Symbol, "synthetic:late-take", 89,
                at, "synthetic:late-take-source", [source]), fixture.Take.ReferenceEligibleAtUtc, at, "synthetic:late-take-review");
        var inputs = fixture.Inputs.Where(i => i is NasdaqHumanH4ContextObservation or NasdaqPreparationCompletionObservation)
            .Concat([take, new NasdaqHumanRelevantLiquidityTakeObservation(take, at, "synthetic:late-relevance")]).ToArray();
        var report = fixture.Run(inputs, series);
        var frame = Frame(report, at);
        Assert.Equal(expectedStart, frame.LifecycleProgression!.ActiveInstance is not null);
        Assert.Equal(expectedStart, Result(frame, "NQ-LIQ-003") == RuleEvaluationResult.Passed);
        Assert.Equal(expectedStart, Result(frame, "NQ-TIME-002") == RuleEvaluationResult.Passed);
        Assert.Equal(0, report.ReadyCount);
        if (expectedStart)
        {
            // The clock alone allows acquisition. The explicit branch remains independently required.
            Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Result(Frame(report, At(10)), "NQ-M5-001"));
        }
    }

    [Fact]
    public void LateWf2ReviewCannotAdvanceWf2OrRewriteEarlierUnclassifiedFrames()
    {
        var fixture = new M5CandidateFixture(false, path: NasdaqStrategyPath.WickfillScenario2, window1130Candidate: true);
        var series = Extend(fixture);
        var withoutReview = fixture.Inputs.Where(i => i is not NasdaqHumanBranchApplicabilityObservation).ToArray();
        var original = fixture.Inputs.OfType<NasdaqHumanBranchApplicabilityObservation>().Single();
        var delayed = new NasdaqHumanBranchApplicabilityObservation(original.InitialTakeObservation, original.Path,
            original.ReviewEffectiveAtUtc, At(5), "fixture:late-wf2-review");
        var unclassified = fixture.Run(withoutReview, series);
        var reviewed = fixture.Run(withoutReview.Append(delayed), series);
        Assert.Equal(JsonSerializer.Serialize(Observations(unclassified).TakeWhile(o => o.AsOfUtc < At(5))),
            JsonSerializer.Serialize(Observations(reviewed).TakeWhile(o => o.AsOfUtc < At(5))));
        var reviewedFrame = Frame(reviewed, At(5));
        Assert.Equal(At(0), Window(reviewedFrame).EndUtc);
        Assert.Equal(RuleEvaluationResult.Failed, Result(reviewedFrame, "NQ-WF2-001"));
        Assert.Equal(RuleEvaluationResult.Failed, Result(reviewedFrame, "NQ-WF2-002"));
        Assert.Null(reviewedFrame.LifecycleProgression!.ActiveInstance);
        Assert.Equal(At(5), Assert.Single(reviewedFrame.LifecycleProgression.Instances).TerminatedAtUtc);
        Assert.Null(Frame(reviewed, At(10)).LifecycleProgression!.ActiveInstance);
    }

    private static (IStrategyReplayInputObservation[] Inputs, MultiTimeframeStrategyBacktestDiagnosticsReport Report)
        RunLateStandard(M5CandidateFixture fixture, CandleSeries[] series, bool complete = true)
    {
        Candle CandleAt(Timeframe tf, int minute) => series.Single(s => s.Timeframe == tf).Candles.Single(c => c.CloseTimeUtc == At(minute));
        Candle Five(int minute) => CandleAt(NasdaqHumanM5TriggerEvent.M5, minute);
        var trigger = new NasdaqHumanM5TriggerObservation(fixture.Take,
            new NasdaqHumanM5TriggerEvent.Ifvg(Five(5), fixture.Direction, [Five(0)], [Five(0)]),
            At(5), "fixture:late-ifvg");
        var fvg = new NasdaqHumanM5FvgObservation(trigger, new([Five(-5), Five(0), Five(5)]), At(5), "fixture:late-fvg");
        var quality = new NasdaqHumanM5FvgQualityObservation(new(fvg, NasdaqHumanM5FvgQualityDecision.Approved,
            At(5), "Synthetic review."), At(10), "fixture:late-quality");
        var correctiveCandle = CandleAt(M5Fixture.Minute, 16);
        var pullback = new NasdaqHumanM1CorrectiveRetracementObservation(quality, new([correctiveCandle]),
            At(16), "fixture:late-pullback");
        var inputs = fixture.Inputs.Concat([trigger, fvg, quality, pullback]).ToArray();
        var first = fixture.Run(inputs, series);
        if (!complete) return (inputs, first);
        var owner = Assert.IsType<NasdaqHumanM1CorrectiveRetracementRuleFact>(
            Frame(first, At(16)).RuleFacts.SingleOrDefault(f => f.RuleId.Value == "NQ-M1-001")?.Fact);
        var realignment = new NasdaqHumanM1RealignmentObservation(owner,
            new(CandleAt(M5Fixture.Minute, 17), [correctiveCandle], 99, fixture.Direction),
            At(17), "fixture:late-realignment");
        inputs = inputs.Append(realignment).ToArray();
        return (inputs, fixture.Run(inputs, series));
    }

    private static CandleSeries[] Extend(M5CandidateFixture fixture) => fixture.Series.Select(s =>
        new CandleSeries(s.ProviderId, s.Symbol, s.Timeframe, s.Candles.Concat(
            s.Timeframe == M5Fixture.Minute ? new[] { 16, 17, 18, 29, 31 }.Select(m => Minute(At(m)))
                : s.Timeframe == NasdaqHumanM5TriggerEvent.M5
                    ? Enumerable.Range(-1, 8).Select(i => M5Fixture.Five(At(i * 5)))
                    : []))).ToArray();

    private static DateTimeOffset At(int minute) => M5Fixture.At(16).AddMinutes(minute);

    private static Candle Minute(DateTimeOffset close) => new(LiquidityFixture.Provider, LiquidityFixture.Symbol,
        M5Fixture.Minute, close.AddMinutes(-1), close, 100, 110, 90, 100, null);

    private static IReadOnlyList<StrategyReplayContextObservation> Observations(MultiTimeframeStrategyBacktestDiagnosticsReport report) =>
        report.OutcomeRun.StrategyRun.StrategyObservations;

    private static StrategyReplayContextObservation Frame(MultiTimeframeStrategyBacktestDiagnosticsReport report, DateTimeOffset at) =>
        Observations(report).Single(o => o.AsOfUtc == at);

    private static RuleEvaluationResult Result(StrategyReplayContextObservation frame, string rule) =>
        frame.Evaluations.Single(e => e.RuleId.Value == rule).Result;

    private static NasdaqTradingWindowFact Window(StrategyReplayContextObservation frame)
    {
        var start = Assert.IsType<NasdaqTradingWindowFact>(frame.RuleFacts.Single(f => f.RuleId.Value == "NQ-TIME-001").Fact);
        var end = Assert.IsType<NasdaqTradingWindowFact>(frame.RuleFacts.Single(f => f.RuleId.Value == "NQ-TIME-002").Fact);
        Assert.Equal(start, end);
        return end;
    }

    private static void AssertWindow(StrategyReplayContextObservation frame, DateTimeOffset date,
        bool startPassed, bool endPassed, int cutoffMinute)
    {
        var window = Window(frame);
        Assert.Equal(date.AddHours(13).AddMinutes(30), window.StartUtc);
        Assert.Equal(date.AddHours(16).AddMinutes(cutoffMinute), window.EndUtc);
        Assert.Equal(startPassed ? RuleEvaluationResult.Passed : RuleEvaluationResult.Waiting, Result(frame, "NQ-TIME-001"));
        Assert.Equal(endPassed ? RuleEvaluationResult.Passed : RuleEvaluationResult.Failed, Result(frame, "NQ-TIME-002"));
        Assert.Equal(startPassed && endPassed, window.Contains(frame.AsOfUtc));
    }
}

using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.Strategies;
using MoneyWay.Application.Backtesting;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Domain.MarketData;
using F = MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs.NasdaqHumanWickfillScenario2ReversalEvaluatorTests;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

// Synthetic native evidence only. Uses the same multi-timeframe contexts and prior canonical owners as the selector tests.
public sealed partial class NasdaqHumanWickfillScenario2StopLossTests
{
    private static readonly RuleId SlRule = new("NQ-SL-001");
    private static ReplayRuleEvaluationDecision EvaluateSl(StrategyReplayContext c) => new MoneyWayNasdaqHumanStructuralStopLossEvaluator().Evaluate(c);

    [Fact]
    public void RealSelectorPublishesDistinctCanonicalSlFactWithoutStandardAncestryOrGeometry()
    {
        var s = BuildStopSetup(); var stop = Stop(s); var context = Frame(s, null, stop);
        var decision = EvaluateSl(context); Assert.Equal(RuleEvaluationResult.Passed, decision.Result);
        var fact = Assert.IsType<NasdaqHumanWickfillScenario2StopLossRuleFact>(decision.Fact);
        Assert.Same(stop, fact.Selection.Fact); Assert.Equal(116, fact.StopPrice);
        Assert.Equal(115, fact.Selection.Fact.ProtectiveReference.Price);
        Assert.Equal(At(13, 31), fact.EffectiveAtUtc); Assert.Equal(At(13, 31), stop.ObservedAtUtc);
        Assert.Contains("synthetic:initial-sl", decision.EvidenceReference); Assert.Contains("SelectorEvaluated", decision.EvidenceReference);
        var raw = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll()).Execute(LiquidityFixture.Definition, context);
        var next = new AdvanceStrategyReplayLifecycleUseCase(new()).Execute(MoneyWayReplayWorkflowDefinitions.GetAll().Single(),
            new MoneyWayNasdaqReplayLifecyclePolicy(), raw, s.History.Last().LifecycleProgression);
        var gate = next.WorkflowProgression.RuleEligibility.Single(e => e.RuleId == SlRule);
        Assert.True(gate.EstablishesProgression); Assert.Equal("NQ-WF2-002", Assert.Single(gate.PrerequisiteRuleIds).Value);
        Assert.DoesNotContain(raw.RuleFacts, f => f.Fact is NasdaqHumanStructuralStopLossRuleFact);
        foreach (var id in new[] { "NQ-RISK-001", "NQ-TP-001" })
            Assert.NotEqual(RuleEvaluationResult.Passed, raw.Evaluations.Single(e => e.RuleId.Value == id).Result);
        Assert.NotEqual(StrategyVerdict.Ready, new EvaluateStrategyReplayContextOutcomeUseCase().Execute(LiquidityFixture.Definition,
            raw.WithProgressions(next.WorkflowProgression, next.LifecycleProgression)).Verdict);
        var canonical = raw.WithProgressions(next.WorkflowProgression, next.LifecycleProgression);
        var later = FrameAfter14(s, stop).WithPriorObservations(s.History.Append(canonical));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, new MoneyWayNasdaqRiskExposureEvaluator().Evaluate(later).Result);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, new MoneyWayNasdaqHumanTakeProfitEvaluator().Evaluate(later).Result);
    }

    [Fact]
    public void EstablishedWf2WithNoNativeStopRequiresHumanReviewNotFabricatedSuccess()
    {
        var s = BuildStopSetup(); var d = EvaluateSl(Frame(s));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, d.Result); Assert.Null(d.Fact);
        Assert.Contains("StopPrice", d.Reason); Assert.Contains("USTEC", d.Reason);
    }

    [Fact]
    public void IncompleteAndSameFrameWf2CannotEstablishSl()
    {
        var s = BuildStopSetup(); var stop = Stop(s);
        Assert.Equal(RuleEvaluationResult.Waiting, EvaluateSl(Frame(s, At(13, 32), stop)).Result);
        Assert.Equal(RuleEvaluationResult.Waiting, EvaluateSl(Frame(s, At(13, 33), stop)).Result);
    }

    [Fact]
    public void FutureHumanValidationIsNotBackdatedAndOriginalTimesAreNotClamped()
    {
        var s = BuildStopSetup(); var later = Stop(s, observed: At(14, 1));
        var atT = Frame(s, At(14), later); var before = EvaluateSl(atT);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, before.Result);
        var after = EvaluateSl(FrameAfter14(s, later)); Assert.Equal(RuleEvaluationResult.Passed, after.Result);
        Assert.Equal(At(13, 31), Assert.IsType<NasdaqHumanWickfillScenario2StopLossRuleFact>(after.Fact).EffectiveAtUtc);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(EvaluateSl(atT)));
    }

    [Fact]
    public void ConflictingNativeStopsAreNotRanked()
    {
        var s = BuildStopSetup(); var d = EvaluateSl(Frame(s, null, Stop(s), Stop(s, price: 117, source: "synthetic:other-review")));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, d.Result); Assert.Null(d.Fact);
    }

    [Fact]
    public void CurrentTerminalGateOverridesOtherwiseUsableNativeSl()
    {
        var s = BuildStopSetup(); var stop = Stop(s);
        // Existing selector suite supplies the real same-frame opposite-take regression. Here exact cutoff also blocks direct calls.
        var d = EvaluateSl(Frame(s, At(16), stop)); Assert.Equal(RuleEvaluationResult.Failed, d.Result); Assert.Null(d.Fact);
    }

    [Fact]
    public void WorkflowAlternativeCannotUseSameFramePassOrReviveOldAlternativePass()
    {
        var s = BuildStopSetup(); var workflow = MoneyWayReplayWorkflowDefinitions.GetAll().Single();
        var context = Frame(s, null, Stop(s));
        var raw = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll()).Execute(LiquidityFixture.Definition, context);
        var fold = new AdvanceStrategyReplayProgressionUseCase();
        var prior = s.History.Last().WorkflowProgression!;
        var rejected = new StrategyReplayProgressionSnapshot(prior.StrategyId, prior.StrategyVersion, prior.ProviderId, prior.Symbol,
            raw.Step - 1, prior.AsOfUtc, prior.RuleEligibility.Select(e => e.RuleId.Value == "NQ-WF2-002"
                ? new StrategyReplayRuleEligibility(e.RuleId, e.AsOfUtc, e.IsEligible, e.PrerequisiteRuleIds, e.MissingPrerequisiteRuleIds, false) : e), prior.EstablishedRuleIds);
        Assert.False(fold.Execute(workflow, raw, rejected).RuleEligibility.Single(e => e.RuleId == SlRule).EstablishesProgression);
    }

    [Fact]
    public void MissingExactNativeSourceCannotBeReplacedByOtherwiseCompatibleStop()
    {
        var s = BuildStopSetup();
        var unavailable = Stop(s, reference: Reference(sources: [LiquidityFixture.Candle(10, high: 900)]));
        Assert.Equal(RuleEvaluationResult.DataUnavailable, EvaluateSl(Frame(s, null, unavailable)).Result);
        Assert.Equal(RuleEvaluationResult.DataUnavailable, EvaluateSl(Frame(s, null, Stop(s), unavailable)).Result);
    }

    [Fact]
    public void WorkflowValidatesUnknownDuplicateSelfAndAlternativeCycles()
    {
        var definition = LiquidityFixture.Definition;
        StrategyReplayWorkflowDefinition Workflow(params StrategyReplayRulePrerequisite[] p) => new(definition.StrategyId, definition.Version, p);
        Assert.Throws<ArgumentException>(() => Workflow(new StrategyReplayRulePrerequisite(SlRule, [new("NQ-M1-003")], alternativePrerequisiteRuleId: new("NQ-M1-003"))));
        Assert.Throws<ArgumentException>(() => Workflow(new StrategyReplayRulePrerequisite(SlRule, [], alternativePrerequisiteRuleId: SlRule)));
        Assert.Throws<ArgumentException>(() => Workflow(new StrategyReplayRulePrerequisite(SlRule, [], alternativePrerequisiteRuleId: new("NQ-WF2-002")), new(new("NQ-WF2-002"), [SlRule])));
        Assert.Throws<InvalidOperationException>(() => new StrategyReplayWorkflowCatalog([definition],
            [Workflow(new StrategyReplayRulePrerequisite(SlRule, [], alternativePrerequisiteRuleId: new("unknown")))]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("100")]
    [InlineData("101")]
    public void CanonicalMultiTimeframeBinderEmitsSlAndHistoricalSummaryWithIndependentDownstreamGates(string? loss) =>
        BuildSyntheticScenario2RiskReport(loss);

    public static NasdaqMentorSessionReplayReport BuildSyntheticScenario2RiskReport(string? loss)
    {
        var baseFixture = F.Build(); var records = baseFixture.Inputs.Select((o, i) =>
            new NasdaqMentorSessionEvidenceRecord("synthetic:input-" + i, o.ObservedAtUtc, (_, _) => o)).ToList();
        StrategyReplayContextObservation? Start(StrategyReplayContext c) => c.PriorObservations.FirstOrDefault(o =>
            o.LifecycleProgression?.ActiveInstance?.StartedAtUtc == o.AsOfUtc && o.RuleFacts.Any(f => f.RuleId.Value == "NQ-LIQ-003"));
        records.Add(new("synthetic:path", At(13, 31), (c, _) => Start(c) is { } start
            ? new NasdaqHumanBranchApplicabilityObservation(start, NasdaqStrategyPath.WickfillScenario2, At(13, 10), At(13, 10), "synthetic:branch") : null));
        records.Add(new("synthetic:turn", At(13, 32), (c, _) =>
        {
            var start = Start(c); if (start is null) return null;
            c = F.Context(c.AsOfUtc, baseFixture.Inputs).WithPriorObservations(c.PriorObservations);
            var selected = new NasdaqHumanWickfillScenario2ContextObservationSelector().Select(c, LiquidityFixture.Session(At(13)));
            return selected is NasdaqHumanWickfillScenario2ContextSelection.Unique unique
                ? new NasdaqHumanWickfillScenario2PostSweepReversalObservation(NasdaqWickfillScenario2OpeningSweepContext.From(c, unique, start),
                    "synthetic:turn", At(13, 32), At(13, 32), "synthetic:review-turn") : null;
        }));
        records.Add(new("synthetic:stop", At(13, 31), (c, _) =>
        {
            var owner = c.PriorObservations.LastOrDefault(o => o.RuleFacts.Any(f => f.Fact is NasdaqWickfillScenario2PreEntryEligibilityRuleFact)
                && o.WorkflowProgression?.RuleEligibility.Single(e => e.RuleId.Value == "NQ-WF2-002").EstablishesProgression == true);
            return owner is null ? null : new NasdaqHumanWickfillScenario2StopLossObservation(owner, Reference(), 116, At(13, 31), At(13, 31), "synthetic:review-stop");
        }));
        if (loss is not null) records.Add(new("synthetic:risk", At(13, 34), (c, _) =>
        {
            var owner = c.PriorObservations.LastOrDefault(o => o.RuleFacts.Any(f => f.Fact is NasdaqHumanWickfillScenario2StopLossRuleFact)
                && o.WorkflowProgression?.RuleEligibility.Single(e => e.RuleId.Value == "NQ-SL-001").EstablishesProgression == true);
            return owner is null ? null : new NasdaqWickfillScenario2RiskExposureObservation(owner, LiquidityFixture.Provider,
                LiquidityFixture.Symbol, Exposure(loss: decimal.Parse(loss, System.Globalization.CultureInfo.InvariantCulture)),
                At(13, 33), At(13, 34), "synthetic:approved-risk");
        }));
        var input = new NasdaqMentorSessionReplayInput("synthetic-WF2-native-SL", LiquidityFixture.Session(At(13)), new Dictionary<Timeframe, string>(), records);
        var link = new NasdaqMentorSessionReplay(input);
        var sourceContext = FrameAfter14(BuildStopSetup());
        var bounded = sourceContext.AvailableTimeframes.Select(t =>
        { sourceContext.TryGetFrame(t, out var frame); return new CandleSeries(sourceContext.ProviderId, sourceContext.Symbol, t, frame!.AvailableCandles); }).ToArray();
        var definitions = new StrategyDefinitionCatalog().GetAll();
        var runner = new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(MoneyWayReplayRuleEvaluators.GetAll()),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), new(), new(definitions, MoneyWayReplayLifecyclePolicies.GetAll()), new(new()));
        var report = new GenerateCanonicalMultiTimeframeBacktestUseCase(new(runner, new()), new()).ExecuteMentorSession(LiquidityFixture.Definition, bounded, link);
        var last = link.Frames.Last();
        Assert.True(last.Strategy.Evaluations.Single(e => e.RuleId == SlRule).Result == RuleEvaluationResult.Passed,
            string.Join(";", link.Diagnostics.Select(d => d.RecordId + ":" + d.Code)) + "\n" + string.Join(";", last.Strategy.Evaluations.Where(e => e.RuleId.Value.Contains("WF2") || e.RuleId == SlRule).Select(e => e.RuleId.Value + ":" + e.Result + ":" + e.Reason)));
        var summary = NasdaqHistoricalTradeLifecycleSummary.FromFrame(last);
        Assert.Equal(116, summary.Scenario2StopLoss!.StopPrice); Assert.Equal(115, summary.Scenario2StopLoss.ProtectivePrice);
        var persisted = JsonSerializer.Deserialize<NasdaqHistoricalTradeLifecycleSummary>(JsonSerializer.Serialize(summary))!;
        Assert.Equal(summary.Scenario2StopLoss, persisted.Scenario2StopLoss);
        Assert.Equal(At(13, 31), summary.Scenario2StopLoss.EffectiveAtUtc); Assert.Null(summary.Entry); Assert.Null(summary.Exit);
        Assert.Null(summary.RiskRatio); Assert.Null(summary.TakeProfitPrice); Assert.False(summary.CompleteSupervisedTrade);
        Assert.All(link.Frames.Where(f => f.Strategy.AsOfUtc <= At(13, 33)), f => Assert.DoesNotContain(f.Inputs, o => o is NasdaqHumanWickfillScenario2StopLossObservation));
        Assert.Equal(report.OutcomeRun.StrategyRun.StrategyObservations.Last(), last.Strategy);
        Assert.Contains(link.Diagnostics, d => d.RecordId == "synthetic:stop" && d.Code == "Bound");
        Assert.Equal(loss is null ? RuleEvaluationResult.HumanValidationRequired : loss == "100" ? RuleEvaluationResult.Passed : RuleEvaluationResult.Failed,
            last.Strategy.Evaluations.Single(e => e.RuleId.Value == "NQ-RISK-001").Result);
        Assert.Equal(summary.Scenario2RiskExposure, persisted.Scenario2RiskExposure);
        if (loss is not null)
        {
            Assert.Equal(decimal.Parse(loss, System.Globalization.CultureInfo.InvariantCulture), summary.Scenario2RiskExposure!.Exposure.ReportedMaximumLoss);
            Assert.Equal(10000, summary.Scenario2RiskExposure.Exposure.AccountBasisAmount);
            Assert.Equal(At(13, 33), summary.Scenario2RiskExposure.EffectiveAtUtc);
            Assert.Equal(At(13, 34), summary.Scenario2RiskExposure.ObservedAtUtc);
            Assert.Contains(link.Diagnostics, d => d.RecordId == "synthetic:risk" && d.Code == "Bound");
            Assert.All(link.Frames.Where(f => f.Strategy.AsOfUtc < At(13, 34)), f => Assert.DoesNotContain(f.Inputs, o => o is NasdaqWickfillScenario2RiskExposureObservation));
        }
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, last.Strategy.Evaluations.Single(e => e.RuleId.Value == "NQ-TP-001").Result);
        return new(input, [], [], report, link.Frames, link.Diagnostics);
    }
}

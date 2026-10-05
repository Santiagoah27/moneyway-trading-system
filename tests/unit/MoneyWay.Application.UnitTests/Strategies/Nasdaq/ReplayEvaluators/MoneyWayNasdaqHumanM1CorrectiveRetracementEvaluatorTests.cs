using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Capabilities;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayEvaluators;

public sealed class MoneyWayNasdaqHumanM1CorrectiveRetracementEvaluatorTests
{
    private static NasdaqHumanM5FvgQualityObservation Quality(NasdaqHumanH4PermittedDirection direction,
        int fvgMinute = 10, NasdaqHumanM5FvgQualityDecision decision = NasdaqHumanM5FvgQualityDecision.Approved,
        int qualityMinute = 10)
    {
        var take = M5Fixture.Take(direction == NasdaqHumanH4PermittedDirection.Sell);
        var reference = M5Fixture.Five(M5Fixture.At(14));
        var trigger = new NasdaqHumanM5TriggerObservation(take,
            new NasdaqHumanM5TriggerEvent.StructuralChange(M5Fixture.Five(M5Fixture.At(14, 10)), direction,
                [reference], [reference], 100), M5Fixture.At(14, 10), "trigger:source");
        var close = M5Fixture.At(14, fvgMinute);
        var fvg = new NasdaqHumanM5FvgObservation(trigger,
            new([M5Fixture.Five(close.AddMinutes(-10)), M5Fixture.Five(close.AddMinutes(-5)), M5Fixture.Five(close)]),
            close, "fvg:source");
        return new(new(fvg, decision, M5Fixture.At(14, qualityMinute), "Source-backed quality review."),
            M5Fixture.At(14, Math.Max(15, qualityMinute)), "quality:source");
    }

    private static NasdaqHumanM1CorrectiveRetracementObservation Pullback(NasdaqHumanM5FvgQualityObservation quality,
        int minute = 20, int observed = 20, string source = "review:pullback", bool unavailable = false)
    {
        var candle = unavailable
            ? new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, M5Fixture.Minute,
                M5Fixture.At(14, minute - 1).AddSeconds(1), M5Fixture.At(14, minute), 100, 110, 90, 100, null)
            : M5Fixture.Series().Single(s => s.Timeframe == M5Fixture.Minute).Candles
                .Single(c => c.CloseTimeUtc == M5Fixture.At(14, minute));
        return new(quality, new([candle]), M5Fixture.At(14, observed), source);
    }

    private static IStrategyReplayInputObservation[] Inputs(NasdaqHumanM5FvgQualityObservation quality,
        params IStrategyReplayInputObservation[] extra) =>
        M5Fixture.Inputs(quality.Fact.Fvg.Direction, quality.Fact.Fvg.Trigger.DecisiveTake)
            .Concat([quality.Fact.Fvg.Trigger, quality.Fact.Fvg, quality]).Concat(extra).ToArray();

    private static MultiTimeframeStrategyBacktestRun Run(IStrategyReplayInputObservation[] inputs)
    {
        var definitions = new StrategyDefinitionCatalog().GetAll();
        return new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(MoneyWayReplayRuleEvaluators.GetAll()),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), new(),
            new(definitions, MoneyWayReplayLifecyclePolicies.GetAll()), new(new()))
            .Execute(LiquidityFixture.Definition, M5Fixture.Series(), inputs);
    }

    private static StrategyReplayContextObservation At(MultiTimeframeStrategyBacktestRun run, int minute) =>
        run.StrategyObservations.Single(o => o.AsOfUtc == M5Fixture.At(14, minute));
    private static RuleEvaluationResult Result(StrategyReplayContextObservation observation) =>
        observation.Evaluations.Single(e => e.RuleId.Value == "NQ-M1-001").Result;

    [Fact]
    public void RegistryCapabilityAndPrerequisiteAreCanonical()
    {
        var registry = MoneyWayReplayRuleEvaluators.GetAll();
        Assert.Single(registry, e => e.RuleId.Value == "NQ-M1-001" && e is MoneyWayNasdaqHumanM1CorrectiveRetracementEvaluator);
        var report = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), registry,
            MoneyWayReplayEvaluationCapabilityDeclarations.GetAll()).Find(LiquidityFixture.Definition.StrategyId, LiquidityFixture.Definition.Version)!;
        Assert.Equal((32, 14, 14, 1, false), (report.TotalRuleCount, report.RequiredRuleCount,
            report.ImplementedCount, report.RequiredEvaluatorGapCount, report.HasFullRequiredEvaluatorRegistration));
        Assert.Equal("NQ-RISK-001", report.Rules.Where(r => r.IsRequired && r.CapabilityStatus != ReplayRuleEvaluationCapabilityStatus.Implemented)
            .OrderBy(r => r.Sequence).First().RuleId.Value);
        var quality = Quality(NasdaqHumanH4PermittedDirection.Buy);
        var run = Run(Inputs(quality, Pullback(quality)));
        Assert.Equal(RuleEvaluationResult.Waiting, Result(At(run, 15)));
        Assert.Equal(RuleEvaluationResult.Waiting, Result(At(run, 20)));
    }

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell)]
    public void UniqueHumanPullbackPassesOnlyCorrectiveStage(NasdaqHumanH4PermittedDirection direction)
    {
        var quality = Quality(direction);
        var pullback = Pullback(quality);
        var run = Run(Inputs(quality, pullback));
        var stage = At(run, 25);
        Assert.Equal(RuleEvaluationResult.Passed, Result(stage));
        var fact = Assert.IsType<NasdaqHumanM1CorrectiveRetracementRuleFact>(
            stage.RuleFacts.Single(f => f.RuleId.Value == "NQ-M1-001").Fact);
        Assert.Same(pullback, fact.Selection.Fact);
        Assert.Equal(direction, fact.Selection.Fact.SetupDirection);
        Assert.Equal(M5Fixture.At(14, 20), fact.Selection.Fact.PullbackEffectiveAtUtc);
        Assert.Equal(stage.AsOfUtc, stage.Evaluations.Single(e => e.RuleId.Value == "NQ-M1-001").EvaluatedAtUtc);
        Assert.Contains("review:pullback", stage.Evaluations.Single(e => e.RuleId.Value == "NQ-M1-001").EvidenceReference);
        Assert.Contains(new RuleId("NQ-M1-001"), At(run, 30).WorkflowProgression!.EstablishedRuleIds);
        Assert.Equal([new RuleId("NQ-M1-001")], MoneyWayReplayWorkflowDefinitions.GetAll().Single()
            .GetPrerequisiteRuleIds(new RuleId("NQ-M1-002")));
        Assert.NotEqual(RuleEvaluationResult.Passed, stage.Evaluations.Single(e => e.RuleId.Value == "NQ-M1-002").Result);
        Assert.NotEqual(RuleEvaluationResult.Passed, stage.Evaluations.Single(e => e.RuleId.Value == "NQ-M1-003").Result);
    }

    [Fact]
    public void MissingConflictAndUnavailableSourcesRemainDistinct()
    {
        var quality = Quality(NasdaqHumanH4PermittedDirection.Buy);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Result(At(Run(Inputs(quality)), 25)));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired,
            Result(At(Run(Inputs(quality, Pullback(quality), Pullback(quality, 30, 30))), 30)));
        var unavailable = At(Run(Inputs(quality, Pullback(quality, unavailable: true))), 25);
        Assert.Equal(RuleEvaluationResult.DataUnavailable, Result(unavailable));
        Assert.Contains("review:pullback", unavailable.Evaluations.Single(e => e.RuleId.Value == "NQ-M1-001").EvidenceReference);
    }

    [Fact]
    public void RejectedOrWrongFvgCannotSupplyPullback()
    {
        var rejected = Quality(NasdaqHumanH4PermittedDirection.Buy,
            decision: NasdaqHumanM5FvgQualityDecision.Rejected);
        Assert.Throws<ArgumentException>(() => Pullback(rejected));
        var approved = Quality(NasdaqHumanH4PermittedDirection.Buy);
        var other = Quality(NasdaqHumanH4PermittedDirection.Buy, fvgMinute: 15, qualityMinute: 15);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired,
            Result(At(Run(Inputs(approved, Pullback(other))), 25)));
    }

    [Fact]
    public void SameEffectiveTimestampDoesNotRequireLaterOneMinuteCandle()
    {
        var quality = Quality(NasdaqHumanH4PermittedDirection.Buy, qualityMinute: 20);
        var pullback = Pullback(quality);
        Assert.Equal(quality.EffectiveAtUtc, pullback.PullbackEffectiveAtUtc);
        Assert.Equal(RuleEvaluationResult.Passed, Result(At(Run(Inputs(quality, pullback)), 25)));
    }

    [Fact]
    public void RejectedCandidateCannotOwnLaterApprovedCandidatesPullback()
    {
        var rejected = Quality(NasdaqHumanH4PermittedDirection.Buy,
            decision: NasdaqHumanM5FvgQualityDecision.Rejected);
        var approved = Quality(NasdaqHumanH4PermittedDirection.Buy, fvgMinute: 25, qualityMinute: 25);
        var pullback = Pullback(approved, 30, 30);
        var run = Run(Inputs(rejected, approved.Fact.Fvg, approved, pullback));
        Assert.Equal(RuleEvaluationResult.Waiting, Result(At(run, 25)));
        var passed = At(run, 35);
        Assert.Equal(RuleEvaluationResult.Passed, Result(passed));
        var fact = Assert.IsType<NasdaqHumanM1CorrectiveRetracementRuleFact>(
            passed.RuleFacts.Single(f => f.RuleId.Value == "NQ-M1-001").Fact);
        Assert.Same(approved.Fact.Fvg, fact.ApprovedQuality.Selection.Fact.Fact.Fvg);
        Assert.Contains(run.StrategyObservations.SelectMany(o => o.RuleFacts).Select(f => f.Fact)
            .OfType<NasdaqHumanM5FvgQualityRuleFact>(), f =>
                f.Selection.Fact.Fact.Decision == NasdaqHumanM5FvgQualityDecision.Rejected);
    }

    [Fact]
    public void FutureAnnotationCannotRewriteEarlierReplayFrame()
    {
        var quality = Quality(NasdaqHumanH4PermittedDirection.Buy);
        var future = Pullback(quality, 20, 30);
        var baseline = Run(Inputs(quality));
        var expanded = Run(Inputs(quality, future));
        Assert.Equal(Result(At(baseline, 25)), Result(At(expanded, 25)));
        Assert.Equal(At(baseline, 25).Evaluations.Single(e => e.RuleId.Value == "NQ-M1-001").EvidenceReference,
            At(expanded, 25).Evaluations.Single(e => e.RuleId.Value == "NQ-M1-001").EvidenceReference);
        Assert.Equal(RuleEvaluationResult.Passed, Result(At(expanded, 30)));
    }

    [Fact]
    public void TerminalSessionAndTimeCutoffBlockProgression()
    {
        var quality = Quality(NasdaqHumanH4PermittedDirection.Buy);
        var pullback = Pullback(quality);
        var invalid = M5Fixture.Take(true, effective: M5Fixture.At(14, 30), observed: M5Fixture.At(14, 30));
        var terminal = Run(Inputs(quality, pullback, invalid,
            new NasdaqHumanRelevantLiquidityTakeObservation(invalid, invalid.ObservedAtUtc,
                "terminal event", quality.Fact.Fvg.Trigger.DecisiveTake)));
        Assert.NotEqual(RuleEvaluationResult.Passed, Result(At(terminal, 35)));
        var ordinary = Run(Inputs(quality, pullback));
        var cutoff = ordinary.StrategyObservations.Single(o => o.AsOfUtc == M5Fixture.At(38, 5));
        Assert.NotEqual(RuleEvaluationResult.Passed, Result(cutoff));
    }
}

using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Capabilities;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanM1RealignmentTests
{
    private static readonly NasdaqHumanM1RealignmentObservationSelector Selector = new();

    private static Candle Minute(int close) => M5Fixture.Series().Single(s => s.Timeframe == M5Fixture.Minute)
        .Candles.Single(c => c.CloseTimeUtc == M5Fixture.At(14, close));

    private static IStrategyReplayInputObservation[] Inputs(NasdaqHumanH4PermittedDirection direction,
        params IStrategyReplayInputObservation[] extra)
    {
        var take = M5Fixture.Take(direction == NasdaqHumanH4PermittedDirection.Sell);
        var reference = M5Fixture.Five(M5Fixture.At(14));
        var trigger = new NasdaqHumanM5TriggerObservation(take,
            new NasdaqHumanM5TriggerEvent.StructuralChange(M5Fixture.Five(M5Fixture.At(14, 10)), direction,
                [reference], [reference], 100), M5Fixture.At(14, 10), "trigger:source");
        var fvg = new NasdaqHumanM5FvgObservation(trigger,
            new([M5Fixture.Five(M5Fixture.At(14)), M5Fixture.Five(M5Fixture.At(14, 5)),
                M5Fixture.Five(M5Fixture.At(14, 10))]), M5Fixture.At(14, 10), "fvg:source");
        var quality = new NasdaqHumanM5FvgQualityObservation(new(fvg,
            NasdaqHumanM5FvgQualityDecision.Approved, M5Fixture.At(14, 10), "Approved source review."),
            M5Fixture.At(14, 15), "quality:source");
        var pullback = new NasdaqHumanM1CorrectiveRetracementObservation(quality,
            new([Minute(20)]), M5Fixture.At(14, 20), "review:pullback");
        return M5Fixture.Inputs(direction, take).Concat([trigger, fvg, quality, pullback]).Concat(extra).ToArray();
    }

    private static MultiTimeframeStrategyBacktestRun Run(IStrategyReplayInputObservation[] inputs)
    {
        var definitions = new StrategyDefinitionCatalog().GetAll();
        return new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(MoneyWayReplayRuleEvaluators.GetAll()),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), new(),
            new(definitions, MoneyWayReplayLifecyclePolicies.GetAll()), new(new()))
            .Execute(LiquidityFixture.Definition, M5Fixture.Series(), inputs);
    }

    private static NasdaqHumanM1CorrectiveRetracementRuleFact PullbackFact(NasdaqHumanH4PermittedDirection direction) =>
        Run(Inputs(direction)).StrategyObservations.SelectMany(o => o.RuleFacts)
            .Where(f => f.RuleId.Value == "NQ-M1-001").Select(f => f.Fact)
            .OfType<NasdaqHumanM1CorrectiveRetracementRuleFact>().First();

    private static (StrategyReplayContext Context, NasdaqHumanM1CorrectiveRetracementRuleFact Fact) Frame(
        NasdaqHumanH4PermittedDirection direction, DateTimeOffset time, params IStrategyReplayInputObservation[] extra)
    {
        var inputs = Inputs(direction, extra);
        var history = Run(inputs).StrategyObservations.Where(o => o.AsOfUtc < time).ToArray();
        var fact = history.SelectMany(o => o.RuleFacts).Where(f => f.RuleId.Value == "NQ-M1-001")
            .Select(f => f.Fact).OfType<NasdaqHumanM1CorrectiveRetracementRuleFact>().Last();
        return (M5Fixture.Context(time, inputs).WithPriorObservations(history), fact);
    }

    private static NasdaqHumanM1RealignmentObservation Realignment(NasdaqHumanM1CorrectiveRetracementRuleFact pullback,
        int close = 20, int observed = 20, string source = "review:realignment", bool sameCloseOrder = true,
        Candle? swingSource = null, decimal? level = null) =>
        new(pullback, new(Minute(close), [swingSource ?? Minute(20)],
            level ?? (pullback.Selection.Fact.SetupDirection == NasdaqHumanH4PermittedDirection.Buy ? 99 : 101),
            pullback.Selection.Fact.SetupDirection), M5Fixture.At(14, observed), source, sameCloseOrder);

    private static RuleEvaluationResult Result(MultiTimeframeStrategyBacktestRun run, DateTimeOffset time) =>
        run.StrategyObservations.Single(o => o.AsOfUtc == time).Evaluations
            .Single(e => e.RuleId.Value == "NQ-M1-002").Result;

    [Fact]
    public void CanonicalRealignmentDerivesOnlyBoundedPreEntryEligibility()
    {
        var evaluators = MoneyWayReplayRuleEvaluators.GetAll();
        Assert.Single(evaluators, e => e.RuleId.Value == "NQ-M1-003"
            && e is MoneyWayNasdaqPreEntryEligibilityEvaluator);
        var missing = Run(Inputs(NasdaqHumanH4PermittedDirection.Buy));
        Assert.Equal(RuleEvaluationResult.Waiting, missing.StrategyObservations
            .Single(o => o.AsOfUtc == M5Fixture.At(14, 30)).Evaluations
            .Single(e => e.RuleId.Value == "NQ-M1-003").Result);

        var pullback = PullbackFact(NasdaqHumanH4PermittedDirection.Buy);
        var realignment = Realignment(pullback);
        var run = Run(Inputs(NasdaqHumanH4PermittedDirection.Buy, realignment));
        var established = run.StrategyObservations.First(o => o.WorkflowProgression?.RuleEligibility
            .SingleOrDefault(e => e.RuleId.Value == "NQ-M1-003")?.EstablishesProgression == true);
        var eligibility = Assert.IsType<NasdaqPreEntryEligibilityRuleFact>(established.RuleFacts
            .Single(f => f.RuleId.Value == "NQ-M1-003").Fact);
        Assert.Same(realignment, eligibility.Realignment.Selection.Fact);
        Assert.Same(run.StrategyObservations.TakeWhile(o => o != established).SelectMany(o => o.RuleFacts)
            .Where(f => f.RuleId.Value == "NQ-M1-002").Select(f => f.Fact)
            .OfType<NasdaqHumanM1RealignmentRuleFact>().Last(), eligibility.Realignment);
        Assert.Equal(realignment.Session, eligibility.Session);
        Assert.Equal(realignment.SetupDirection, eligibility.Direction);
        Assert.Equal(realignment.RealignmentEffectiveAtUtc, eligibility.EligibilityEffectiveAtUtc);
        Assert.NotEqual(established.AsOfUtc, eligibility.EligibilityEffectiveAtUtc);
        Assert.Equal(established.AsOfUtc, established.Evaluations
            .Single(e => e.RuleId.Value == "NQ-M1-003").EvaluatedAtUtc);
        Assert.Contains("review:realignment", established.Evaluations
            .Single(e => e.RuleId.Value == "NQ-M1-003").EvidenceReference);
        Assert.Equal([new RuleId("NQ-M1-003")], MoneyWayReplayWorkflowDefinitions.GetAll().Single()
            .GetPrerequisiteRuleIds(new RuleId("NQ-SL-001")));
        Assert.NotEqual(RuleEvaluationResult.Passed, established.Evaluations.Single(e => e.RuleId.Value == "NQ-SL-001").Result);
        Assert.DoesNotContain(established.RuleFacts, f => f.RuleId.Value == "NQ-SL-001");

        var report = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), evaluators,
            MoneyWayReplayEvaluationCapabilityDeclarations.GetAll())
            .Find(LiquidityFixture.Definition.StrategyId, LiquidityFixture.Definition.Version)!;
        Assert.Equal((32, 14, 16, 0, true), (report.TotalRuleCount, report.RequiredRuleCount,
            report.ImplementedCount, report.RequiredEvaluatorGapCount, report.HasFullRequiredEvaluatorRegistration));
    }

    [Fact]
    public void FutureRealignmentAndTerminalSessionCannotEstablishPreEntryEligibility()
    {
        var pullback = PullbackFact(NasdaqHumanH4PermittedDirection.Buy);
        var baseline = Run(Inputs(NasdaqHumanH4PermittedDirection.Buy));
        var future = Run(Inputs(NasdaqHumanH4PermittedDirection.Buy, Realignment(pullback, observed: 60)));
        var atHistoricalFrame = M5Fixture.At(14, 30);
        Assert.Equal(baseline.StrategyObservations.Single(o => o.AsOfUtc == atHistoricalFrame).Evaluations
            .Single(e => e.RuleId.Value == "NQ-M1-003").Result,
            future.StrategyObservations.Single(o => o.AsOfUtc == atHistoricalFrame).Evaluations
                .Single(e => e.RuleId.Value == "NQ-M1-003").Result);
        Assert.DoesNotContain(future.StrategyObservations.Where(o => o.AsOfUtc <= atHistoricalFrame)
            .SelectMany(o => o.RuleFacts), f => f.RuleId.Value == "NQ-M1-003");

        var invalid = M5Fixture.Take(true, effective: atHistoricalFrame, observed: atHistoricalFrame);
        var terminal = Run(Inputs(NasdaqHumanH4PermittedDirection.Buy, Realignment(pullback), invalid,
            new NasdaqHumanRelevantLiquidityTakeObservation(invalid, invalid.ObservedAtUtc,
                "terminal event", pullback.Selection.Fact.ApprovedQuality.Fact.Fvg.Trigger.DecisiveTake)));
        Assert.DoesNotContain(terminal.StrategyObservations.Where(o => o.AsOfUtc >= atHistoricalFrame)
            .SelectMany(o => o.RuleFacts), f => f.RuleId.Value == "NQ-M1-003");
        Assert.DoesNotContain(terminal.StrategyObservations.Where(o => o.AsOfUtc >= M5Fixture.At(16))
            .SelectMany(o => o.Evaluations), e => e.RuleId.Value == "NQ-M1-003"
                && e.Result == RuleEvaluationResult.Passed);
    }

    [Fact]
    public void EvaluatorRegistryCapabilityAndCanonicalPrerequisite()
    {
        var evaluators = MoneyWayReplayRuleEvaluators.GetAll();
        Assert.Single(evaluators, e => e.RuleId.Value == "NQ-M1-002" && e is MoneyWayNasdaqHumanM1RealignmentEvaluator);
        var report = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), evaluators,
            MoneyWayReplayEvaluationCapabilityDeclarations.GetAll())
            .Find(LiquidityFixture.Definition.StrategyId, LiquidityFixture.Definition.Version)!;
        Assert.Equal((32, 14, 16, 0, true), (report.TotalRuleCount, report.RequiredRuleCount,
            report.ImplementedCount, report.RequiredEvaluatorGapCount, report.HasFullRequiredEvaluatorRegistration));
        Assert.DoesNotContain(report.Rules, r => r.IsRequired && r.CapabilityStatus != ReplayRuleEvaluationCapabilityStatus.Implemented);
        Assert.Equal(RuleEvaluationResult.Waiting,
            Result(Run(Inputs(NasdaqHumanH4PermittedDirection.Buy)), M5Fixture.At(14, 20)));
    }

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell)]
    public void UniqueEvidencePassesOnlyRealignmentStage(NasdaqHumanH4PermittedDirection direction)
    {
        var pullback = PullbackFact(direction);
        var evidence = Realignment(pullback);
        var run = Run(Inputs(direction, evidence));
        var stage = run.StrategyObservations.Single(o => o.AsOfUtc == M5Fixture.At(14, 30));
        Assert.Equal(RuleEvaluationResult.Passed, Result(run, stage.AsOfUtc));
        var fact = Assert.IsType<NasdaqHumanM1RealignmentRuleFact>(
            stage.RuleFacts.Single(f => f.RuleId.Value == "NQ-M1-002").Fact);
        Assert.Same(evidence, fact.Selection.Fact);
        Assert.Equal(direction, fact.Selection.Fact.SetupDirection);
        Assert.Equal(evidence.PullbackEffectiveAtUtc, fact.Selection.Fact.RealignmentEffectiveAtUtc);
        var evaluation = stage.Evaluations.Single(e => e.RuleId.Value == "NQ-M1-002");
        Assert.Equal(stage.AsOfUtc, evaluation.EvaluatedAtUtc);
        Assert.Contains("review:realignment", evaluation.EvidenceReference);
        Assert.Contains("PullbackBeforeRealignmentAtSameClose", evaluation.EvidenceReference);
        Assert.Contains(new MoneyWay.Domain.Strategies.RuleId("NQ-M1-002"), stage.WorkflowProgression!.EstablishedRuleIds);
        Assert.Equal([new MoneyWay.Domain.Strategies.RuleId("NQ-M1-002")], MoneyWayReplayWorkflowDefinitions.GetAll().Single()
            .GetPrerequisiteRuleIds(new MoneyWay.Domain.Strategies.RuleId("NQ-M1-003")));
        Assert.NotEqual(RuleEvaluationResult.Passed, stage.Evaluations.Single(e => e.RuleId.Value == "NQ-M1-003").Result);
    }

    [Fact]
    public void MissingConflictUnavailableAndLaterConfirmationMapSeparately()
    {
        var pullback = PullbackFact(NasdaqHumanH4PermittedDirection.Buy);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired,
            Result(Run(Inputs(NasdaqHumanH4PermittedDirection.Buy)), M5Fixture.At(14, 30)));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired,
            Result(Run(Inputs(NasdaqHumanH4PermittedDirection.Buy,
                Realignment(pullback), Realignment(pullback, level: 98))), M5Fixture.At(14, 30)));
        var absent = new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, M5Fixture.Minute,
            M5Fixture.At(14, 19).AddSeconds(1), M5Fixture.At(14, 20), 100, 110, 90, 100, null);
        var unavailable = Run(Inputs(NasdaqHumanH4PermittedDirection.Buy, Realignment(pullback, swingSource: absent)));
        Assert.Equal(RuleEvaluationResult.DataUnavailable, Result(unavailable, M5Fixture.At(14, 30)));
        Assert.Equal(RuleEvaluationResult.Passed,
            Result(Run(Inputs(NasdaqHumanH4PermittedDirection.Buy,
                Realignment(pullback, 30, 30, sameCloseOrder: false))), M5Fixture.At(14, 30)));
    }

    [Fact]
    public void WrongPullbackCannotPassAndCompatibleInputOrderPreservesEvidence()
    {
        var buy = PullbackFact(NasdaqHumanH4PermittedDirection.Buy);
        var sell = PullbackFact(NasdaqHumanH4PermittedDirection.Sell);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired,
            Result(Run(Inputs(NasdaqHumanH4PermittedDirection.Buy, Realignment(sell))), M5Fixture.At(14, 30)));
        var first = Realignment(buy);
        var second = Realignment(buy, source: "second reviewer");
        var one = Run(Inputs(NasdaqHumanH4PermittedDirection.Buy, second, first));
        var two = Run(Inputs(NasdaqHumanH4PermittedDirection.Buy, first, second));
        var time = M5Fixture.At(14, 30);
        Assert.Equal(RuleEvaluationResult.Passed, Result(one, time));
        Assert.Equal(one.StrategyObservations.Single(o => o.AsOfUtc == time).Evaluations
            .Single(e => e.RuleId.Value == "NQ-M1-002").EvidenceReference,
            two.StrategyObservations.Single(o => o.AsOfUtc == time).Evaluations
                .Single(e => e.RuleId.Value == "NQ-M1-002").EvidenceReference);
    }

    [Fact]
    public void FutureHumanObservationAndTerminalCutoffCannotBackfillOrRevive()
    {
        var pullback = PullbackFact(NasdaqHumanH4PermittedDirection.Buy);
        var future = Realignment(pullback, observed: 60);
        var baseline = Run(Inputs(NasdaqHumanH4PermittedDirection.Buy));
        var expanded = Run(Inputs(NasdaqHumanH4PermittedDirection.Buy, future));
        Assert.Equal(Result(baseline, M5Fixture.At(14, 30)), Result(expanded, M5Fixture.At(14, 30)));
        Assert.Equal(RuleEvaluationResult.Passed, Result(expanded, M5Fixture.At(15)));
        Assert.NotEqual(RuleEvaluationResult.Passed, Result(expanded, M5Fixture.At(38, 5)));
        var invalid = M5Fixture.Take(true, effective: M5Fixture.At(14, 30), observed: M5Fixture.At(14, 30));
        var terminal = Run(Inputs(NasdaqHumanH4PermittedDirection.Buy, Realignment(pullback), invalid,
            new NasdaqHumanRelevantLiquidityTakeObservation(invalid, invalid.ObservedAtUtc,
                "terminal event", pullback.Selection.Fact.ApprovedQuality.Fact.Fvg.Trigger.DecisiveTake)));
        Assert.NotEqual(RuleEvaluationResult.Passed, Result(terminal, M5Fixture.At(15)));
    }

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell)]
    public void ExactHumanRealignmentIsUniqueAndDirectionComesFromSetup(NasdaqHumanH4PermittedDirection direction)
    {
        var original = PullbackFact(direction);
        var evidence = Realignment(original);
        var frame = Frame(direction, M5Fixture.At(14, 30), evidence);
        var unique = Assert.IsType<NasdaqHumanM1RealignmentSelection.Unique>(Selector.Select(frame.Context, frame.Fact));
        Assert.Same(evidence, unique.Fact);
        Assert.Equal(direction, unique.Fact.SetupDirection);
        Assert.Equal(M5Fixture.At(14, 20), unique.Fact.RealignmentEffectiveAtUtc);
        Assert.Equal(original.ApprovedQuality.Candidate.Selection.Fact.Event.EffectiveAtUtc,
            unique.Fact.Pullback.ApprovedQuality.Candidate.Selection.Fact.Event.EffectiveAtUtc);
        Assert.IsType<NasdaqHumanM1RealignmentSelection.Missing>(
            Selector.Select(Frame(direction, M5Fixture.At(14, 30)).Context, frame.Fact));
    }

    [Fact]
    public void SameCloseRequiresExplicitHumanOrderAndEarlierConfirmationIsInvalid()
    {
        var pullback = PullbackFact(NasdaqHumanH4PermittedDirection.Buy);
        Assert.Throws<ArgumentException>(() => Realignment(pullback, sameCloseOrder: false));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM1RealignmentObservation(pullback,
            new(M5Fixture.Series().Single(s => s.Timeframe == M5Fixture.Minute).Candles
                .Single(c => c.CloseTimeUtc == M5Fixture.At(14, 15)),
                [M5Fixture.Series().Single(s => s.Timeframe == M5Fixture.Minute).Candles
                    .Single(c => c.CloseTimeUtc == M5Fixture.At(14, 15))], 100,
                NasdaqHumanH4PermittedDirection.Buy), M5Fixture.At(14, 20), "review:early"));
        var later = Realignment(pullback, 30, 30, sameCloseOrder: false);
        var frame = Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(14, 30), later);
        Assert.IsType<NasdaqHumanM1RealignmentSelection.Unique>(Selector.Select(frame.Context, frame.Fact));
        Assert.False(Realignment(pullback, 30, 30).PullbackBeforeRealignmentAtSameClose);
    }

    [Fact]
    public void CompatibleSupportAndConflictingSelectionAreInputOrderIndependent()
    {
        var pullback = PullbackFact(NasdaqHumanH4PermittedDirection.Buy);
        var a = Realignment(pullback);
        var b = Realignment(pullback, source: "second reviewer");
        var first = Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(14, 30), b, a);
        var unique = Assert.IsType<NasdaqHumanM1RealignmentSelection.Unique>(Selector.Select(first.Context, first.Fact));
        Assert.Equal(2, unique.SupportingObservations.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanM1RealignmentObservation>)unique.SupportingObservations).Clear());
        var other = Realignment(pullback, level: 98, source: "different swing");
        var x = Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(14, 30), other, a);
        var y = Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(14, 30), a, other);
        var conflict = Assert.IsType<NasdaqHumanM1RealignmentSelection.Conflict>(Selector.Select(x.Context, x.Fact));
        Assert.Equal(2, conflict.Alternatives.Count);
        Assert.Equal(conflict.Alternatives,
            Assert.IsType<NasdaqHumanM1RealignmentSelection.Conflict>(Selector.Select(y.Context, y.Fact)).Alternatives);
    }

    [Fact]
    public void WrongPullbackAndWrongDirectionCannotSupplyEvidence()
    {
        var pullback = PullbackFact(NasdaqHumanH4PermittedDirection.Buy);
        var other = PullbackFact(NasdaqHumanH4PermittedDirection.Sell);
        var frame = Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(14, 30), Realignment(other));
        Assert.IsType<NasdaqHumanM1RealignmentSelection.Missing>(Selector.Select(frame.Context, frame.Fact));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM1RealignmentObservation(pullback,
            new(Minute(20), [Minute(20)], 100, NasdaqHumanH4PermittedDirection.Sell),
            M5Fixture.At(14, 20), "wrong direction", true));
    }

    [Fact]
    public void UnavailableSourceIsPreservedAndFutureObservationIsInvisible()
    {
        var pullback = PullbackFact(NasdaqHumanH4PermittedDirection.Buy);
        var absent = new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, M5Fixture.Minute,
            M5Fixture.At(14, 19).AddSeconds(1), M5Fixture.At(14, 20), 100, 110, 90, 100, null);
        var unavailable = Realignment(pullback, swingSource: absent);
        var frame = Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(14, 30), unavailable);
        Assert.Same(unavailable, Assert.Single(Assert.IsType<NasdaqHumanM1RealignmentSelection.Missing>(
            Selector.Select(frame.Context, frame.Fact)).UnavailableSourceObservations));
        var late = Realignment(pullback, observed: 60);
        var early = Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(14, 30), late);
        Assert.IsType<NasdaqHumanM1RealignmentSelection.Missing>(Selector.Select(early.Context, early.Fact));
        Assert.IsType<NasdaqHumanM1RealignmentSelection.Unique>(Selector.Select(
            Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), late).Context, frame.Fact));
    }

    [Fact]
    public void WrongTimeframeAndCutoffCannotSelectRealignment()
    {
        var pullback = PullbackFact(NasdaqHumanH4PermittedDirection.Buy);
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM1RealignmentEvent(
            M5Fixture.Five(M5Fixture.At(14, 20)), [Minute(20)], 100, NasdaqHumanH4PermittedDirection.Buy));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM1RealignmentEvent(
            Minute(20), [M5Fixture.Five(M5Fixture.At(14, 20))], 100, NasdaqHumanH4PermittedDirection.Buy));
        var sources = new List<Candle> { Minute(20) };
        var marketEvent = new NasdaqHumanM1RealignmentEvent(Minute(20), sources, 100, NasdaqHumanH4PermittedDirection.Buy);
        sources.Clear();
        Assert.Single(marketEvent.CorrectiveSwingCandles);
        Assert.Throws<NotSupportedException>(() => ((IList<Candle>)marketEvent.CorrectiveSwingCandles).Clear());
        var frame = Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(38, 5), Realignment(pullback));
        Assert.IsType<NasdaqHumanM1RealignmentSelection.Missing>(Selector.Select(frame.Context, frame.Fact));
    }

    [Fact]
    public void TerminalSessionCannotReviveRealignment()
    {
        var pullback = PullbackFact(NasdaqHumanH4PermittedDirection.Buy);
        var invalid = M5Fixture.Take(true, effective: M5Fixture.At(14, 30), observed: M5Fixture.At(14, 30));
        var terminal = Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15),
            Realignment(pullback), invalid,
            new NasdaqHumanRelevantLiquidityTakeObservation(invalid, invalid.ObservedAtUtc,
                "terminal event", pullback.Selection.Fact.ApprovedQuality.Fact.Fvg.Trigger.DecisiveTake));
        Assert.Contains(terminal.Context.PriorObservations.SelectMany(o => o.RuleFacts).Select(f => f.Fact)
            .OfType<NasdaqLiquidityTakeRuleFact>(), f => f.IsSessionInvalidated);
        Assert.IsType<NasdaqHumanM1RealignmentSelection.Missing>(Selector.Select(terminal.Context, terminal.Fact));
    }
}

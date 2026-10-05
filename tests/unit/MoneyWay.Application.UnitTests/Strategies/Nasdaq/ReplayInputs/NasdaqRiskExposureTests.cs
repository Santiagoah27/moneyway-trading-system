using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Capabilities;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqRiskExposureTests
{
    private static readonly NasdaqRiskExposureObservationSelector Selector = new();

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

    private static (NasdaqPreEntryEligibilityRuleFact Fact, NasdaqHumanM1RealignmentObservation Realignment) Setup(
        NasdaqHumanH4PermittedDirection direction, decimal? swingLevel = null)
    {
        var pullback = Run(Inputs(direction)).StrategyObservations.SelectMany(o => o.RuleFacts)
            .Where(f => f.RuleId.Value == "NQ-M1-001").Select(f => f.Fact)
            .OfType<NasdaqHumanM1CorrectiveRetracementRuleFact>().First();
        var realignment = new NasdaqHumanM1RealignmentObservation(pullback,
            new(Minute(20), [Minute(20)], swingLevel ?? (direction == NasdaqHumanH4PermittedDirection.Buy ? 99 : 101), direction),
            M5Fixture.At(14, 20), "review:realignment", pullbackBeforeRealignmentAtSameClose: true);
        var fact = Run(Inputs(direction, realignment)).StrategyObservations.SelectMany(o => o.RuleFacts)
            .Where(f => f.RuleId.Value == "NQ-M1-003").Select(f => f.Fact)
            .OfType<NasdaqPreEntryEligibilityRuleFact>().First();
        return (fact, realignment);
    }

    private static StrategyReplayContext Frame(NasdaqHumanH4PermittedDirection direction, DateTimeOffset time,
        NasdaqHumanM1RealignmentObservation realignment, params IStrategyReplayInputObservation[] extra)
    {
        var inputs = Inputs(direction, realignment).Concat(extra).ToArray();
        var history = Run(inputs).StrategyObservations.Where(o => o.AsOfUtc < time).ToArray();
        return M5Fixture.Context(time, inputs).WithPriorObservations(history);
    }

    private static NasdaqHumanStructuralStopLossObservation Stop(NasdaqPreEntryEligibilityRuleFact fact,
        Candle? source = null, decimal? stopPrice = null, DateTimeOffset? observed = null, string reference = "review:stop")
    {
        var anchor = new NasdaqHumanM5ProtectionAnchor(fact.Direction == NasdaqHumanH4PermittedDirection.Buy
            ? NasdaqM5ProtectionAnchorKind.HigherLow : NasdaqM5ProtectionAnchorKind.LowerHigh,
            [source ?? M5Fixture.Five(M5Fixture.At(14, 20))]);
        return new(fact, anchor, stopPrice ?? (fact.Direction == NasdaqHumanH4PermittedDirection.Buy ? 89 : 111),
            M5Fixture.At(14, 30), observed ?? M5Fixture.At(14, 30), reference);
    }

    private static (NasdaqPreEntryEligibilityRuleFact Entry, NasdaqHumanStructuralStopLossRuleFact Sl,
        NasdaqHumanM1RealignmentObservation Realignment, NasdaqHumanStructuralStopLossObservation Stop) RiskSetup(
        NasdaqHumanH4PermittedDirection direction = NasdaqHumanH4PermittedDirection.Buy, decimal? swing = null, decimal? stopPrice = null)
    {
        var setup = Setup(direction, swing);
        var stop = Stop(setup.Fact, stopPrice: stopPrice);
        var sl = Run(Inputs(direction, setup.Realignment, stop)).StrategyObservations.SelectMany(o => o.RuleFacts)
            .Where(f => f.RuleId.Value == "NQ-SL-001").Select(f => f.Fact)
            .OfType<NasdaqHumanStructuralStopLossRuleFact>().First();
        return (setup.Fact, sl, setup.Realignment, stop);
    }

    private static NasdaqRiskExposure Exposure(decimal account = 10000, decimal loss = 100,
        string basis = "reviewed balance", string currency = "USD", string costs = "commission included; spread excluded",
        decimal? quantity = null, string? unit = null) =>
        new(account, currency, basis, loss, 100, costs, "account:planned-loss calculation", quantity, unit);

    private static NasdaqRiskExposureObservation Risk(NasdaqHumanStructuralStopLossRuleFact sl,
        NasdaqRiskExposure? exposure = null, DateTimeOffset? effective = null,
        DateTimeOffset? observed = null, string source = "review:risk") =>
        new(sl.PreEntryEligibility, sl, exposure ?? Exposure(), effective ?? M5Fixture.At(15),
            observed ?? M5Fixture.At(15), source);

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell)]
    public void ExactDocumentedExposureIsUniqueWithoutRiskDecision(NasdaqHumanH4PermittedDirection direction)
    {
        var setup = RiskSetup(direction);
        var exposure = Exposure(loss: 150, quantity: 2, unit: "documented contracts");
        var risk = Risk(setup.Sl, exposure);
        var context = Frame(direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, risk);
        var selected = Assert.IsType<NasdaqRiskExposureSelection.Unique>(Selector.Select(context, setup.Entry, setup.Sl));
        Assert.Same(risk, selected.Fact);
        Assert.Same(exposure, selected.Fact.Exposure);
        Assert.Same(setup.Sl, selected.Fact.StopLoss);
        Assert.Same(setup.Sl.PreEntryEligibility, selected.Fact.PreEntryEligibility);
        Assert.Equal(direction, selected.Fact.Direction);
        Assert.Equal(150, exposure.ReportedMaximumLoss);
        Assert.Equal(10000, exposure.AccountBasisAmount);
        Assert.Equal("USD", exposure.Currency);
        Assert.Equal("reviewed balance", exposure.AccountBasis);
        Assert.Equal(100, exposure.PlannedEntryPrice);
        Assert.Equal(2, exposure.Quantity);
        Assert.Equal("documented contracts", exposure.QuantityUnit);
        Assert.Equal(M5Fixture.At(15), risk.EffectiveAtUtc);
        Assert.NotEqual(context.AsOfUtc, risk.EffectiveAtUtc);
        Assert.Empty(selected.UnavailableSourceObservations);
        var report = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(),
            MoneyWayReplayRuleEvaluators.GetAll(), MoneyWayReplayEvaluationCapabilityDeclarations.GetAll())
            .Find(LiquidityFixture.Definition.StrategyId, LiquidityFixture.Definition.Version)!;
        Assert.Equal((32, 14, 14, 1, false), (report.TotalRuleCount, report.RequiredRuleCount, report.ImplementedCount,
            report.RequiredEvaluatorGapCount, report.HasFullRequiredEvaluatorRegistration));
        Assert.DoesNotContain(MoneyWayReplayRuleEvaluators.GetAll(), e => e.RuleId.Value is "NQ-RISK-001" or "NQ-TP-001");
        Assert.Empty(MoneyWayReplayWorkflowDefinitions.GetAll().Single().GetPrerequisiteRuleIds(new("NQ-RISK-001")));
    }

    [Fact]
    public void MissingAndCompatibleDuplicatesPreserveAllSupportDeterministically()
    {
        var setup = RiskSetup();
        var time = M5Fixture.At(15, 5);
        Assert.IsType<NasdaqRiskExposureSelection.Missing>(Selector.Select(
            Frame(setup.Entry.Direction, time, setup.Realignment, setup.Stop), setup.Entry, setup.Sl));
        var first = Risk(setup.Sl);
        var second = Risk(setup.Sl, new(10000, "USD", "reviewed balance", 100, 100,
            "commission included; spread excluded", "second:calculation"), observed: time, source: "second:review");
        var forward = Assert.IsType<NasdaqRiskExposureSelection.Unique>(Selector.Select(
            Frame(setup.Entry.Direction, time, setup.Realignment, setup.Stop, second, first), setup.Entry, setup.Sl));
        var reversed = Assert.IsType<NasdaqRiskExposureSelection.Unique>(Selector.Select(
            Frame(setup.Entry.Direction, time, setup.Realignment, setup.Stop, first, second), setup.Entry, setup.Sl));
        Assert.Equal(2, forward.SupportingObservations.Count);
        Assert.Equal(forward.SupportingObservations.Select(o => o.SourceReference), reversed.SupportingObservations.Select(o => o.SourceReference));
        Assert.Contains(first, forward.SupportingObservations);
        Assert.Contains(second, forward.SupportingObservations);
    }

    [Theory]
    [InlineData(12000, 100, "reviewed balance", "USD", "commission included; spread excluded", 0)]
    [InlineData(10000, 110, "reviewed balance", "USD", "commission included; spread excluded", 0)]
    [InlineData(10000, 100, "reviewed equity", "USD", "commission included; spread excluded", 0)]
    [InlineData(10000, 100, "reviewed balance", "EUR", "commission included; spread excluded", 0)]
    [InlineData(10000, 100, "reviewed balance", "USD", "all costs excluded", 0)]
    [InlineData(10000, 100, "reviewed balance", "USD", "commission included; spread excluded", 1)]
    public void IncompatibleDocumentedParametersOrAssessmentTimesConflict(int account, int loss, string basis,
        string currency, string costs, int minute)
    {
        var setup = RiskSetup();
        var first = Risk(setup.Sl);
        var other = Risk(setup.Sl, Exposure(account, loss, basis, currency, costs),
            M5Fixture.At(15, minute), M5Fixture.At(15, minute), "conflicting:review");
        var selection = Assert.IsType<NasdaqRiskExposureSelection.Conflict>(Selector.Select(
            Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, first, other), setup.Entry, setup.Sl));
        Assert.Equal(2, selection.Alternatives.Count);
        Assert.Contains(other, selection.Alternatives);
    }

    [Fact]
    public void WrongEligibilityDifferentStopAndMixedAncestryCannotQualify()
    {
        var setup = RiskSetup();
        var wrong = RiskSetup(swing: 98);
        var differentStop = RiskSetup(stopPrice: 88);
        Assert.Throws<ArgumentException>(() => new NasdaqRiskExposureObservation(setup.Entry, wrong.Sl, Exposure(),
            M5Fixture.At(15), M5Fixture.At(15), "mixed:ancestry"));
        foreach (var risk in new[] { Risk(wrong.Sl), Risk(differentStop.Sl) })
            Assert.IsType<NasdaqRiskExposureSelection.Missing>(Selector.Select(
                Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, risk), setup.Entry, setup.Sl));
        Assert.IsType<NasdaqRiskExposureSelection.Missing>(Selector.Select(
            Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, Risk(setup.Sl)), wrong.Entry, setup.Sl));
    }

    [Fact]
    public void FutureEvidenceCannotBackfillAndMissingPrerequisiteCannotQualify()
    {
        var setup = RiskSetup();
        var risk = Risk(setup.Sl);
        var future = Risk(setup.Sl, Exposure(loss: 200), observed: M5Fixture.At(15, 10), source: "future:conflict");
        var time = M5Fixture.At(15, 5);
        var baseline = Frame(setup.Entry.Direction, time, setup.Realignment, setup.Stop, risk);
        var expanded = Frame(setup.Entry.Direction, time, setup.Realignment, setup.Stop, risk, future);
        Assert.Equal(Assert.IsType<NasdaqRiskExposureSelection.Unique>(Selector.Select(baseline, setup.Entry, setup.Sl)).SupportingObservations,
            Assert.IsType<NasdaqRiskExposureSelection.Unique>(Selector.Select(expanded, setup.Entry, setup.Sl)).SupportingObservations);
        var bounded = M5Fixture.Context(time, baseline.InputObservations.ToArray(), includeFutureCandles: false)
            .WithPriorObservations(baseline.PriorObservations);
        Assert.Equal(Assert.IsType<NasdaqRiskExposureSelection.Unique>(Selector.Select(baseline, setup.Entry, setup.Sl)).SupportingObservations,
            Assert.IsType<NasdaqRiskExposureSelection.Unique>(Selector.Select(bounded, setup.Entry, setup.Sl)).SupportingObservations);
        Assert.IsType<NasdaqRiskExposureSelection.Missing>(Selector.Select(
            Frame(setup.Entry.Direction, time, setup.Realignment, setup.Stop, future), setup.Entry, setup.Sl));
        Assert.IsType<NasdaqRiskExposureSelection.Missing>(Selector.Select(
            Frame(setup.Entry.Direction, time, setup.Realignment, risk), setup.Entry, setup.Sl));
        Assert.IsType<NasdaqRiskExposureSelection.Missing>(Selector.Select(
            baseline.WithPriorObservations([]), setup.Entry, setup.Sl));
    }

    [Fact]
    public void MissingInheritedMarketSourcesRemainUnavailableAndInconsistentSourcesAreUnusable()
    {
        var setup = RiskSetup();
        var risk = Risk(setup.Sl);
        var context = Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, risk);
        var frames = context.AvailableTimeframes.Where(t => t != NasdaqHumanM5ProtectionAnchor.M5)
            .ToDictionary(t => t, t => { context.TryGetFrame(t, out var frame); return frame!; });
        var partial = new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition,
            new MultiTimeframeReplayFrame(context.ProviderId, context.Symbol, context.Step, context.AsOfUtc,
                context.ConfiguredTimeframes, context.UpdatedTimeframes.Where(frames.ContainsKey), frames),
            context.InputObservations).WithPriorObservations(context.PriorObservations);
        var missing = Assert.IsType<NasdaqRiskExposureSelection.Missing>(Selector.Select(partial, setup.Entry, setup.Sl));
        Assert.Same(risk, Assert.Single(missing.UnavailableSourceObservations));

        var changed = new CandleSeries(LiquidityFixture.Provider, LiquidityFixture.Symbol, NasdaqHumanM5ProtectionAnchor.M5,
            M5Fixture.M5Sources.Select(c => c.CloseTimeUtc == M5Fixture.At(14, 20)
                ? M5Fixture.Five(c.CloseTimeUtc, 101) : c));
        var cursor = new MultiTimeframeCandleReplayCursor([changed]);
        ReplayFrame? m5 = null;
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == context.AsOfUtc) { m5 = frame.FramesByTimeframe[NasdaqHumanM5ProtectionAnchor.M5]; break; }
        frames.Add(NasdaqHumanM5ProtectionAnchor.M5, m5!);
        var inconsistent = new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition,
            new MultiTimeframeReplayFrame(context.ProviderId, context.Symbol, context.Step, context.AsOfUtc,
                context.ConfiguredTimeframes, context.UpdatedTimeframes, frames), context.InputObservations)
            .WithPriorObservations(context.PriorObservations);
        Assert.Empty(Assert.IsType<NasdaqRiskExposureSelection.Missing>(Selector.Select(inconsistent, setup.Entry, setup.Sl))
            .UnavailableSourceObservations);
    }

    [Fact]
    public void TerminalCutoffAndNewStopConflictDominateExistingRiskFact()
    {
        var setup = RiskSetup();
        var risk = Risk(setup.Sl);
        Assert.IsType<NasdaqRiskExposureSelection.Missing>(Selector.Select(
            Frame(setup.Entry.Direction, M5Fixture.At(16), setup.Realignment, setup.Stop, risk), setup.Entry, setup.Sl));
        var invalid = M5Fixture.Take(true, effective: M5Fixture.At(14, 30), observed: M5Fixture.At(14, 30));
        var initiating = setup.Entry.Realignment.Pullback.Selection.Fact.ApprovedQuality.Fact.Fvg.Trigger.DecisiveTake;
        var terminal = Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, risk,
            invalid, new NasdaqHumanRelevantLiquidityTakeObservation(invalid, invalid.ObservedAtUtc, "terminal:event", initiating));
        Assert.IsType<NasdaqRiskExposureSelection.Missing>(Selector.Select(terminal, setup.Entry, setup.Sl));
        var gate = terminal.PriorObservations.Last().RuleFacts.Single(f => f.RuleId.Value == "NQ-LIQ-003");
        Assert.True(Assert.IsType<NasdaqLiquidityTakeRuleFact>(gate.Fact).IsSessionInvalidated);
        var valid = Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, risk);
        var last = valid.PriorObservations.Last();
        var gatedHistory = valid.PriorObservations.SkipLast(1).Append(new StrategyReplayContextObservation(
            last.StrategyId, last.StrategyVersion, last.ProviderId, last.Symbol, last.Step, last.AsOfUtc,
            last.Evaluations, last.WorkflowProgression, last.LifecycleProgression,
            ruleFacts: last.RuleFacts.Where(f => f.RuleId.Value != "NQ-LIQ-003").Append(gate)));
        Assert.IsType<NasdaqRiskExposureSelection.Missing>(Selector.Select(valid.WithPriorObservations(gatedHistory), setup.Entry, setup.Sl));
        var conflictingStop = Stop(setup.Entry, stopPrice: 88);
        Assert.IsType<NasdaqRiskExposureSelection.Missing>(Selector.Select(
            Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, conflictingStop, risk), setup.Entry, setup.Sl));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-1, 100)]
    [InlineData(10000, -1)]
    public void InvalidNumericInputsAreRejected(int account, int loss) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Exposure(account, loss));

    [Fact]
    public void RequiredDocumentationQuantityAndUtcAvailabilityAreValidated()
    {
        Assert.Equal(0, Exposure(loss: 0).ReportedMaximumLoss);
        Assert.Null(Exposure().Quantity);
        Assert.Throws<ArgumentException>(() => Exposure(basis: " "));
        Assert.Throws<ArgumentException>(() => Exposure(currency: ""));
        Assert.Throws<ArgumentException>(() => Exposure(costs: ""));
        Assert.Throws<ArgumentException>(() => Exposure(quantity: 0, unit: "contracts"));
        Assert.Throws<ArgumentException>(() => Exposure(quantity: 2));
        Assert.Throws<ArgumentException>(() => Exposure(unit: "contracts"));
        Assert.Throws<ArgumentException>(() => Exposure(quantity: 2, unit: " "));
        Assert.Throws<ArgumentException>(() => new NasdaqRiskExposure(10000, "USD", "reviewed balance", 100, 100,
            "costs included", " "));
        var setup = RiskSetup();
        Assert.Throws<ArgumentException>(() => Risk(setup.Sl, effective: M5Fixture.At(14, 20)));
        Assert.Throws<ArgumentException>(() => Risk(setup.Sl, observed: M5Fixture.At(14, 30)));
        Assert.Throws<ArgumentException>(() => Risk(setup.Sl, effective: M5Fixture.At(15).ToOffset(TimeSpan.FromHours(1))));
        Assert.Throws<ArgumentException>(() => Risk(setup.Sl, observed: M5Fixture.At(15).ToOffset(TimeSpan.FromHours(1))));
        Assert.Throws<ArgumentException>(() => Risk(setup.Sl, source: " "));
    }

    [Fact]
    public void EntryPriceQuantityAndQuantityUnitsAreSemanticInputsRatherThanDerivedValues()
    {
        var setup = RiskSetup();
        var first = Risk(setup.Sl);
        var exposures = new[]
        {
            new NasdaqRiskExposure(10000, "USD", "reviewed balance", 100, 101,
                "commission included; spread excluded", "entry:reference"),
            Exposure(quantity: 2, unit: "contracts"),
            Exposure(quantity: 2, unit: "shares"),
        };
        foreach (var exposure in exposures)
        {
            var other = Risk(setup.Sl, exposure);
            var forward = Assert.IsType<NasdaqRiskExposureSelection.Conflict>(Selector.Select(
                Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, first, other), setup.Entry, setup.Sl));
            var reversed = Assert.IsType<NasdaqRiskExposureSelection.Conflict>(Selector.Select(
                Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, other, first), setup.Entry, setup.Sl));
            Assert.Equal(forward.Alternatives, reversed.Alternatives);
        }
    }
}

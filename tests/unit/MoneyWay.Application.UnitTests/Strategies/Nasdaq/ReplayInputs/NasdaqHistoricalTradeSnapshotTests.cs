using MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;
using System.Text.Json;
using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
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

public sealed class NasdaqHistoricalTradeSnapshotTests
{
    private static readonly NasdaqHistoricalTradeSnapshotAssembler Assembler = new();

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
        NasdaqHumanM1RealignmentObservation Realignment, NasdaqHumanStructuralStopLossObservation Stop) TargetSetup(
        NasdaqHumanH4PermittedDirection direction = NasdaqHumanH4PermittedDirection.Buy, decimal? swing = null, decimal? stopPrice = null)
    {
        var setup = Setup(direction, swing);
        var stop = Stop(setup.Fact, stopPrice: stopPrice);
        var sl = Run(Inputs(direction, setup.Realignment, stop)).StrategyObservations.SelectMany(o => o.RuleFacts)
            .Where(f => f.RuleId.Value == "NQ-SL-001").Select(f => f.Fact)
            .OfType<NasdaqHumanStructuralStopLossRuleFact>().First();
        return (setup.Fact, sl, setup.Realignment, stop);
    }

    private static NasdaqHumanTakeProfitTarget SessionTarget(bool low = false, bool london = false,
        string source = "review:target source")
    {
        var context = M5Fixture.Context(M5Fixture.At(13, 15), []);
        var endpoint = london ? (low ? NasdaqLiquidityTakeReference.SessionEndpoint.LondonLow : NasdaqLiquidityTakeReference.SessionEndpoint.LondonHigh)
            : (low ? NasdaqLiquidityTakeReference.SessionEndpoint.AsiaLow : NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh);
        var reference = new NasdaqLiquidityTakeReference.SessionLevel(context, new NasdaqSessionLiquidityCalculator().Calculate(context),
            endpoint, context.AsOfUtc, source);
        return new(reference, reference.ReferencePrice);
    }

    private static NasdaqHumanTakeProfitTarget StructuralTarget(bool low, int hours, decimal price = 20000,
        Candle? candle = null)
    {
        var member = LiquidityFixture.Human(candle ?? M5Fixture.Sources().Single(c => c.Timeframe == new Timeframe(hours, TimeframeUnit.Hour)
            && c.OpenTimeUtc == M5Fixture.At(8)), low, price);
        var observation = new NasdaqHumanStructuralLiquidityObservation(LiquidityFixture.Session(13), [member],
            M5Fixture.At(12), M5Fixture.At(13), "review:target structure");
        var selection = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(
            new NasdaqHumanStructuralLiquidityObservationSelector().Select(M5Fixture.Context(M5Fixture.At(13, 15), [observation]), observation.Session));
        return new(new NasdaqLiquidityTakeReference.Structural(selection, member), price);
    }

    private static NasdaqHumanTakeProfitObservation Target(NasdaqHumanStructuralStopLossRuleFact sl,
        NasdaqHumanTakeProfitTarget? target = null, DateTimeOffset? effective = null, DateTimeOffset? observed = null,
        string source = "review:selected TP") => new(sl, target ?? SessionTarget(sl.Direction == NasdaqHumanH4PermittedDirection.Sell),
            effective ?? M5Fixture.At(15), observed ?? M5Fixture.At(15), source);

    private static NasdaqRiskExposureObservation Risk(NasdaqPreEntryEligibilityRuleFact entry,
        NasdaqHumanStructuralStopLossRuleFact sl, decimal loss = 100) => new(entry, sl,
            new(10000, "USD", "documented balance", loss, 100, "documented costs", "risk:calculation", 2, "contracts"),
            M5Fixture.At(15), M5Fixture.At(15), "risk:review");

    private static NasdaqHistoricalObservedEntryObservation Entry(NasdaqPreEntryEligibilityRuleFact eligibility,
        decimal price = 20000, DateTimeOffset? observed = null, IEnumerable<Candle>? sources = null) => new(
            new(eligibility, "mentor:execution:1", price, eligibility.EligibilityEffectiveAtUtc, "entry:retained record",
                supportingCandles: sources), observed ?? M5Fixture.At(15), "entry:review");

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell)]
    public void CompleteSnapshotPreservesExactParametersAncestryAndProvenanceWithoutPriceOrdering(NasdaqHumanH4PermittedDirection direction)
    {
        var setup = TargetSetup(direction);
        var target = Target(setup.Sl);
        var risk = Risk(setup.Entry, setup.Sl);
        var entry = Entry(setup.Entry);
        var context = Frame(direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, target, risk, entry);
        var snapshot = Assert.IsType<NasdaqHistoricalTradeSnapshotResult.Available>(Assembler.Assemble(context, setup.Entry)).Snapshot;
        Assert.Same(setup.Entry, snapshot.PreEntryEligibility);
        Assert.Same(entry.Entry, snapshot.Entry);
        Assert.Same(entry, snapshot.ObservedEntry.Fact);
        Assert.Equal(20000, snapshot.EntryPrice); // Deliberately outside SL/TP: no new price-ordering rule.
        Assert.Equal(100, snapshot.Risk.Exposure.PlannedEntryPrice);
        Assert.Equal(direction, snapshot.Direction);
        Assert.Equal(context.AsOfUtc, snapshot.AsOfUtc);
        Assert.Equal(setup.Entry.EligibilityEffectiveAtUtc, snapshot.EntryEffectiveAtUtc);
        Assert.True(context.PriorObservations.First(o => o.RuleFacts.Any(f => f.Fact is NasdaqPreEntryEligibilityRuleFact)).AsOfUtc > snapshot.EntryEffectiveAtUtc);
        Assert.Same(context.PriorObservations.Last().RuleFacts.Single(f => f.RuleId.Value == "NQ-SL-001").Fact, snapshot.StopLoss);
        Assert.Same(context.PriorObservations.Last().RuleFacts.Single(f => f.RuleId.Value == "NQ-TP-001").Fact, snapshot.TakeProfit);
        Assert.Same(context.PriorObservations.Last().RuleFacts.Single(f => f.RuleId.Value == "NQ-RISK-001").Fact, snapshot.Risk);
        Assert.Equal(setup.Stop.StopPrice, snapshot.StopPrice);
        Assert.Same(setup.Stop.ProtectionAnchor, snapshot.StopLoss.Selection.Fact.ProtectionAnchor);
        Assert.Equal(setup.Stop.EffectiveAtUtc, snapshot.StopLoss.EffectiveAtUtc);
        Assert.Same(target.Target, snapshot.TakeProfit.Target);
        Assert.Equal(target.Target.TargetReferencePrice, snapshot.TakeProfitPrice);
        Assert.Equal(target.EffectiveAtUtc, snapshot.TakeProfit.EffectiveAtUtc);
        Assert.Equal(risk.EffectiveAtUtc, snapshot.Risk.EffectiveAtUtc);
        Assert.Same(risk.Exposure, snapshot.Risk.Exposure);
        Assert.Equal(0.01m, snapshot.Risk.RiskRatio);
        Assert.Equal(0.01m, snapshot.Risk.Limit);
        Assert.Equal("entry:retained record", snapshot.Entry.ExecutionSourceReference);
        Assert.Contains(setup.Stop, snapshot.StopLoss.Selection.SupportingObservations);
        Assert.Contains(target, snapshot.TakeProfit.Selection.SupportingObservations);
        Assert.Contains(risk, snapshot.Risk.Selection.SupportingObservations);
        Assert.All(typeof(NasdaqHistoricalTradeSnapshot).GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("conflict")]
    [InlineData("wrong")]
    [InlineData("future")]
    [InlineData("unavailable")]
    public void EntryDiagnosticsPreventIncompleteSnapshots(string mode)
    {
        var setup = TargetSetup();
        var target = Target(setup.Sl);
        var risk = Risk(setup.Entry, setup.Sl);
        var wrong = TargetSetup(swing: 98);
        IStrategyReplayInputObservation[] entries = mode switch
        {
            "missing" => [],
            "conflict" => [Entry(setup.Entry), Entry(setup.Entry, price: 20001)],
            "wrong" => [Entry(wrong.Entry)],
            "future" => [Entry(setup.Entry, observed: M5Fixture.At(15, 10))],
            _ => [Entry(setup.Entry, sources: [Minute(20)])],
        };
        var context = Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment,
            new IStrategyReplayInputObservation[] { setup.Stop, target, risk }.Concat(entries).ToArray());
        if (mode == "unavailable") context = WithoutFrame(context, M5Fixture.Minute);
        var result = Assert.IsType<NasdaqHistoricalTradeSnapshotResult.Unavailable>(Assembler.Assemble(context, setup.Entry));
        if (mode == "conflict") Assert.Equal(2, Assert.IsType<NasdaqHistoricalObservedEntrySelection.Conflict>(result.EntrySelection).Alternatives.Count);
        if (mode == "unavailable") Assert.Single(result.EntrySelection.UnavailableSourceObservations);
    }

    private static StrategyReplayContext ChangeFact(StrategyReplayContext context, string ruleId, StrategyReplayRuleFact? replacement)
    {
        return context.WithPriorObservations(context.PriorObservations.Select(o => new StrategyReplayContextObservation(
            o.StrategyId, o.StrategyVersion, o.ProviderId, o.Symbol, o.Step, o.AsOfUtc, o.Evaluations,
            o.WorkflowProgression, o.LifecycleProgression, o.MarketDataObservability,
            o.RuleFacts.Where(f => f.RuleId.Value != ruleId).Concat(replacement is null ? [] : [replacement]))));
    }

    [Theory]
    [InlineData("NQ-SL-001")]
    [InlineData("NQ-TP-001")]
    [InlineData("NQ-RISK-001")]
    public void RawEvidenceCannotReplaceMissingCanonicalConstituents(string ruleId)
    {
        var setup = TargetSetup();
        var context = Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment,
            setup.Stop, Target(setup.Sl), Risk(setup.Entry, setup.Sl), Entry(setup.Entry));
        Assert.IsType<NasdaqHistoricalTradeSnapshotResult.Available>(Assembler.Assemble(context, setup.Entry));
        Assert.IsType<NasdaqHistoricalTradeSnapshotResult.Unavailable>(Assembler.Assemble(ChangeFact(context, ruleId, null), setup.Entry));
    }

    [Theory]
    [InlineData("NQ-SL-001")]
    [InlineData("NQ-TP-001")]
    [InlineData("NQ-RISK-001")]
    public void NumericallyMatchingComponentsFromOtherAncestryCannotBeCombined(string ruleId)
    {
        var a = TargetSetup();
        var b = TargetSetup(swing: 98);
        var context = Frame(a.Entry.Direction, M5Fixture.At(15, 5), a.Realignment,
            a.Stop, Target(a.Sl), Risk(a.Entry, a.Sl), Entry(a.Entry));
        var other = Frame(b.Entry.Direction, M5Fixture.At(15, 5), b.Realignment,
            b.Stop, Target(b.Sl), Risk(b.Entry, b.Sl), Entry(b.Entry));
        var replacement = other.PriorObservations.Last().RuleFacts.Single(f => f.RuleId.Value == ruleId);
        Assert.IsType<NasdaqHistoricalTradeSnapshotResult.Unavailable>(Assembler.Assemble(ChangeFact(context, ruleId, replacement), a.Entry));
    }

    [Fact]
    public void ExactSlSelectionMustAgreeEvenWithinTheSameEligibility()
    {
        var a = TargetSetup();
        var b = TargetSetup(stopPrice: 88);
        var context = Frame(a.Entry.Direction, M5Fixture.At(15, 5), a.Realignment,
            a.Stop, Target(a.Sl), Risk(a.Entry, a.Sl), Entry(a.Entry));
        var other = Frame(b.Entry.Direction, M5Fixture.At(15, 5), b.Realignment,
            b.Stop, Target(b.Sl), Risk(b.Entry, b.Sl), Entry(b.Entry));
        foreach (var ruleId in new[] { "NQ-TP-001", "NQ-RISK-001" })
            Assert.IsType<NasdaqHistoricalTradeSnapshotResult.Unavailable>(Assembler.Assemble(ChangeFact(context, ruleId,
                other.PriorObservations.Last().RuleFacts.Single(f => f.RuleId.Value == ruleId)), a.Entry));
    }

    [Fact]
    public void CanonicalFailedRiskIsPreservedAsExposureWithoutAuthorizingTrade()
    {
        var setup = TargetSetup();
        var context = Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment,
            setup.Stop, Target(setup.Sl), Risk(setup.Entry, setup.Sl, loss: 101), Entry(setup.Entry));
        var snapshot = Assert.IsType<NasdaqHistoricalTradeSnapshotResult.Available>(Assembler.Assemble(context, setup.Entry)).Snapshot;
        Assert.False(snapshot.Risk.IsWithinLimit);
        Assert.Equal(0.0101m, snapshot.Risk.RiskRatio);
    }

    [Theory]
    [InlineData("NQ-SL-001")]
    [InlineData("NQ-TP-001")]
    [InlineData("NQ-RISK-001")]
    public void LaterConstituentFactsDoNotBackfillEarlierContext(string ruleId)
    {
        var setup = TargetSetup();
        var target = Target(setup.Sl);
        var risk = Risk(setup.Entry, setup.Sl);
        var inputs = new IStrategyReplayInputObservation[] { setup.Stop, target, risk, Entry(setup.Entry) };
        var context = Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, inputs);
        var earlier = ChangeFact(context, ruleId, null);
        Assert.IsType<NasdaqHistoricalTradeSnapshotResult.Unavailable>(Assembler.Assemble(earlier, setup.Entry));
        Assert.IsType<NasdaqHistoricalTradeSnapshotResult.Available>(Assembler.Assemble(context, setup.Entry));
        Assert.IsType<NasdaqHistoricalTradeSnapshotResult.Unavailable>(Assembler.Assemble(earlier, setup.Entry));
    }

    [Theory]
    [InlineData("TP")]
    [InlineData("Risk")]
    public void FutureRawEvidenceOnlyProducesFactsInLaterCanonicalFrames(string delayed)
    {
        var setup = TargetSetup();
        var target = Target(setup.Sl, observed: delayed == "TP" ? M5Fixture.At(15, 5) : M5Fixture.At(15));
        var original = Risk(setup.Entry, setup.Sl);
        var risk = new NasdaqRiskExposureObservation(setup.Entry, setup.Sl, original.Exposure,
            original.EffectiveAtUtc, delayed == "Risk" ? M5Fixture.At(15, 5) : M5Fixture.At(15), original.SourceReference);
        var entries = new IStrategyReplayInputObservation[] { setup.Stop, target, risk, Entry(setup.Entry) };
        var earlier = Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, entries);
        Assert.IsType<NasdaqHistoricalTradeSnapshotResult.Unavailable>(Assembler.Assemble(earlier, setup.Entry));
        var later = Frame(setup.Entry.Direction, M5Fixture.At(15, 10), setup.Realignment, entries);
        Assert.IsType<NasdaqHistoricalTradeSnapshotResult.Available>(Assembler.Assemble(later, setup.Entry));
        Assert.IsType<NasdaqHistoricalTradeSnapshotResult.Unavailable>(Assembler.Assemble(earlier, setup.Entry));
    }

    [Fact]
    public void CompositionIsStableWithoutPostEntryMarketFramesAndDoesNotChangeCanonicalEvaluations()
    {
        var setup = TargetSetup();
        var inputs = Inputs(setup.Entry.Direction, setup.Realignment, setup.Stop, Target(setup.Sl), Risk(setup.Entry, setup.Sl), Entry(setup.Entry));
        var context = Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, inputs.Skip(Inputs(setup.Entry.Direction, setup.Realignment).Length).ToArray());
        var baseline = JsonSerializer.Serialize(context.PriorObservations);
        var noCandles = context;
        foreach (var timeframe in context.AvailableTimeframes) noCandles = WithoutFrame(noCandles, timeframe);
        var snapshot = Assert.IsType<NasdaqHistoricalTradeSnapshotResult.Available>(Assembler.Assemble(noCandles, setup.Entry)).Snapshot;
        var other = Assert.IsType<NasdaqHistoricalTradeSnapshotResult.Available>(Assembler.Assemble(context, setup.Entry)).Snapshot;
        Assert.Same(snapshot.Entry, other.Entry);
        Assert.Same(snapshot.StopLoss, other.StopLoss);
        Assert.Equal(baseline, JsonSerializer.Serialize(context.PriorObservations));
        var report = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), MoneyWayReplayRuleEvaluators.GetAll(),
            MoneyWayReplayEvaluationCapabilityDeclarations.GetAll()).Find(LiquidityFixture.Definition.StrategyId, LiquidityFixture.Definition.Version)!;
        Assert.Equal((32, 14, 16, 0, true), (report.TotalRuleCount, report.RequiredRuleCount, report.ImplementedCount,
            report.RequiredEvaluatorGapCount, report.HasFullRequiredEvaluatorRegistration));
    }

    private static StrategyReplayContext WithoutFrame(StrategyReplayContext context, Timeframe missing)
    {
        var frames = context.AvailableTimeframes.Where(t => t != missing)
            .ToDictionary(t => t, t => { context.TryGetFrame(t, out var frame); return frame!; });
        return new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition,
            new MultiTimeframeReplayFrame(context.ProviderId, context.Symbol, context.Step, context.AsOfUtc,
                context.ConfiguredTimeframes, context.UpdatedTimeframes.Where(frames.ContainsKey), frames),
            context.InputObservations).WithPriorObservations(context.PriorObservations);
    }

}

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

public sealed class NasdaqHistoricalObservedEntryTests
{
    private static readonly NasdaqHistoricalObservedEntryObservationSelector Selector = new();

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

    private static NasdaqHistoricalObservedEntryObservation Entry(NasdaqPreEntryEligibilityRuleFact fact,
        decimal price = 123.456m, DateTimeOffset? effective = null, DateTimeOffset? observed = null,
        string id = "mentor:execution:1", string source = "review:entry", IEnumerable<Candle>? candles = null,
        decimal? quantity = null, string? unit = null) => new(new(fact, id, price,
            effective ?? fact.EligibilityEffectiveAtUtc, "retained:execution record", quantity, unit, candles),
            observed ?? M5Fixture.At(15), source);

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell)]
    public void MissingNeverInfersExecutionFromCandlesOrPlannedRiskPrice(NasdaqHumanH4PermittedDirection direction)
    {
        var setup = TargetSetup(direction);
        var exposure = new NasdaqRiskExposure(10000, "USD", "balance", 100, 100, "documented costs", "risk:record");
        var risk = new NasdaqRiskExposureObservation(setup.Entry, setup.Sl, exposure, M5Fixture.At(15), M5Fixture.At(15), "risk:review");
        Assert.IsType<NasdaqHistoricalObservedEntrySelection.Missing>(Selector.Select(
            Frame(direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, risk), setup.Entry));
        var entry = Entry(setup.Entry);
        var unique = Assert.IsType<NasdaqHistoricalObservedEntrySelection.Unique>(Selector.Select(
            Frame(direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, risk, entry), setup.Entry));
        Assert.Equal(123.456m, unique.Fact.Entry.EntryPrice);
        Assert.Equal(100m, risk.Exposure.PlannedEntryPrice);
        Assert.Same(setup.Entry, unique.Fact.PreEntryEligibility);
        Assert.Same(setup.Entry.Realignment, unique.Fact.Entry.PreEntryEligibility.Realignment);
        Assert.Equal(direction, unique.Fact.Entry.Direction);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SameCloseAndLaterExecutionsDoNotWaitForCanonicalTransport(int minutes)
    {
        var setup = Setup(NasdaqHumanH4PermittedDirection.Buy);
        var time = setup.Fact.EligibilityEffectiveAtUtc.AddMinutes(minutes);
        var entry = Entry(setup.Fact, effective: time, observed: time);
        var context = Frame(setup.Fact.Direction, M5Fixture.At(15, 5), setup.Realignment, entry);
        var established = context.PriorObservations.First(o => o.RuleFacts.Any(f => f.Fact is NasdaqPreEntryEligibilityRuleFact));
        Assert.True(established.AsOfUtc > entry.Entry.EntryEffectiveAtUtc);
        Assert.IsType<NasdaqHistoricalObservedEntrySelection.Unique>(Selector.Select(context, setup.Fact));
        Assert.IsType<NasdaqHistoricalObservedEntrySelection.Missing>(Selector.Select(
            context.WithPriorObservations([]), setup.Fact));
    }

    [Fact]
    public void AvailabilityDoesNotBackdateKnowledgeOrAcceptWrongAncestry()
    {
        var setup = Setup(NasdaqHumanH4PermittedDirection.Buy);
        var wrong = Setup(setup.Fact.Direction, 98);
        var entry = Entry(setup.Fact, observed: M5Fixture.At(15, 5));
        var futureConflict = Entry(setup.Fact, price: 999, observed: M5Fixture.At(15, 10));
        var later = new LaterTradeInformation(setup.Fact.Session, M5Fixture.At(16), "later:result", true, true, "unknown");
        var earlier = Frame(setup.Fact.Direction, M5Fixture.At(15), setup.Realignment, entry, futureConflict, later);
        Assert.IsType<NasdaqHistoricalObservedEntrySelection.Missing>(Selector.Select(earlier, setup.Fact));
        var current = Frame(setup.Fact.Direction, M5Fixture.At(15, 5), setup.Realignment, entry, futureConflict, later);
        Assert.Same(entry, Assert.IsType<NasdaqHistoricalObservedEntrySelection.Unique>(Selector.Select(current, setup.Fact)).Fact);
        Assert.IsType<NasdaqHistoricalObservedEntrySelection.Missing>(Selector.Select(current, wrong.Fact));
        Assert.IsType<NasdaqHistoricalObservedEntrySelection.Missing>(Selector.Select(
            Frame(setup.Fact.Direction, M5Fixture.At(15, 5), setup.Realignment, Entry(wrong.Fact)), setup.Fact));
        Assert.IsType<NasdaqHistoricalObservedEntrySelection.Conflict>(Selector.Select(
            Frame(setup.Fact.Direction, M5Fixture.At(15, 10), setup.Realignment, entry, futureConflict), setup.Fact));
    }

    [Fact]
    public void CompatibleSupportIsImmutableAndInputOrderIndependent()
    {
        var setup = Setup(NasdaqHumanH4PermittedDirection.Buy);
        var candles = new List<Candle> { Minute(20) };
        var a = Entry(setup.Fact, candles: candles, quantity: 2, unit: "contracts");
        candles.Clear();
        var b = new NasdaqHistoricalObservedEntryObservation(new(setup.Fact, a.Entry.ExecutionId,
            a.Entry.EntryPrice, a.Entry.EntryEffectiveAtUtc, "second:retained record", 2, "contracts"),
            M5Fixture.At(15, 5), "second:review");
        var first = Assert.IsType<NasdaqHistoricalObservedEntrySelection.Unique>(Selector.Select(
            Frame(setup.Fact.Direction, M5Fixture.At(15, 5), setup.Realignment, a, b), setup.Fact));
        var reverse = Assert.IsType<NasdaqHistoricalObservedEntrySelection.Unique>(Selector.Select(
            Frame(setup.Fact.Direction, M5Fixture.At(15, 5), setup.Realignment, b, a), setup.Fact));
        Assert.Equal(first.SupportingObservations, reverse.SupportingObservations);
        Assert.Equal(new[] { a, b }, first.SupportingObservations);
        Assert.Single(a.Entry.SupportingCandles);
        Assert.Equal(2m, first.Fact.Entry.Quantity);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHistoricalObservedEntryObservation>)first.SupportingObservations).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<Candle>)a.Entry.SupportingCandles).Clear());
    }

    [Theory]
    [InlineData("price")]
    [InlineData("time")]
    [InlineData("identity")]
    [InlineData("quantity")]
    public void IncompatibleExecutionsConflictWithoutAggregationOrWinner(string difference)
    {
        var setup = Setup(NasdaqHumanH4PermittedDirection.Buy);
        var a = Entry(setup.Fact);
        var b = Entry(setup.Fact, price: difference == "price" ? 124 : 123.456m,
            effective: difference == "time" ? M5Fixture.At(14, 30) : null,
            id: difference == "identity" ? "mentor:execution:2" : "mentor:execution:1",
            quantity: difference == "quantity" ? 1 : null, unit: difference == "quantity" ? "contracts" : null);
        var first = Assert.IsType<NasdaqHistoricalObservedEntrySelection.Conflict>(Selector.Select(
            Frame(setup.Fact.Direction, M5Fixture.At(15, 5), setup.Realignment, a, b), setup.Fact));
        var reversed = Assert.IsType<NasdaqHistoricalObservedEntrySelection.Conflict>(Selector.Select(
            Frame(setup.Fact.Direction, M5Fixture.At(15, 5), setup.Realignment, b, a), setup.Fact));
        Assert.Equal(first.Alternatives, reversed.Alternatives);
        Assert.Equal(2, first.Alternatives.Count);
    }

    [Fact]
    public void UnavailableSourcesArePreservedAndWrongOrMismatchedSourcesAreUnusable()
    {
        var setup = Setup(NasdaqHumanH4PermittedDirection.Buy);
        var entry = Entry(setup.Fact, candles: [Minute(20)]);
        var context = Frame(setup.Fact.Direction, M5Fixture.At(15, 5), setup.Realignment, entry);
        var missing = WithoutFrame(context, M5Fixture.Minute);
        Assert.Equal(new[] { entry }, Assert.IsType<NasdaqHistoricalObservedEntrySelection.Missing>(
            Selector.Select(missing, setup.Fact)).UnavailableSourceObservations);
        var available = Entry(setup.Fact, source: "available:review");
        var mixed = WithoutFrame(Frame(setup.Fact.Direction, M5Fixture.At(15, 5), setup.Realignment, entry, available), M5Fixture.Minute);
        Assert.Single(Assert.IsType<NasdaqHistoricalObservedEntrySelection.Unique>(Selector.Select(mixed, setup.Fact)).UnavailableSourceObservations);
        var altered = new Candle(entry.ProviderId, entry.Symbol, Minute(20).Timeframe,
            Minute(20).OpenTimeUtc, Minute(20).CloseTimeUtc, 100, 120, 90, 100, Minute(20).Volume);
        var bad = Entry(setup.Fact, candles: [altered]);
        var unusable = Assert.IsType<NasdaqHistoricalObservedEntrySelection.Missing>(Selector.Select(
            Frame(setup.Fact.Direction, M5Fixture.At(15, 5), setup.Realignment, bad), setup.Fact));
        Assert.Empty(unusable.UnavailableSourceObservations);
    }

    [Fact]
    public void TerminalLifecycleAndCanonicalWindowPreventRevival()
    {
        var setup = Setup(NasdaqHumanH4PermittedDirection.Buy);
        var entry = Entry(setup.Fact, effective: M5Fixture.At(15));
        var initiating = setup.Fact.Realignment.Pullback.Selection.Fact.ApprovedQuality.Fact.Fvg.Trigger.DecisiveTake;
        var invalid = M5Fixture.Take(true, effective: M5Fixture.At(14, 30), observed: M5Fixture.At(14, 30));
        var terminal = Frame(setup.Fact.Direction, M5Fixture.At(15, 5), setup.Realignment, entry, invalid,
            new NasdaqHumanRelevantLiquidityTakeObservation(invalid, invalid.ObservedAtUtc, "terminal:record", initiating));
        Assert.IsType<NasdaqHistoricalObservedEntrySelection.Missing>(Selector.Select(terminal, setup.Fact));
        // Transport a known canonical terminal gate into a context that already carries exact eligibility.
        var valid = Frame(setup.Fact.Direction, M5Fixture.At(15, 5), setup.Realignment, entry);
        Assert.IsType<NasdaqHistoricalObservedEntrySelection.Unique>(Selector.Select(valid, setup.Fact));
        var gate = terminal.PriorObservations.Last().RuleFacts.Single(f => f.RuleId.Value == "NQ-LIQ-003");
        var last = valid.PriorObservations.Last();
        var gated = valid.PriorObservations.SkipLast(1).Append(new StrategyReplayContextObservation(
            last.StrategyId, last.StrategyVersion, last.ProviderId, last.Symbol, last.Step, last.AsOfUtc,
            last.Evaluations, last.WorkflowProgression, last.LifecycleProgression,
            ruleFacts: last.RuleFacts.Where(f => f.RuleId.Value != "NQ-LIQ-003").Append(gate)));
        Assert.IsType<NasdaqHistoricalObservedEntrySelection.Missing>(Selector.Select(valid.WithPriorObservations(gated), setup.Fact));
        var cutoff = Entry(setup.Fact, effective: M5Fixture.At(16), observed: M5Fixture.At(16));
        Assert.IsType<NasdaqHistoricalObservedEntrySelection.Missing>(Selector.Select(
            Frame(setup.Fact.Direction, M5Fixture.At(16), setup.Realignment, cutoff), setup.Fact));
    }

    [Fact]
    public void ConstructorsValidateCausalityUtcProvenanceAndDocumentedQuantity()
    {
        var setup = Setup(NasdaqHumanH4PermittedDirection.Buy);
        Assert.Throws<ArgumentException>(() => Entry(setup.Fact, effective: setup.Fact.EligibilityEffectiveAtUtc.AddTicks(-1)));
        Assert.Throws<ArgumentException>(() => Entry(setup.Fact, effective: M5Fixture.At(15).ToOffset(TimeSpan.FromHours(1))));
        Assert.Throws<ArgumentException>(() => Entry(setup.Fact, observed: M5Fixture.At(15).ToOffset(TimeSpan.FromHours(1))));
        Assert.Throws<ArgumentException>(() => Entry(setup.Fact, observed: setup.Fact.EligibilityEffectiveAtUtc.AddTicks(-1)));
        Assert.Throws<ArgumentException>(() => Entry(setup.Fact, id: " "));
        Assert.Throws<ArgumentException>(() => Entry(setup.Fact, source: " "));
        Assert.Throws<ArgumentException>(() => new NasdaqHistoricalObservedEntry(setup.Fact, "execution:1", 1,
            setup.Fact.EligibilityEffectiveAtUtc, " "));
        Assert.Throws<ArgumentException>(() => Entry(setup.Fact, quantity: 0, unit: "contracts"));
        Assert.Throws<ArgumentException>(() => Entry(setup.Fact, quantity: 1));
        Assert.Throws<ArgumentException>(() => Entry(setup.Fact, unit: "contracts"));
        Assert.Throws<ArgumentException>(() => Entry(setup.Fact, candles: [M5Fixture.Five(M5Fixture.At(15, 5))]));
    }

    [Fact]
    public void EntryEvidenceHasNoCanonicalRuleOrExecutionSideEffectsAndCoverageIsUnchanged()
    {
        var setup = Setup(NasdaqHumanH4PermittedDirection.Buy);
        var baseline = Run(Inputs(setup.Fact.Direction, setup.Realignment));
        var withEntry = Run(Inputs(setup.Fact.Direction, setup.Realignment, Entry(setup.Fact)));
        Assert.Equal(baseline.StrategyObservations.SelectMany(o => o.Evaluations), withEntry.StrategyObservations.SelectMany(o => o.Evaluations));
        Assert.Equal(baseline.StrategyObservations.SelectMany(o => o.RuleFacts).Select(f => JsonSerializer.Serialize(f.Fact, f.Fact.GetType())),
            withEntry.StrategyObservations.SelectMany(o => o.RuleFacts).Select(f => JsonSerializer.Serialize(f.Fact, f.Fact.GetType())));
        var report = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(),
            MoneyWayReplayRuleEvaluators.GetAll(), MoneyWayReplayEvaluationCapabilityDeclarations.GetAll())
            .Find(LiquidityFixture.Definition.StrategyId, LiquidityFixture.Definition.Version)!;
        Assert.Equal((32, 14, 16, 0, true), (report.TotalRuleCount, report.RequiredRuleCount, report.ImplementedCount,
            report.RequiredEvaluatorGapCount, report.HasFullRequiredEvaluatorRegistration));
    }

    private sealed record LaterTradeInformation(NasdaqDemoSessionIdentity Session, DateTimeOffset ObservedAtUtc,
        string SourceReference, bool ReportedTakeProfitHit, bool ReportedStopLossHit, string ReportedOutcome)
        : IStrategyReplayInputObservation
    {
        public StrategyId StrategyId => Session.StrategyId;
        public StrategyVersion StrategyVersion => Session.StrategyVersion;
        public MarketDataProviderId ProviderId => Session.ProviderId;
        public MarketSymbol Symbol => Session.Symbol;
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

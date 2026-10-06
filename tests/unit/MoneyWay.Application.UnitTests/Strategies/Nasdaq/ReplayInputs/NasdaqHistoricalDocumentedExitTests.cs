using System.Text.Json;
using MoneyWay.Application.Backtesting;
using MoneyWay.Application.MarketData.PriceLevels;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay.Capabilities;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;
namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHistoricalDocumentedExitTests
{
    private static readonly NasdaqHistoricalTradeSnapshotAssembler Assembler = new();
    private static readonly NasdaqHistoricalTradeLevelContactCollector Collector = new();
    private static readonly NasdaqHistoricalTradeContactOrderResolver Resolver = new();
    private static readonly NasdaqHistoricalDocumentedExitObservationSelector Selector = new();

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

    private static NasdaqHumanTakeProfitObservation Target(NasdaqHumanStructuralStopLossRuleFact sl,
        NasdaqHumanTakeProfitTarget? target = null, DateTimeOffset? effective = null, DateTimeOffset? observed = null,
        string source = "review:selected TP") => new(sl, target ?? SessionTarget(sl.Direction == NasdaqHumanH4PermittedDirection.Sell),
            effective ?? M5Fixture.At(15), observed ?? M5Fixture.At(15), source);

    private static NasdaqRiskExposureObservation Risk(NasdaqPreEntryEligibilityRuleFact entry,
        NasdaqHumanStructuralStopLossRuleFact sl, decimal loss = 100) => new(entry, sl,
            new(10000, "USD", "documented balance", loss, 100, "documented costs", "risk:calculation", 2, "contracts"),
            M5Fixture.At(15), M5Fixture.At(15), "risk:review");

    private static NasdaqHistoricalObservedEntryObservation Entry(NasdaqPreEntryEligibilityRuleFact eligibility,
        decimal price = 100, DateTimeOffset? observed = null, IEnumerable<Candle>? sources = null) => new(
            new(eligibility, "mentor:execution:1", price, eligibility.EligibilityEffectiveAtUtc, "entry:retained record",
                supportingCandles: sources), observed ?? M5Fixture.At(15), "entry:review");

    private static NasdaqHistoricalTradeSnapshot Snapshot(NasdaqHumanH4PermittedDirection direction = NasdaqHumanH4PermittedDirection.Buy,
        decimal? swing = null, DateTimeOffset? entryTime = null)
    {
        var setup = TargetSetup(direction, swing);
        var entry = entryTime is null ? Entry(setup.Entry) : new NasdaqHistoricalObservedEntryObservation(
            new(setup.Entry, "mentor:execution:1", 100, entryTime.Value, "entry:record"), entryTime.Value, "entry:review");
        var context = Frame(direction, entryTime ?? M5Fixture.At(15, 5), setup.Realignment,
            setup.Stop, Target(setup.Sl), Risk(setup.Entry, setup.Sl), entry);
        return Assert.IsType<NasdaqHistoricalTradeSnapshotResult.Available>(Assembler.Assemble(context, setup.Entry)).Snapshot;
    }

    private static Candle Source(DateTimeOffset open, DateTimeOffset close, decimal high = 109, decimal low = 91,
        Timeframe? timeframe = null) => new(LiquidityFixture.Provider, LiquidityFixture.Symbol,
            timeframe ?? M5Fixture.Minute, open, close, 100, high, low, 100, null);

    private static StrategyReplayContext Market(NasdaqHistoricalTradeSnapshot snapshot, IEnumerable<Candle> sources,
        DateTimeOffset? asOf = null, HistoricalMarketPriceObservation[]? prices = null)
    {
        var time = asOf ?? M5Fixture.At(15, 5);
        var candles = sources.Concat([Source(time.AddMinutes(-1), time)]).GroupBy(c => c.Timeframe)
            .Select(g => new CandleSeries(snapshot.Session.ProviderId, snapshot.Session.Symbol, g.Key,
                g.OrderBy(c => c.OpenTimeUtc))).ToArray();
        var cursor = new CanonicalMultiTimeframeReplayCursor(candles,
            new(snapshot.Session.ProviderId, snapshot.Session.Symbol, prices ?? []));
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == time) return new CreateStrategyReplayContextUseCase().ExecuteCanonical(LiquidityFixture.Definition, frame);
        throw new InvalidOperationException("Requested boundary was not produced.");
    }

    private static NasdaqHistoricalDocumentedExitObservation Exit(NasdaqHistoricalTradeSnapshot snapshot,
        decimal price = 105, DateTimeOffset? effective = null, DateTimeOffset? observed = null,
        string id = "mentor:close:1", string reference = "review:exit",
        NasdaqHistoricalEntryBeforeExitEvidence? proof = null, string? reason = null,
        NasdaqHistoricalDocumentedExitScope? scope = null, decimal? quantity = null, string? unit = null,
        IEnumerable<Candle>? sources = null, string record = "retained:close record") => new(
            new(snapshot, id, price, effective ?? snapshot.EntryEffectiveAtUtc.AddSeconds(30), record,
                proof, reason, scope, quantity, unit, sources), observed ?? M5Fixture.At(15, 5), reference);

    private static NasdaqHistoricalEntryBeforeExitEvidence Proof(NasdaqHistoricalTradeSnapshot snapshot,
        string evidence = "source:open-close relation", DateTimeOffset? observed = null,
        string reference = "retained:chronology", string exitId = "mentor:close:1") =>
        new(snapshot, exitId, evidence, observed ?? M5Fixture.At(15, 5), reference);

    private static StrategyReplayContext Context(NasdaqHistoricalTradeSnapshot snapshot,
        DateTimeOffset? time = null, params IStrategyReplayInputObservation[] inputs) =>
        WithInputs(Market(snapshot, [], time ?? snapshot.AsOfUtc), inputs);

    [Fact]
    public void MissingRetainsNoInventedExecution()
    {
        var snapshot = Snapshot();
        var result = Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Missing>(Selector.Select(Context(snapshot), snapshot));
        Assert.Empty(result.UnavailableSourceObservations);
        Assert.Empty(result.UnresolvedCausalityObservations);
    }

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell)]
    public void StrictlyLaterExitPreservesLiteralEventAndExactSnapshot(NasdaqHumanH4PermittedDirection direction)
    {
        var snapshot = Snapshot(direction);
        var observation = Exit(snapshot, price: 123.456m);
        var result = Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Unique>(Selector.Select(
            Context(snapshot, inputs: [observation]), snapshot));
        Assert.Same(observation, result.Fact);
        Assert.Same(snapshot, result.Fact.Snapshot);
        Assert.Equal(123.456m, result.Fact.Exit.ExitPrice);
        Assert.Equal(snapshot.EntryEffectiveAtUtc.AddSeconds(30), result.Fact.Exit.ExitEffectiveAtUtc);
        Assert.NotEqual(snapshot.AsOfUtc, result.Fact.Exit.ExitEffectiveAtUtc);
        Assert.Null(result.Fact.Exit.EntryBeforeExitEvidence);
        Assert.Equal("mentor:close:1", result.Fact.Exit.ExecutionId);
        Assert.Equal("retained:close record", result.Fact.Exit.ExecutionSourceReference);
    }

    [Fact]
    public void EqualTimestampWithAuthoritativeRelationshipIsUnique()
    {
        var snapshot = Snapshot();
        var proof = Proof(snapshot);
        var observation = Exit(snapshot, effective: snapshot.EntryEffectiveAtUtc, proof: proof);
        var result = Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Unique>(Selector.Select(
            Context(snapshot, inputs: [observation]), snapshot));
        Assert.Same(proof, result.Fact.Exit.EntryBeforeExitEvidence);
        Assert.Equal(snapshot.Entry.ExecutionId, proof.EntryExecutionId);
        Assert.Equal(observation.Exit.ExecutionId, proof.ExitExecutionId);
        Assert.Equal("source:open-close relation", proof.EvidenceId);
        Assert.Equal("retained:chronology", proof.SourceReference);
    }

    [Fact]
    public void EqualTimestampWithoutProofIsPreservedForHumanValidation()
    {
        var snapshot = Snapshot();
        var observation = Exit(snapshot, effective: snapshot.EntryEffectiveAtUtc);
        var result = Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Missing>(Selector.Select(
            Context(snapshot, inputs: [observation]), snapshot));
        Assert.Same(observation, Assert.Single(result.UnresolvedCausalityObservations));
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHistoricalDocumentedExitObservation>)result.UnresolvedCausalityObservations).Clear());
    }

    [Fact]
    public void UnresolvedAlternativeCannotBeDisplacedByResolvedAssertion()
    {
        var snapshot = Snapshot();
        var unknown = Exit(snapshot, effective: snapshot.EntryEffectiveAtUtc);
        var resolved = Exit(snapshot, effective: snapshot.EntryEffectiveAtUtc, proof: Proof(snapshot));
        var result = Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Conflict>(Selector.Select(
            Context(snapshot, inputs: [unknown, resolved]), snapshot));
        Assert.Equal(2, result.Alternatives.Count);
        Assert.Same(unknown, Assert.Single(result.UnresolvedCausalityObservations));
    }

    [Fact]
    public void ConflictingUnresolvedAssertionsPreserveBothPriceConflictAndUnknownOrder()
    {
        var snapshot = Snapshot();
        var a = Exit(snapshot, effective: snapshot.EntryEffectiveAtUtc);
        var b = Exit(snapshot, price: 999, effective: snapshot.EntryEffectiveAtUtc);
        var result = Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Conflict>(Selector.Select(
            Context(snapshot, inputs: [b, a]), snapshot));
        Assert.Equal(2, result.Alternatives.Count);
        Assert.Equal(2, result.UnresolvedCausalityObservations.Count);
        Assert.Equal(result.Alternatives, result.UnresolvedCausalityObservations);
    }

    [Fact]
    public void ExitBeforeEntryIsInvalidAtConstruction()
    {
        var snapshot = Snapshot();
        Assert.Throws<ArgumentException>(() => Exit(snapshot, effective: snapshot.EntryEffectiveAtUtc.AddTicks(-1)));
    }

    [Fact]
    public void CompatibleSupportsRetainAllProvenanceAndAreImmutableAndOrdered()
    {
        var snapshot = Snapshot();
        var a = Exit(snapshot, effective: snapshot.EntryEffectiveAtUtc, proof: Proof(snapshot));
        var b = Exit(snapshot, effective: snapshot.EntryEffectiveAtUtc,
            proof: Proof(snapshot, reference: "second:relationship record"), reference: "second:review", record: "second:execution record");
        var first = Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Unique>(Selector.Select(Context(snapshot, inputs: [a, b]), snapshot));
        var reverse = Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Unique>(Selector.Select(Context(snapshot, inputs: [b, a]), snapshot));
        Assert.Equal(first.SupportingObservations, reverse.SupportingObservations);
        Assert.Equal(2, first.SupportingObservations.Count);
        Assert.Contains(a, first.SupportingObservations);
        Assert.Contains(b, first.SupportingObservations);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHistoricalDocumentedExitObservation>)first.SupportingObservations).Clear());
    }

    [Theory]
    [InlineData("price")]
    [InlineData("time")]
    [InlineData("execution")]
    [InlineData("reason")]
    [InlineData("scope")]
    [InlineData("quantity")]
    [InlineData("proof")]
    public void IncompatibleFactsConflictWithoutAChosenWinner(string field)
    {
        var snapshot = Snapshot();
        var time = snapshot.EntryEffectiveAtUtc;
        var a = Exit(snapshot, effective: time, proof: Proof(snapshot), reason: "source-stated reason",
            scope: NasdaqHistoricalDocumentedExitScope.Full, quantity: 2, unit: "contracts");
        var id = field == "execution" ? "other:close" : a.Exit.ExecutionId;
        var b = Exit(snapshot, price: field == "price" ? 106 : 105,
            effective: field == "time" ? time.AddSeconds(1) : time, id: id,
            proof: Proof(snapshot, evidence: field == "proof" ? "other:relationship" : "source:open-close relation", exitId: id),
            reason: field == "reason" ? "different stated reason" : "source-stated reason",
            scope: field == "scope" ? NasdaqHistoricalDocumentedExitScope.Partial : NasdaqHistoricalDocumentedExitScope.Full,
            quantity: field == "quantity" ? 1 : 2, unit: "contracts");
        var first = Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Conflict>(Selector.Select(Context(snapshot, inputs: [a, b]), snapshot));
        var reverse = Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Conflict>(Selector.Select(Context(snapshot, inputs: [b, a]), snapshot));
        Assert.Equal(first.Alternatives, reverse.Alternatives);
        Assert.Equal(2, first.Alternatives.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHistoricalDocumentedExitObservation>)first.Alternatives).Clear());
    }

    [Fact]
    public void NumericallyIdenticalSnapshotCannotRebindAnExitOrProof()
    {
        var a = Snapshot();
        var b = Snapshot();
        Assert.Equal(a.EntryPrice, b.EntryPrice);
        Assert.Equal(a.EntryEffectiveAtUtc, b.EntryEffectiveAtUtc);
        Assert.Equal(a.StopPrice, b.StopPrice);
        Assert.Equal(a.TakeProfitPrice, b.TakeProfitPrice);
        var exit = Exit(b);
        Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Missing>(Selector.Select(Context(a, inputs: [exit]), a));
        Assert.Throws<ArgumentException>(() => Exit(a, effective: a.EntryEffectiveAtUtc, proof: Proof(b)));
        Assert.Throws<ArgumentException>(() => Exit(a, effective: a.EntryEffectiveAtUtc, proof: Proof(a, exitId: "different:exit")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExplicitPriceEqualityDoesNotInferReasonScopeOrQuantity(bool target)
    {
        var snapshot = Snapshot();
        var price = target ? snapshot.TakeProfitPrice : snapshot.StopPrice;
        var exit = Exit(snapshot, price: price);
        var result = Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Unique>(Selector.Select(Context(snapshot, inputs: [exit]), snapshot));
        Assert.Equal(price, result.Fact.Exit.ExitPrice);
        Assert.Null(result.Fact.Exit.DocumentedReason);
        Assert.Null(result.Fact.Exit.DocumentedScope);
        Assert.Null(result.Fact.Exit.Quantity);
        Assert.Null(result.Fact.Exit.QuantityUnit);
    }

    [Fact]
    public void OptionalSourceFactsAreLiteralWithoutSizeReconciliationOrAggregation()
    {
        var snapshot = Snapshot();
        var exit = Exit(snapshot, reason: "manual close in source", scope: NasdaqHistoricalDocumentedExitScope.Partial,
            quantity: 123, unit: "documented source units");
        var result = Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Unique>(Selector.Select(Context(snapshot, inputs: [exit]), snapshot));
        Assert.Equal("manual close in source", result.Fact.Exit.DocumentedReason);
        Assert.Equal(NasdaqHistoricalDocumentedExitScope.Partial, result.Fact.Exit.DocumentedScope);
        Assert.Equal(123, result.Fact.Exit.Quantity);
        Assert.Equal("documented source units", result.Fact.Exit.QuantityUnit);
    }

    [Fact]
    public void AvailabilityAndLaterConflictsCannotBackfillEarlierSelection()
    {
        var snapshot = Snapshot();
        var time = snapshot.AsOfUtc;
        var exit = Exit(snapshot, effective: time.AddMinutes(-1), observed: time.AddMinutes(1));
        var conflict = Exit(snapshot, price: 999, observed: time.AddMinutes(2));
        var earlier = Context(snapshot, time, exit, conflict);
        var missing = Selector.Select(earlier, snapshot);
        Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Missing>(missing);
        Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Unique>(Selector.Select(Context(snapshot, time.AddMinutes(1), exit, conflict), snapshot));
        Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Conflict>(Selector.Select(Context(snapshot, time.AddMinutes(2), exit, conflict), snapshot));
        Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Missing>(Selector.Select(earlier, snapshot));
        Assert.Empty(missing.UnresolvedCausalityObservations);
    }

    [Fact]
    public void LaterCausalProofCannotBecomeEarlierKnowledge()
    {
        var snapshot = Snapshot();
        var time = snapshot.AsOfUtc;
        var proof = Proof(snapshot, observed: time.AddMinutes(1));
        Assert.Throws<ArgumentException>(() => Exit(snapshot, effective: snapshot.EntryEffectiveAtUtc, observed: time, proof: proof));
        var exit = Exit(snapshot, effective: snapshot.EntryEffectiveAtUtc, observed: proof.ObservedAtUtc, proof: proof);
        Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Missing>(Selector.Select(Context(snapshot, time, exit), snapshot));
        Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Unique>(Selector.Select(Context(snapshot, proof.ObservedAtUtc, exit), snapshot));
    }

    [Fact]
    public void FutureSnapshotCannotBeConsumedEvenWithVisibleExit()
    {
        var snapshot = Snapshot();
        var exit = Exit(snapshot, observed: snapshot.EntryEffectiveAtUtc.AddMinutes(1));
        Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Missing>(Selector.Select(
            Context(snapshot, snapshot.AsOfUtc.AddMinutes(-1), exit), snapshot));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingRequiredMarketSourceIsRetainedIncludingUnresolvedCausality(bool unresolved)
    {
        var snapshot = Snapshot();
        var candle = Minute(20);
        var sources = new List<Candle> { candle };
        var exit = Exit(snapshot, effective: unresolved ? snapshot.EntryEffectiveAtUtc : null, sources: sources);
        sources.Clear();
        var context = Market(snapshot, []);
        context = WithInputs(context, exit);
        var result = Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Missing>(Selector.Select(context, snapshot));
        Assert.Same(exit, Assert.Single(result.UnavailableSourceObservations));
        Assert.Equal(unresolved ? 1 : 0, result.UnresolvedCausalityObservations.Count);
        Assert.Single(exit.Exit.SupportingCandles);
        Assert.Throws<NotSupportedException>(() => ((IList<Candle>)exit.Exit.SupportingCandles).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHistoricalDocumentedExitObservation>)result.UnavailableSourceObservations).Clear());
    }

    [Fact]
    public void MatchingMarketSourceSupportsExitButMismatchingPayloadIsUnusable()
    {
        var snapshot = Snapshot();
        var source = Minute(20);
        var exit = Exit(snapshot, sources: [source]);
        Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Unique>(Selector.Select(
            WithInputs(Market(snapshot, [source]), exit), snapshot));
        var changed = new Candle(source.ProviderId, source.Symbol, source.Timeframe, source.OpenTimeUtc, source.CloseTimeUtc,
            source.Open, source.High + 1, source.Low, source.Close, source.Volume);
        var result = Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Missing>(Selector.Select(
            WithInputs(Market(snapshot, [changed]), exit), snapshot));
        Assert.Empty(result.UnavailableSourceObservations);
    }

    private static StrategyReplayContext WithInputs(StrategyReplayContext context, params IStrategyReplayInputObservation[] inputs)
    {
        var frames = context.AvailableTimeframes.ToDictionary(t => t, t => { context.TryGetFrame(t, out var frame); return frame!; });
        return new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition,
            new MultiTimeframeReplayFrame(context.ProviderId, context.Symbol, context.Step, context.AsOfUtc,
                context.ConfiguredTimeframes, context.UpdatedTimeframes, frames), inputs).WithPriorObservations(context.PriorObservations);
    }

    [Theory]
    [InlineData(NasdaqHistoricalTradeLevelRole.TakeProfit)]
    [InlineData(NasdaqHistoricalTradeLevelRole.StopLoss)]
    public void EarliestProvenContactNeverSubstitutesForDocumentedExit(NasdaqHistoricalTradeLevelRole role)
    {
        var snapshot = Snapshot();
        var source = Source(M5Fixture.At(15, 3), M5Fixture.At(15, 4),
            high: role == NasdaqHistoricalTradeLevelRole.TakeProfit ? 111 : 109,
            low: role == NasdaqHistoricalTradeLevelRole.StopLoss ? 89 : 91);
        var context = Market(snapshot, [source]);
        var contacts = Collector.Collect(context, snapshot);
        var resolution = Assert.IsType<NasdaqHistoricalTradeContactResolution.EarliestProvenContact>(Resolver.Resolve(snapshot, contacts, context.AsOfUtc));
        Assert.Equal(role, resolution.FirstContact.Role);
        Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Missing>(Selector.Select(context, snapshot));
    }

    [Fact]
    public void AmbiguousSameCandleContactsNeverCreateAnExit()
    {
        var snapshot = Snapshot();
        var context = Market(snapshot, [Source(M5Fixture.At(15, 3), M5Fixture.At(15, 4), high: 111, low: 89)]);
        var contacts = Collector.Collect(context, snapshot);
        Assert.IsType<NasdaqHistoricalTradeContactResolution.OrderingUnresolved>(Resolver.Resolve(snapshot, contacts, context.AsOfUtc));
        Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Missing>(Selector.Select(context, snapshot));
    }

    [Fact]
    public void UniqueAndConflictRetainUnavailableSourceAlternatives()
    {
        var snapshot = Snapshot();
        var usable = Exit(snapshot);
        var unavailable = Exit(snapshot, sources: [Minute(20)], reference: "missing:source");
        var context = Context(snapshot, inputs: [usable, unavailable]);
        var unique = Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Unique>(Selector.Select(context, snapshot));
        Assert.Same(unavailable, Assert.Single(unique.UnavailableSourceObservations));
        var conflict = Exit(snapshot, price: 999);
        var result = Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Conflict>(Selector.Select(
            Context(snapshot, inputs: [usable, unavailable, conflict]), snapshot));
        Assert.Same(unavailable, Assert.Single(result.UnavailableSourceObservations));
    }

    [Fact]
    public void PostEntryExitAfterOperationalCutoffDoesNotRerunEntryGateOrForceExit()
    {
        var snapshot = Snapshot();
        var exit = Exit(snapshot, effective: M5Fixture.At(16, 1), observed: M5Fixture.At(16, 5));
        var result = Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Unique>(Selector.Select(
            Context(snapshot, M5Fixture.At(16, 5), exit), snapshot));
        Assert.Equal(M5Fixture.At(16, 1), result.Fact.Exit.ExitEffectiveAtUtc);
    }

    [Fact]
    public void LaterOutcomeAnnotationsAreNotExitEvidenceAndDoNotChangeHistoricalSelection()
    {
        var snapshot = Snapshot();
        var time = snapshot.AsOfUtc;
        var annotation = new LaterTradeInformation(snapshot.Session, time.AddMinutes(1), "later:outcome", "claimed profit");
        Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Missing>(Selector.Select(Context(snapshot, time, annotation), snapshot));
        Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Missing>(Selector.Select(Context(snapshot, annotation.ObservedAtUtc, annotation), snapshot));
        var exit = Exit(snapshot);
        var original = Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Unique>(Selector.Select(Context(snapshot, time, exit), snapshot));
        var extended = Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Unique>(Selector.Select(Context(snapshot, time, exit, annotation), snapshot));
        Assert.Equal(original.SupportingObservations, extended.SupportingObservations);
    }

    private sealed record LaterTradeInformation(NasdaqDemoSessionIdentity Session, DateTimeOffset ObservedAtUtc,
        string SourceReference, string ReportedOutcome) : IStrategyReplayInputObservation
    {
        public StrategyId StrategyId => Session.StrategyId;
        public StrategyVersion StrategyVersion => Session.StrategyVersion;
        public MarketDataProviderId ProviderId => Session.ProviderId;
        public MarketSymbol Symbol => Session.Symbol;
    }

    [Fact]
    public void ExitInputDoesNotModifyCanonicalRuleEvaluationsOrFacts()
    {
        var setup = TargetSetup();
        var inputs = Inputs(setup.Entry.Direction, setup.Realignment, setup.Stop, Target(setup.Sl), Risk(setup.Entry, setup.Sl), Entry(setup.Entry));
        var snapshot = Snapshot();
        var baseline = Run(inputs);
        var withExit = Run(inputs.Concat([Exit(snapshot)]).ToArray());
        Assert.Equal(baseline.StrategyObservations.SelectMany(o => o.Evaluations), withExit.StrategyObservations.SelectMany(o => o.Evaluations));
        Assert.Equal(baseline.StrategyObservations.SelectMany(o => o.RuleFacts).Select(f => JsonSerializer.Serialize(f.Fact, f.Fact.GetType())),
            withExit.StrategyObservations.SelectMany(o => o.RuleFacts).Select(f => JsonSerializer.Serialize(f.Fact, f.Fact.GetType())));
    }

    [Fact]
    public void InvalidTimestampsProvenanceQuantityAndSourceAreRejected()
    {
        var snapshot = Snapshot();
        Assert.Throws<ArgumentException>(() => Exit(snapshot, effective: snapshot.EntryEffectiveAtUtc.ToOffset(TimeSpan.FromHours(1))));
        Assert.Throws<ArgumentException>(() => Exit(snapshot, observed: snapshot.EntryEffectiveAtUtc.AddTicks(-1)));
        Assert.Throws<ArgumentException>(() => Exit(snapshot, id: " "));
        Assert.Throws<ArgumentException>(() => Exit(snapshot, record: " "));
        Assert.Throws<ArgumentException>(() => Exit(snapshot, reference: " "));
        Assert.Throws<ArgumentException>(() => Exit(snapshot, reason: " "));
        Assert.Throws<ArgumentException>(() => Exit(snapshot, scope: (NasdaqHistoricalDocumentedExitScope)999));
        Assert.Throws<ArgumentException>(() => Exit(snapshot, quantity: 0, unit: "contracts"));
        Assert.Throws<ArgumentException>(() => Exit(snapshot, quantity: 1));
        Assert.Throws<ArgumentException>(() => Exit(snapshot, unit: "contracts"));
        Assert.Throws<ArgumentException>(() => Proof(snapshot, evidence: " "));
        Assert.Throws<ArgumentException>(() => Proof(snapshot, observed: snapshot.EntryEffectiveAtUtc.ToOffset(TimeSpan.FromHours(1))));
        Assert.Throws<ArgumentException>(() => Exit(snapshot, sources: [null!]));
        Assert.Throws<ArgumentException>(() => Exit(snapshot, sources: [M5Fixture.Five(M5Fixture.At(15, 10))]));
        var source = Minute(20);
        var wrongSource = new Candle(new MarketDataProviderId("other"), source.Symbol, source.Timeframe,
            source.OpenTimeUtc, source.CloseTimeUtc, source.Open, source.High, source.Low, source.Close, null);
        Assert.Throws<ArgumentException>(() => Exit(snapshot, sources: [wrongSource]));
        Assert.Throws<ArgumentNullException>(() => Selector.Select(null!, snapshot));
        Assert.Throws<ArgumentNullException>(() => Selector.Select(Context(snapshot), null!));
    }

    [Fact]
    public void SelectionHasNoVerdictEconomicOrExecutionSideEffectsAndCoverageIsUnchanged()
    {
        var snapshot = Snapshot();
        var before = JsonSerializer.Serialize(snapshot);
        var observation = Exit(snapshot);
        var context = Context(snapshot, inputs: [observation]);
        var history = JsonSerializer.Serialize(context.PriorObservations);
        Selector.Select(context, snapshot);
        Assert.Equal(before, JsonSerializer.Serialize(snapshot));
        Assert.Equal(history, JsonSerializer.Serialize(context.PriorObservations));
        foreach (var type in new[] { typeof(NasdaqHistoricalDocumentedExit), typeof(NasdaqHistoricalEntryBeforeExitEvidence),
            typeof(NasdaqHistoricalDocumentedExitObservation), typeof(NasdaqHistoricalDocumentedExitSelection.Missing),
            typeof(NasdaqHistoricalDocumentedExitSelection.Unique), typeof(NasdaqHistoricalDocumentedExitSelection.Conflict) })
            Assert.All(type.GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.Empty(typeof(NasdaqHistoricalDocumentedExitObservationSelector).GetFields(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public));
        var report = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), MoneyWayReplayRuleEvaluators.GetAll(),
            MoneyWayReplayEvaluationCapabilityDeclarations.GetAll()).Find(LiquidityFixture.Definition.StrategyId, LiquidityFixture.Definition.Version)!;
        Assert.Equal((32, 14, 16, 0, true), (report.TotalRuleCount, report.RequiredRuleCount, report.ImplementedCount,
            report.RequiredEvaluatorGapCount, report.HasFullRequiredEvaluatorRegistration));
    }
}

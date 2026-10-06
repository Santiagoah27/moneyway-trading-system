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

public sealed class NasdaqHistoricalTradeContactOrderTests
{
    private static readonly NasdaqHistoricalTradeSnapshotAssembler Assembler = new();
    private static readonly NasdaqHistoricalTradeLevelContactCollector Collector = new();
    private static readonly NasdaqHistoricalTradeContactOrderResolver Resolver = new();

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

    [Fact]
    public void EmptyAndBeforeEntryOnlyHaveNoRelevantContactAndRetainDiagnostics()
    {
        var snapshot = Snapshot();
        Assert.Empty(Assert.IsType<NasdaqHistoricalTradeContactResolution.NoRelevantContacts>(
            Resolver.Resolve(snapshot, [], snapshot.AsOfUtc)).Contacts);
        var source = Source(snapshot.EntryEffectiveAtUtc.AddMinutes(-2), snapshot.EntryEffectiveAtUtc.AddMinutes(-1), 110, 89);
        var contacts = Collector.Collect(Market(snapshot, [source]), snapshot);
        var result = Assert.IsType<NasdaqHistoricalTradeContactResolution.NoRelevantContacts>(Resolver.Resolve(snapshot, contacts, snapshot.AsOfUtc));
        Assert.Equal(2, result.BeforeEntryContacts.Count);
        Assert.Empty(result.StrictlyPostEntryContacts);
        Assert.Null(result.EarliestStrictlyPostEntryContact);
    }

    [Theory]
    [InlineData(NasdaqHistoricalTradeLevelRole.StopLoss)]
    [InlineData(NasdaqHistoricalTradeLevelRole.TakeProfit)]
    public void SingleStrictContactIsEarliestProvenContactWithoutEconomicMeaning(NasdaqHistoricalTradeLevelRole role)
    {
        var snapshot = Snapshot();
        var source = Source(M5Fixture.At(14, 40), M5Fixture.At(14, 41), role == NasdaqHistoricalTradeLevelRole.TakeProfit ? 110 : 109,
            role == NasdaqHistoricalTradeLevelRole.StopLoss ? 89 : 91);
        var contacts = Collector.Collect(Market(snapshot, [source]), snapshot);
        var result = Assert.IsType<NasdaqHistoricalTradeContactResolution.EarliestProvenContact>(Resolver.Resolve(snapshot, contacts, snapshot.AsOfUtc));
        Assert.Same(Assert.Single(contacts), result.FirstContact);
        Assert.Equal(role, result.FirstContact.Role);
        Assert.Empty(result.PairwiseOrders);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DisjointContactsProveEitherLevelCanBeEarlier(bool tpFirst)
    {
        var snapshot = Snapshot();
        var a = Source(M5Fixture.At(14, 40), M5Fixture.At(14, 41), tpFirst ? 110 : 109, tpFirst ? 91 : 89);
        var b = Source(M5Fixture.At(14, 43), M5Fixture.At(14, 44), tpFirst ? 109 : 110, tpFirst ? 89 : 91);
        var contacts = Collector.Collect(Market(snapshot, [a, b]), snapshot);
        var result = Assert.IsType<NasdaqHistoricalTradeContactResolution.EarliestProvenContact>(Resolver.Resolve(snapshot, contacts.Reverse(), snapshot.AsOfUtc));
        Assert.Same(a, result.FirstContact.SourceCandle);
        Assert.Equal(NasdaqHistoricalContactOrder.Before, Assert.Single(result.PairwiseOrders).Order);
        Assert.Equal(2, result.Contacts.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SameSourceAndOverlappingDifferentIntervalsRemainUnresolved(bool differentIntervals)
    {
        var snapshot = Snapshot();
        var a = Source(M5Fixture.At(14, 40), M5Fixture.At(14, 45), 110, differentIntervals ? 91 : 89, new(5, TimeframeUnit.Minute));
        var sources = differentIntervals ? new[] { a, Source(M5Fixture.At(14, 42), M5Fixture.At(14, 43), low: 89) } : [a];
        var contacts = Collector.Collect(Market(snapshot, sources), snapshot);
        var result = Assert.IsType<NasdaqHistoricalTradeContactResolution.OrderingUnresolved>(Resolver.Resolve(snapshot, contacts, snapshot.AsOfUtc));
        Assert.Equal(2, result.Contacts.Count);
        Assert.Null(result.EarliestStrictlyPostEntryContact);
        Assert.Equal(NasdaqHistoricalContactOrder.Unresolved, Assert.Single(result.UnresolvedPairs).Order);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EntryOverlapDominatesLaterQualifiedStrictContact(bool overlapStop)
    {
        var snapshot = Snapshot();
        var entry = snapshot.EntryEffectiveAtUtc;
        var overlap = Source(entry.AddMinutes(-1), entry, overlapStop ? 109 : 110, overlapStop ? 89 : 91);
        var after = Source(M5Fixture.At(14, 40), M5Fixture.At(14, 41), overlapStop ? 110 : 109, overlapStop ? 91 : 89);
        var contacts = Collector.Collect(Market(snapshot, [overlap, after]), snapshot);
        var result = Assert.IsType<NasdaqHistoricalTradeContactResolution.EntryBoundaryAmbiguous>(Resolver.Resolve(snapshot, contacts, snapshot.AsOfUtc));
        Assert.Single(result.EntryBoundaryContacts);
        Assert.Single(result.StrictlyPostEntryContacts);
        Assert.Same(after, result.EarliestStrictlyPostEntryContact!.SourceCandle);
        Assert.Equal(2, result.Contacts.Count);
        // The qualified AfterEntry projection does not create an unqualified FirstContact result.
        Assert.IsNotType<NasdaqHistoricalTradeContactResolution.EarliestProvenContact>(result);
    }

    [Fact]
    public void BothEntryOverlappingLevelsRemainAmbiguousWithoutStrictProjection()
    {
        var snapshot = Snapshot();
        var source = Source(snapshot.EntryEffectiveAtUtc.AddMinutes(-1), snapshot.EntryEffectiveAtUtc, 110, 89);
        var result = Assert.IsType<NasdaqHistoricalTradeContactResolution.EntryBoundaryAmbiguous>(Resolver.Resolve(snapshot,
            Collector.Collect(Market(snapshot, [source]), snapshot), snapshot.AsOfUtc));
        Assert.Equal(2, result.EntryBoundaryContacts.Count);
        Assert.Null(result.EarliestStrictlyPostEntryContact);
        Assert.Single(result.UnresolvedPairs);
    }

    [Fact]
    public void PreEntryHistoryDoesNotBlockStrictContactAndRepeatedContactsArePreserved()
    {
        var snapshot = Snapshot();
        var before = Source(snapshot.EntryEffectiveAtUtc.AddMinutes(-2), snapshot.EntryEffectiveAtUtc.AddMinutes(-1), low: 89);
        var first = Source(M5Fixture.At(14, 40), M5Fixture.At(14, 41), 110);
        var later = Source(M5Fixture.At(14, 43), M5Fixture.At(14, 44), 110);
        var contacts = Collector.Collect(Market(snapshot, [before, first, later]), snapshot);
        var result = Assert.IsType<NasdaqHistoricalTradeContactResolution.EarliestProvenContact>(Resolver.Resolve(snapshot, contacts, snapshot.AsOfUtc));
        Assert.Single(result.BeforeEntryContacts);
        Assert.Equal(2, result.StrictlyPostEntryContacts.Count);
        Assert.Same(first, result.FirstContact.SourceCandle);
        Assert.Equal(3, result.PairwiseOrders.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AuthoritativePreciseSequenceIsComposedWithoutInventingEqualTimeOrder(bool ordered)
    {
        var snapshot = Snapshot();
        var at = M5Fixture.At(15, 6);
        var tp = new HistoricalMarketPriceObservation(snapshot.Session.ProviderId, snapshot.Session.Symbol, at, 110, "price", "100ms", ordered ? 1 : null);
        var sl = new HistoricalMarketPriceObservation(snapshot.Session.ProviderId, snapshot.Session.Symbol, at, 89, "price", "100ms", ordered ? 2 : null);
        var contacts = Collector.Collect(Market(snapshot, [], at, [tp, sl]), snapshot);
        var result = Resolver.Resolve(snapshot, contacts.Reverse(), at);
        if (ordered)
            Assert.Same(tp, Assert.IsType<NasdaqHistoricalTradeContactResolution.EarliestProvenContact>(result).FirstContact.SourcePrice);
        else
            Assert.Single(Assert.IsType<NasdaqHistoricalTradeContactResolution.OrderingUnresolved>(result).UnresolvedPairs);
    }

    [Fact]
    public void EarliestCanBeProvenWhileLaterContactsRemainIncomparable()
    {
        var snapshot = Snapshot();
        var early = Source(M5Fixture.At(14, 40), M5Fixture.At(14, 41), 110);
        var later = Source(M5Fixture.At(14, 43), M5Fixture.At(14, 44), 110, 89);
        var result = Assert.IsType<NasdaqHistoricalTradeContactResolution.EarliestProvenContact>(Resolver.Resolve(snapshot,
            Collector.Collect(Market(snapshot, [early, later]), snapshot), snapshot.AsOfUtc));
        Assert.Same(early, result.FirstContact.SourceCandle);
        Assert.Single(result.UnresolvedPairs);
        Assert.Equal(3, result.Contacts.Count);
    }

    [Fact]
    public void MixedSnapshotIdentitiesNullAndInvalidAvailabilityAreRejected()
    {
        var a = Snapshot();
        var b = Snapshot(swing: 98);
        var source = Source(M5Fixture.At(14, 40), M5Fixture.At(14, 41), 110);
        var first = Collector.Collect(Market(a, [source]), a);
        var second = Collector.Collect(Market(b, [source]), b);
        Assert.Throws<ArgumentException>(() => Resolver.Resolve(a, first.Concat(second), a.AsOfUtc));
        Assert.Throws<ArgumentException>(() => Resolver.Resolve(a, [null!], a.AsOfUtc));
        Assert.Throws<ArgumentException>(() => Resolver.Resolve(a, first, a.AsOfUtc.AddTicks(-1)));
        Assert.Throws<ArgumentException>(() => Resolver.Resolve(a, first, a.AsOfUtc.ToOffset(TimeSpan.FromHours(1))));
    }

    [Fact]
    public void ReorderedInputsProduceEquivalentResolutionAndCollectionsAreImmutableSnapshots()
    {
        var snapshot = Snapshot();
        var a = Source(M5Fixture.At(14, 40), M5Fixture.At(14, 41), 110, 89);
        var b = Source(M5Fixture.At(14, 43), M5Fixture.At(14, 44), 110);
        var input = Collector.Collect(Market(snapshot, [a, b]), snapshot).ToList();
        var first = Resolver.Resolve(snapshot, input, snapshot.AsOfUtc);
        var reverse = Resolver.Resolve(snapshot, input.AsEnumerable().Reverse(), snapshot.AsOfUtc);
        Assert.Equal(first.GetType(), reverse.GetType());
        Assert.Equal(first.Contacts, reverse.Contacts);
        Assert.Equal(first.PairwiseOrders.Select(p => (p.Left, p.Right, p.Order)), reverse.PairwiseOrders.Select(p => (p.Left, p.Right, p.Order)));
        input.Clear();
        Assert.Equal(3, first.Contacts.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHistoricalTradeLevelContact>)first.Contacts).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHistoricalTradeContactPairOrder>)first.PairwiseOrders).Clear());
        Assert.All(typeof(NasdaqHistoricalTradeContactResolution).GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void FutureAssertionsCannotAlterEarlierResolutionAndContactSupportIsNotDeleted()
    {
        var snapshot = Snapshot();
        var early = Source(M5Fixture.At(15, 2), M5Fixture.At(15, 3), 110);
        var future = Source(M5Fixture.At(15, 6), M5Fixture.At(15, 7), 110, 89);
        var current = Collector.Collect(Market(snapshot, [early]), snapshot);
        var later = Collector.Collect(Market(snapshot, [future], M5Fixture.At(15, 8)), snapshot);
        var input = current.Concat(later).ToList();
        var atT = Assert.IsType<NasdaqHistoricalTradeContactResolution.EarliestProvenContact>(Resolver.Resolve(snapshot, input, snapshot.AsOfUtc));
        Assert.Single(atT.Contacts);
        var atNext = Resolver.Resolve(snapshot, input, M5Fixture.At(15, 8));
        Assert.Equal(3, atNext.Contacts.Count);
        Assert.Single(atT.Contacts);
        Assert.Same(Assert.Single(current), atT.FirstContact);
    }

    [Fact]
    public void ResolutionDoesNotMutateCanonicalFactsOrCapabilityCoverage()
    {
        var snapshot = Snapshot();
        var proof = JsonSerializer.Serialize(snapshot);
        Resolver.Resolve(snapshot, [], snapshot.AsOfUtc);
        Assert.Equal(proof, JsonSerializer.Serialize(snapshot));
        var report = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), MoneyWayReplayRuleEvaluators.GetAll(),
            MoneyWayReplayEvaluationCapabilityDeclarations.GetAll()).Find(LiquidityFixture.Definition.StrategyId, LiquidityFixture.Definition.Version)!;
        Assert.Equal((32, 14, 16, 0, true), (report.TotalRuleCount, report.RequiredRuleCount, report.ImplementedCount,
            report.RequiredEvaluatorGapCount, report.HasFullRequiredEvaluatorRegistration));
    }
}

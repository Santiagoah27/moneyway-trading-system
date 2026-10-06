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

public sealed class NasdaqHistoricalTradeLevelContactTests
{
    private static readonly NasdaqHistoricalTradeSnapshotAssembler Assembler = new();
    private static readonly NasdaqHistoricalTradeLevelContactCollector Collector = new();

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

    private static NasdaqHumanTakeProfitTarget StructuralTarget(bool low, int hours, decimal price = 100,
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

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy, NasdaqHistoricalTradeLevelRole.StopLoss)]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy, NasdaqHistoricalTradeLevelRole.TakeProfit)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell, NasdaqHistoricalTradeLevelRole.StopLoss)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell, NasdaqHistoricalTradeLevelRole.TakeProfit)]
    public void ExactInclusiveDirectionalLevelContactPreservesSourceAndSnapshot(NasdaqHumanH4PermittedDirection direction,
        NasdaqHistoricalTradeLevelRole role)
    {
        var snapshot = Snapshot(direction);
        var level = role == NasdaqHistoricalTradeLevelRole.StopLoss ? snapshot.StopPrice : snapshot.TakeProfitPrice;
        var lower = (role == NasdaqHistoricalTradeLevelRole.StopLoss) == (direction == NasdaqHumanH4PermittedDirection.Buy);
        var source = Source(M5Fixture.At(15, 2), M5Fixture.At(15, 3), lower ? 109 : level, lower ? level : 91);
        var context = Market(snapshot, [source]);
        var contact = Assert.Single(Collector.Collect(context, snapshot));
        Assert.Same(snapshot, contact.Snapshot);
        Assert.Same(source, contact.SourceCandle);
        Assert.Equal(role, contact.Role);
        Assert.Equal(level, contact.LevelPrice);
        Assert.Equal(source.Timeframe, contact.SourceTimeframe);
        Assert.Equal((source.OpenTimeUtc, source.CloseTimeUtc),
            (contact.EvidenceWindow.EarliestPossibleUtc, contact.EvidenceWindow.LatestPossibleUtc));
        Assert.False(contact.HasExactTimestamp);
        Assert.Equal(context.AsOfUtc, contact.ObservedAtUtc);
        Assert.Equal(source.CloseTimeUtc, contact.SourceAvailableAtUtc);
        Assert.Equal(NasdaqHistoricalContactEntryRelation.AfterEntry, contact.EntryRelation);

    }

    [Fact]
    public void NoContactAndNearLevelsHaveNoToleranceOrEconomicStatus()
    {
        var snapshot = Snapshot();
        var source = Source(M5Fixture.At(15, 2), M5Fixture.At(15, 3), snapshot.TakeProfitPrice - 0.0001m, snapshot.StopPrice + 0.0001m);
        Assert.Empty(Collector.Collect(Market(snapshot, [source]), snapshot));
    }

    [Fact]
    public void SameCandleBothLevelsAndOverlappingTimeframesHaveUnresolvedOrder()
    {
        var snapshot = Snapshot();
        var source = Source(M5Fixture.At(15), M5Fixture.At(15, 5), 110, 89, new(5, TimeframeUnit.Minute));
        var broader = Source(M5Fixture.At(14, 5), M5Fixture.At(15, 5), 110, 89, new(1, TimeframeUnit.Hour));
        var contacts = Collector.Collect(Market(snapshot, [source, broader]), snapshot);
        Assert.Equal(4, contacts.Count);
        var same = contacts.Where(c => ReferenceEquals(c.SourceCandle, source)).ToArray();
        Assert.Equal(NasdaqHistoricalContactOrder.Unresolved, same[0].CompareOrder(same[1]));
        Assert.Equal(NasdaqHistoricalContactOrder.Unresolved, same[0].CompareOrder(contacts.First(c => ReferenceEquals(c.SourceCandle, broader))));
        Assert.Contains(contacts, c => c.SourceTimeframe == new Timeframe(1, TimeframeUnit.Hour));
        Assert.Contains(contacts, c => c.SourceTimeframe == new Timeframe(5, TimeframeUnit.Minute));
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHistoricalTradeLevelContact>)contacts).Clear());
    }

    [Fact]
    public void PreEntryBoundaryAndStrictlyPostEntryIntervalsRemainDistinct()
    {
        var snapshot = Snapshot();
        var entry = snapshot.EntryEffectiveAtUtc;
        var before = Source(entry.AddMinutes(-2), entry.AddMinutes(-1), 110);
        var endingAtEntry = Source(entry.AddMinutes(-1), entry, 110);
        var startingAtEntry = Source(entry, entry.AddMinutes(1), 110);
        var after = Source(entry.AddMinutes(2), entry.AddMinutes(3), 110);
        var contacts = Collector.Collect(Market(snapshot, [before, endingAtEntry, startingAtEntry, after]), snapshot);
        Assert.Equal(new[] { NasdaqHistoricalContactEntryRelation.BeforeEntry, NasdaqHistoricalContactEntryRelation.OverlapsEntryBoundary,
            NasdaqHistoricalContactEntryRelation.OverlapsEntryBoundary, NasdaqHistoricalContactEntryRelation.AfterEntry }, contacts.Select(c => c.EntryRelation));
        Assert.Equal(NasdaqHistoricalContactOrder.Before, contacts[0].CompareOrder(contacts[3]));
        Assert.Equal(NasdaqHistoricalContactOrder.After, contacts[3].CompareOrder(contacts[0]));
        Assert.Equal(NasdaqHistoricalContactOrder.Unresolved, contacts[1].CompareOrder(contacts[2]));
    }

    [Fact]
    public void RepeatedContactsInDifferentCandlesAreRetainedWithoutFirstExitPolicy()
    {
        var snapshot = Snapshot();
        var a = Source(M5Fixture.At(14, 40), M5Fixture.At(14, 41), 110);
        var b = Source(M5Fixture.At(14, 43), M5Fixture.At(14, 44), low: 89);
        var c = Source(M5Fixture.At(14, 46), M5Fixture.At(14, 47), 110);
        var contacts = Collector.Collect(Market(snapshot, [a, b, c]), snapshot);
        var first = contacts.Single(x => ReferenceEquals(x.SourceCandle, a));
        var second = contacts.Single(x => ReferenceEquals(x.SourceCandle, b));
        Assert.Equal(NasdaqHistoricalContactOrder.Before, first.CompareOrder(second));
        Assert.Equal(2, contacts.Count(x => x.Role == NasdaqHistoricalTradeLevelRole.TakeProfit));
        Assert.Equal(3, contacts.Count);
    }

    [Fact]
    public void LaterCandleAvailabilityCannotBackfillOrMutateEarlierContacts()
    {
        var snapshot = Snapshot();
        var future = Source(M5Fixture.At(15, 6), M5Fixture.At(15, 7), 110, 89);
        var earlier = Market(snapshot, []);
        var withFuture = Market(snapshot, [future]);
        var original = Collector.Collect(earlier, snapshot);
        Assert.Empty(original);
        Assert.Empty(Collector.Collect(withFuture, snapshot));
        Assert.Equal(2, Collector.Collect(Market(snapshot, [future], M5Fixture.At(15, 8)), snapshot).Count);
        Assert.Empty(original);
    }

    private static HistoricalMarketPriceObservation Price(NasdaqHistoricalTradeSnapshot snapshot, DateTimeOffset time,
        decimal price, long? sequence = null) => new(snapshot.Session.ProviderId, snapshot.Session.Symbol,
            time, price, "documented-price", "100ms", sequence);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PrecisePricesRetainExactIdentityAndOnlyAuthoritativeSequenceProvesEqualTimeOrder(bool ordered)
    {
        var snapshot = Snapshot();
        var time = M5Fixture.At(15, 6);
        var a = Price(snapshot, time, snapshot.TakeProfitPrice, ordered ? 1 : null);
        var b = Price(snapshot, time, snapshot.StopPrice, ordered ? 2 : null);
        var context = Market(snapshot, [], time, [a, b]);
        var contacts = Collector.Collect(context, snapshot);
        Assert.Equal(2, contacts.Count);
        var tp = contacts.Single(c => c.Role == NasdaqHistoricalTradeLevelRole.TakeProfit);
        var sl = contacts.Single(c => c.Role == NasdaqHistoricalTradeLevelRole.StopLoss);
        Assert.Same(a, tp.SourcePrice);
        Assert.Same(b, sl.SourcePrice);
        Assert.Equal("100ms", tp.SourceResolution);
        Assert.True(tp.HasExactTimestamp);
        Assert.Equal((time, time), (tp.EvidenceWindow.EarliestPossibleUtc, tp.EvidenceWindow.LatestPossibleUtc));
        Assert.Equal(ordered ? NasdaqHistoricalContactOrder.Before : NasdaqHistoricalContactOrder.Unresolved, tp.CompareOrder(sl));
        Assert.Equal(ordered, tp.HasAuthoritativeSourceOrder);
        Assert.Equal(NasdaqHistoricalContactEntryRelation.AfterEntry, tp.EntryRelation);
        Assert.Empty(Collector.Collect(Market(snapshot, [], snapshot.AsOfUtc, [a, b]), snapshot));
    }

    [Fact]
    public void SourceContactsRemainBoundToExactSnapshotWithMatchingPricesAndDifferentAncestry()
    {
        var a = Snapshot();
        var b = Snapshot(swing: 98);
        var source = Source(M5Fixture.At(15, 2), M5Fixture.At(15, 3), 110);
        var one = Assert.Single(Collector.Collect(Market(a, [source]), a));
        var two = Assert.Single(Collector.Collect(Market(b, [source]), b));
        Assert.Same(a, one.Snapshot);
        Assert.Same(b, two.Snapshot);
        Assert.Throws<ArgumentException>(() => one.CompareOrder(two));
        Assert.Throws<ArgumentException>(() => Collector.Collect(Market(a, [], a.AsOfUtc.AddMinutes(-1)), a));
        Assert.All(typeof(NasdaqHistoricalTradeLevelContact).GetProperties(), property => Assert.Null(property.SetMethod));
    }
    [Fact]
    public void ExactPriceAtEntryPreservesUnresolvedEntryBoundaryPolicy()
    {
        var time = M5Fixture.At(15, 10);
        var snapshot = Snapshot(entryTime: time);
        var price = Price(snapshot, time, snapshot.TakeProfitPrice);
        var contact = Assert.Single(Collector.Collect(Market(snapshot, [], time, [price]), snapshot));
        Assert.True(contact.HasExactTimestamp);
        Assert.Equal(NasdaqHistoricalContactEntryRelation.OverlapsEntryBoundary, contact.EntryRelation);
    }

    [Fact]
    public void ContactCollectionIsDeterministicAndLeavesSnapshotAndCanonicalCoverageUnchanged()
    {
        var snapshot = Snapshot();
        var source = Source(M5Fixture.At(15, 2), M5Fixture.At(15, 3), 110, 89);
        var context = Market(snapshot, [source]);
        var proof = JsonSerializer.Serialize(snapshot);
        var first = Collector.Collect(context, snapshot);
        var second = Collector.Collect(context, snapshot);
        Assert.Equal(first.Select(c => (c.Role, c.LevelPrice, c.EvidenceWindow, c.ObservedAtUtc)),
            second.Select(c => (c.Role, c.LevelPrice, c.EvidenceWindow, c.ObservedAtUtc)));
        Assert.Equal(proof, JsonSerializer.Serialize(snapshot));
        var report = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(), MoneyWayReplayRuleEvaluators.GetAll(),
            MoneyWayReplayEvaluationCapabilityDeclarations.GetAll()).Find(LiquidityFixture.Definition.StrategyId, LiquidityFixture.Definition.Version)!;
        Assert.Equal((32, 14, 16, 0, true), (report.TotalRuleCount, report.RequiredRuleCount, report.ImplementedCount,
            report.RequiredEvaluatorGapCount, report.HasFullRequiredEvaluatorRegistration));
    }

}

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

public sealed class NasdaqHumanTakeProfitTests
{
    private static readonly NasdaqHumanTakeProfitObservationSelector Selector = new();

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

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy, false)]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy, true)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell, false)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell, true)]
    public void HumanSessionTargetIsUniqueWithExactPriceAncestryAndSelectionTime(NasdaqHumanH4PermittedDirection direction, bool london)
    {
        var setup = TargetSetup(direction);
        var target = Target(setup.Sl, SessionTarget(direction == NasdaqHumanH4PermittedDirection.Sell, london));
        var context = Frame(direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, target);
        var selection = Assert.IsType<NasdaqHumanTakeProfitSelection.Unique>(Selector.Select(context, setup.Sl));
        Assert.Same(target, selection.Fact);
        Assert.Same(setup.Sl, target.StopLoss);
        Assert.Same(setup.Sl.PreEntryEligibility, target.PreEntryEligibility);
        Assert.Equal(direction, target.Direction);
        Assert.Equal(target.Target.Reference.ReferencePrice, target.Target.TargetReferencePrice);
        Assert.Equal(target.Target.TargetReferencePrice, target.Target.TakeProfitPrice);
        Assert.Equal(M5Fixture.At(15), target.EffectiveAtUtc);
        Assert.NotEqual(context.AsOfUtc, target.EffectiveAtUtc);
        Assert.NotEqual(setup.Sl.EffectiveAtUtc, target.EffectiveAtUtc);
        Assert.NotEqual(((NasdaqLiquidityTakeReference.SessionLevel)target.Target.Reference).SelectedAtUtc, target.EffectiveAtUtc);
        Assert.Equal("review:selected TP", target.SourceReference);
        Assert.Empty(selection.UnavailableSourceObservations);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 4)]
    [InlineData(true, 1)]
    [InlineData(true, 4)]
    public void HumanReviewedStructuralTargetPreservesSourceCategoryAndExactCoordinate(bool low, int hours)
    {
        var setup = TargetSetup(low ? NasdaqHumanH4PermittedDirection.Sell : NasdaqHumanH4PermittedDirection.Buy);
        var target = Target(setup.Sl, StructuralTarget(low, hours));
        var selection = Assert.IsType<NasdaqHumanTakeProfitSelection.Unique>(Selector.Select(
            Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, target), setup.Sl));
        var source = Assert.IsType<NasdaqLiquidityTakeReference.Structural>(selection.Fact.Target.Reference);
        Assert.Equal(new Timeframe(hours, TimeframeUnit.Hour), source.Member.Timeframe);
        Assert.Equal(20000m, selection.Fact.Target.TargetReferencePrice);
        Assert.Equal(20000m, selection.Fact.Target.TakeProfitPrice);
        Assert.NotEmpty(source.Member.SourceCandles);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 4)]
    [InlineData(true, 1)]
    [InlineData(true, 4)]
    public void FormulaBackedTurnReferencesRetainTheirBodyCoordinate(bool low, int hours)
    {
        var setup = TargetSetup(low ? NasdaqHumanH4PermittedDirection.Sell : NasdaqHumanH4PermittedDirection.Buy);
        var candle = M5Fixture.Sources().Single(c => c.OpenTimeUtc == M5Fixture.At(8)
            && c.Timeframe == new Timeframe(hours, TimeframeUnit.Hour));
        var member = NasdaqStructuralLiquidityReference.OrdinaryTurn(low ? NasdaqHumanH4StructuralRole.HigherLow
            : NasdaqHumanH4StructuralRole.LowerHigh, [candle], "review:validated turn");
        var review = new NasdaqHumanStructuralLiquidityObservation(setup.Sl.Session, [member],
            M5Fixture.At(12), M5Fixture.At(13), "review:relevant structural target");
        var references = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(
            new NasdaqHumanStructuralLiquidityObservationSelector().Select(M5Fixture.Context(M5Fixture.At(13, 15), [review]), review.Session));
        var target = Target(setup.Sl, new(new NasdaqLiquidityTakeReference.Structural(references, member), member.StructuralPrice));
        var selected = Assert.IsType<NasdaqHumanTakeProfitSelection.Unique>(Selector.Select(
            Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, target), setup.Sl));
        Assert.Equal(member.StructuralPrice, selected.Fact.Target.TakeProfitPrice);
        Assert.Equal(NasdaqStructuralLiquidityPriceOwnership.FormulaBacked, member.PriceOwnership);
    }

    [Fact]
    public void UnequalPricesAreRejectedWithoutToleranceOrStopDistanceDerivation()
    {
        var reference = StructuralTarget(false, 1).Reference;
        Assert.Equal(20000m, new NasdaqHumanTakeProfitTarget(reference, 20000m).TakeProfitPrice);
        foreach (var price in new[] { 19999.99m, 20000.00000001m, 0m })
            Assert.Throws<ArgumentException>(() => new NasdaqHumanTakeProfitTarget(reference, price));
        foreach (var stop in new[] { 88m, 89m })
        {
            var setup = TargetSetup(stopPrice: stop);
            Assert.Equal(20000m, Target(setup.Sl, new(reference, 20000m)).Target.TakeProfitPrice);
        }
    }

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell)]
    public void OppositeTargetSideIsRejected(NasdaqHumanH4PermittedDirection direction)
    {
        var setup = TargetSetup(direction);
        Assert.Throws<ArgumentException>(() => Target(setup.Sl, SessionTarget(direction == NasdaqHumanH4PermittedDirection.Buy)));
    }

    [Fact]
    public void MissingNeverChoosesFromExistingCandidatesAndDuplicatesPreserveImmutableOrderedSupport()
    {
        var setup = TargetSetup();
        var time = M5Fixture.At(15, 5);
        var candidates = Inputs(setup.Entry.Direction).OfType<NasdaqHumanStructuralLiquidityObservation>().ToArray();
        Assert.NotEmpty(candidates);
        Assert.IsType<NasdaqHumanTakeProfitSelection.Missing>(Selector.Select(
            Frame(setup.Entry.Direction, time, setup.Realignment, setup.Stop), setup.Sl));
        var first = Target(setup.Sl);
        var second = Target(setup.Sl, SessionTarget(source: "second:source"), observed: time, source: "second:review");
        var forward = Assert.IsType<NasdaqHumanTakeProfitSelection.Unique>(Selector.Select(
            Frame(setup.Entry.Direction, time, setup.Realignment, setup.Stop, second, first), setup.Sl));
        var reversed = Assert.IsType<NasdaqHumanTakeProfitSelection.Unique>(Selector.Select(
            Frame(setup.Entry.Direction, time, setup.Realignment, setup.Stop, first, second), setup.Sl));
        Assert.Equal(new[] { first, second }, forward.SupportingObservations);
        Assert.Equal(forward.SupportingObservations, reversed.SupportingObservations);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanTakeProfitObservation>)forward.SupportingObservations).Clear());
        var sources = ((NasdaqLiquidityTakeReference.SessionLevel)first.Target.Reference).SourceCandles;
        Assert.Throws<NotSupportedException>(() => ((IList<Candle>)sources).Clear());
    }

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell)]
    public void CompetingHumanSelectionsConflictWithoutPriceOrSourceRanking(NasdaqHumanH4PermittedDirection direction)
    {
        var setup = TargetSetup(direction);
        var first = Target(setup.Sl);
        var alternatives = new[] {
            Target(setup.Sl, SessionTarget(direction == NasdaqHumanH4PermittedDirection.Sell, true), source: "different:identity"),
            Target(setup.Sl, StructuralTarget(direction == NasdaqHumanH4PermittedDirection.Sell, 1, 20001), source: "different:price"),
            Target(setup.Sl, effective: M5Fixture.At(15, 1), observed: M5Fixture.At(15, 1), source: "different:selection time") };
        foreach (var other in alternatives)
        {
            var forward = Assert.IsType<NasdaqHumanTakeProfitSelection.Conflict>(Selector.Select(
                Frame(direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, first, other), setup.Sl));
            var reversed = Assert.IsType<NasdaqHumanTakeProfitSelection.Conflict>(Selector.Select(
                Frame(direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, other, first), setup.Sl));
            Assert.Equal(2, forward.Alternatives.Count);
            Assert.Equal(forward.Alternatives, reversed.Alternatives);
            Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanTakeProfitObservation>)forward.Alternatives).Clear());
        }
    }

    [Fact]
    public void EqualPricesDifferentSourceIdentitiesAndTiedMetadataRemainDeterministicConflict()
    {
        var setup = TargetSetup();
        var first = Target(setup.Sl, SessionTarget());
        var second = Target(setup.Sl, SessionTarget(london: true));
        Assert.Equal(first.Target.TargetReferencePrice, second.Target.TargetReferencePrice);
        var forward = Assert.IsType<NasdaqHumanTakeProfitSelection.Conflict>(Selector.Select(
            Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, first, second), setup.Sl));
        var reversed = Assert.IsType<NasdaqHumanTakeProfitSelection.Conflict>(Selector.Select(
            Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, second, first), setup.Sl));
        Assert.Equal(forward.Alternatives, reversed.Alternatives);
    }

    [Fact]
    public void WrongExactSetupOrStopAndAbsentCanonicalPrerequisiteCannotQualify()
    {
        var setup = TargetSetup();
        foreach (var wrong in new[] { TargetSetup(swing: 98), TargetSetup(stopPrice: 88) })
            Assert.IsType<NasdaqHumanTakeProfitSelection.Missing>(Selector.Select(
                Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, Target(wrong.Sl)), setup.Sl));
        var observation = Target(setup.Sl);
        Assert.IsType<NasdaqHumanTakeProfitSelection.Missing>(Selector.Select(
            Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, observation), setup.Sl));
        var context = Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, observation);
        Assert.IsType<NasdaqHumanTakeProfitSelection.Missing>(Selector.Select(context.WithPriorObservations([]), setup.Sl));
        Assert.IsType<NasdaqHumanTakeProfitSelection.Missing>(Selector.Select(context, TargetSetup(stopPrice: 88).Sl));
    }

    [Fact]
    public void MissingExactTargetSourcesArePreservedAndChangedOhlcIsUnusable()
    {
        var setup = TargetSetup();
        var observation = Target(setup.Sl);
        var context = Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, observation);
        var hour = new Timeframe(1, TimeframeUnit.Hour);
        var frames = context.AvailableTimeframes.Where(t => t != hour)
            .ToDictionary(t => t, t => { context.TryGetFrame(t, out var frame); return frame!; });
        StrategyReplayContext WithFrames() => new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition,
            new MultiTimeframeReplayFrame(context.ProviderId, context.Symbol, context.Step, context.AsOfUtc,
                context.ConfiguredTimeframes, context.UpdatedTimeframes.Where(frames.ContainsKey), frames),
            context.InputObservations).WithPriorObservations(context.PriorObservations);
        var missing = Assert.IsType<NasdaqHumanTakeProfitSelection.Missing>(Selector.Select(WithFrames(), setup.Sl));
        Assert.Same(observation, Assert.Single(missing.UnavailableSourceObservations));
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanTakeProfitObservation>)missing.UnavailableSourceObservations).Clear());
        context.TryGetFrame(hour, out var original);
        var changed = new CandleSeries(context.ProviderId, context.Symbol, hour, original!.AvailableCandles.Select(c =>
            c.OpenTimeUtc == M5Fixture.At(8) ? LiquidityFixture.Candle(8, open: 101) : c));
        var cursor = new MultiTimeframeCandleReplayCursor([changed]);
        while (cursor.TryAdvance(out var frame)) frames[hour] = frame!.FramesByTimeframe[hour];
        Assert.Empty(Assert.IsType<NasdaqHumanTakeProfitSelection.Missing>(Selector.Select(WithFrames(), setup.Sl)).UnavailableSourceObservations);
    }

    [Fact]
    public void UnavailableCompetingTargetRemainsAuditableAlongsideUsableSupport()
    {
        var setup = TargetSetup();
        var available = Target(setup.Sl);
        var unavailable = Target(setup.Sl, StructuralTarget(false, 4), source: "review:unavailable alternative");
        var context = Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, available, unavailable);
        var h4 = new Timeframe(4, TimeframeUnit.Hour);
        var frames = context.AvailableTimeframes.Where(t => t != h4)
            .ToDictionary(t => t, t => { context.TryGetFrame(t, out var frame); return frame!; });
        var partial = new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition,
            new MultiTimeframeReplayFrame(context.ProviderId, context.Symbol, context.Step, context.AsOfUtc,
                context.ConfiguredTimeframes, context.UpdatedTimeframes.Where(frames.ContainsKey), frames),
            context.InputObservations).WithPriorObservations(context.PriorObservations);
        var selected = Assert.IsType<NasdaqHumanTakeProfitSelection.Unique>(Selector.Select(partial, setup.Sl));
        Assert.Same(available, Assert.Single(selected.SupportingObservations));
        Assert.Same(unavailable, Assert.Single(selected.UnavailableSourceObservations));
        // Unique describes resolved support only; it is not a runtime permission to bypass the unresolved alternative.
    }

    [Fact]
    public void LaterTargetChangesAndFutureMarketDataCannotChangeEarlierSelection()
    {
        var setup = TargetSetup();
        var first = Target(setup.Sl);
        var future = Target(setup.Sl, StructuralTarget(false, 1), observed: M5Fixture.At(15, 10), source: "future:target change");
        var time = M5Fixture.At(15, 5);
        var laterOutcome = new LaterTradeInformation(setup.Sl.Session, M5Fixture.At(15, 10), "later:trade record", true, true, "inconclusive");
        var context = Frame(setup.Entry.Direction, time, setup.Realignment, setup.Stop, first, future, laterOutcome);
        Assert.DoesNotContain(laterOutcome, context.InputObservations);
        var baseline = Frame(setup.Entry.Direction, time, setup.Realignment, setup.Stop, first);
        var selected = Assert.IsType<NasdaqHumanTakeProfitSelection.Unique>(Selector.Select(context, setup.Sl));
        Assert.Equal(Assert.IsType<NasdaqHumanTakeProfitSelection.Unique>(Selector.Select(baseline, setup.Sl)).SupportingObservations,
            selected.SupportingObservations);
        var bounded = M5Fixture.Context(time, context.InputObservations.ToArray(), includeFutureCandles: false)
            .WithPriorObservations(context.PriorObservations);
        Assert.Equal(selected.SupportingObservations,
            Assert.IsType<NasdaqHumanTakeProfitSelection.Unique>(Selector.Select(bounded, setup.Sl)).SupportingObservations);
        Assert.IsType<NasdaqHumanTakeProfitSelection.Missing>(Selector.Select(
            Frame(setup.Entry.Direction, time, setup.Realignment, setup.Stop, future), setup.Sl));
    }

    [Fact]
    public void FutureSourceCannotBeDocumentedAsEarlierTargetSelection()
    {
        var setup = TargetSetup();
        var target = SessionTarget();
        Assert.Throws<ArgumentException>(() => Target(setup.Sl, target, effective: M5Fixture.At(12), observed: M5Fixture.At(15)));
        var futureSource = LiquidityFixture.Candle(12, hours: 4);
        var reference = LiquidityFixture.Human(futureSource);
        var review = new NasdaqHumanStructuralLiquidityObservation(setup.Sl.Session, [reference],
            M5Fixture.At(16), M5Fixture.At(17), "future:source");
        var sourceContext = LiquidityFixture.Context([futureSource], [review], 17);
        var selection = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(new NasdaqHumanStructuralLiquidityObservationSelector().Select(sourceContext, review.Session));
        var futureTarget = new NasdaqHumanTakeProfitTarget(new NasdaqLiquidityTakeReference.Structural(selection, reference), reference.StructuralPrice);
        Assert.Throws<ArgumentException>(() => Target(setup.Sl, futureTarget, effective: M5Fixture.At(15), observed: M5Fixture.At(17)));
        var observation = Target(setup.Sl, futureTarget, effective: M5Fixture.At(17), observed: M5Fixture.At(17));
        Assert.IsType<NasdaqHumanTakeProfitSelection.Missing>(Selector.Select(
            Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, observation), setup.Sl));
    }

    [Fact]
    public void TerminalCutoffAndCurrentStopConflictPreventRevival()
    {
        var setup = TargetSetup();
        var target = Target(setup.Sl);
        Assert.IsType<NasdaqHumanTakeProfitSelection.Missing>(Selector.Select(
            Frame(setup.Entry.Direction, M5Fixture.At(16), setup.Realignment, setup.Stop, target), setup.Sl));
        var invalid = M5Fixture.Take(true, effective: M5Fixture.At(14, 30), observed: M5Fixture.At(14, 30));
        var initiating = setup.Entry.Realignment.Pullback.Selection.Fact.ApprovedQuality.Fact.Fvg.Trigger.DecisiveTake;
        var terminal = Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, target,
            invalid, new NasdaqHumanRelevantLiquidityTakeObservation(invalid, invalid.ObservedAtUtc, "terminal:event", initiating));
        Assert.IsType<NasdaqHumanTakeProfitSelection.Missing>(Selector.Select(terminal, setup.Sl));
        var gate = terminal.PriorObservations.Last().RuleFacts.Single(f => f.RuleId.Value == "NQ-LIQ-003");
        var valid = Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, target);
        var last = valid.PriorObservations.Last();
        var gatedHistory = valid.PriorObservations.SkipLast(1).Append(new StrategyReplayContextObservation(
            last.StrategyId, last.StrategyVersion, last.ProviderId, last.Symbol, last.Step, last.AsOfUtc,
            last.Evaluations, last.WorkflowProgression, last.LifecycleProgression,
            ruleFacts: last.RuleFacts.Where(f => f.RuleId.Value != "NQ-LIQ-003").Append(gate)));
        Assert.IsType<NasdaqHumanTakeProfitSelection.Missing>(Selector.Select(valid.WithPriorObservations(gatedHistory), setup.Sl));
        Assert.IsType<NasdaqHumanTakeProfitSelection.Missing>(Selector.Select(
            Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, Stop(setup.Entry, stopPrice: 88), target), setup.Sl));
        var late = Target(setup.Sl, observed: M5Fixture.At(16));
        Assert.IsType<NasdaqHumanTakeProfitSelection.Missing>(Selector.Select(
            Frame(setup.Entry.Direction, M5Fixture.At(16), setup.Realignment, setup.Stop, late), setup.Sl));
    }

    [Fact]
    public void UtcAvailabilityProvenanceAndIndependentSelectionTimeAreValidated()
    {
        var setup = TargetSetup();
        Assert.Throws<ArgumentException>(() => Target(setup.Sl, observed: M5Fixture.At(14, 30)));
        Assert.Throws<ArgumentException>(() => Target(setup.Sl, effective: M5Fixture.At(15).ToOffset(TimeSpan.FromHours(1))));
        Assert.Throws<ArgumentException>(() => Target(setup.Sl, observed: M5Fixture.At(15).ToOffset(TimeSpan.FromHours(1))));
        Assert.Throws<ArgumentException>(() => Target(setup.Sl, source: " "));
        Assert.Throws<ArgumentException>(() => Target(setup.Sl, effective: M5Fixture.At(14, 20), observed: M5Fixture.At(14, 20)));
        // Target selection time is documented independently of when the SL assertion becomes available.
        var early = Target(setup.Sl, effective: M5Fixture.At(14, 20), observed: M5Fixture.At(15));
        Assert.Equal(M5Fixture.At(14, 20), early.EffectiveAtUtc);
        Assert.IsType<NasdaqHumanTakeProfitSelection.Unique>(Selector.Select(
            Frame(setup.Entry.Direction, M5Fixture.At(15, 5), setup.Realignment, setup.Stop, early), setup.Sl));
    }

    // Opaque test input verifies that later recorded hits/results cannot backfill target selection.
    // This is not a production hit detector, outcome model or execution policy.
    private sealed record LaterTradeInformation(NasdaqDemoSessionIdentity Session, DateTimeOffset ObservedAtUtc,
        string SourceReference, bool ReportedTakeProfitHit, bool ReportedStopLossHit, string ReportedOutcome)
        : IStrategyReplayInputObservation
    {
        public StrategyId StrategyId => Session.StrategyId;
        public StrategyVersion StrategyVersion => Session.StrategyVersion;
        public MarketDataProviderId ProviderId => Session.ProviderId;
        public MarketSymbol Symbol => Session.Symbol;
    }

    [Fact]
    public void EvidenceDoesNotChangeCanonicalEvaluationsFactsOrCapability()
    {
        var setup = TargetSetup();
        var inputs = Inputs(setup.Entry.Direction, setup.Realignment, setup.Stop);
        var baseline = Run(inputs);
        var withEvidence = Run(inputs.Append(Target(setup.Sl)).ToArray());
        Assert.Equal(baseline.StrategyObservations.SelectMany(o => o.Evaluations), withEvidence.StrategyObservations.SelectMany(o => o.Evaluations));
        Assert.Equal(baseline.StrategyObservations.SelectMany(o => o.RuleFacts).Select(f =>
            (f.RuleId, Proof: JsonSerializer.Serialize(f.Fact, f.Fact.GetType()))),
            withEvidence.StrategyObservations.SelectMany(o => o.RuleFacts).Select(f =>
                (f.RuleId, Proof: JsonSerializer.Serialize(f.Fact, f.Fact.GetType()))));
        var report = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(),
            MoneyWayReplayRuleEvaluators.GetAll(), MoneyWayReplayEvaluationCapabilityDeclarations.GetAll())
            .Find(LiquidityFixture.Definition.StrategyId, LiquidityFixture.Definition.Version)!;
        Assert.Equal((32, 14, 15, 0, true), (report.TotalRuleCount, report.RequiredRuleCount, report.ImplementedCount,
            report.RequiredEvaluatorGapCount, report.HasFullRequiredEvaluatorRegistration));
        Assert.DoesNotContain(MoneyWayReplayRuleEvaluators.GetAll(), e => e.RuleId.Value == "NQ-TP-001");
        Assert.Equal(new[] { new RuleId("NQ-SL-001") }, MoneyWayReplayWorkflowDefinitions.GetAll().Single().GetPrerequisiteRuleIds(new("NQ-TP-001")));
    }
}

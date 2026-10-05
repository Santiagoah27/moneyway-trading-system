using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanStructuralStopLossTests
{
    private static readonly NasdaqHumanStructuralStopLossObservationSelector Selector = new();

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

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy, NasdaqM5ProtectionAnchorKind.HigherLow, 90)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell, NasdaqM5ProtectionAnchorKind.LowerHigh, 110)]
    public void ExactHumanSelectedM5AnchorAndDocumentedStopAreUnique(
        NasdaqHumanH4PermittedDirection direction, NasdaqM5ProtectionAnchorKind kind, decimal anchorPrice)
    {
        var setup = Setup(direction);
        var stop = Stop(setup.Fact);
        var selection = Assert.IsType<NasdaqHumanStructuralStopLossSelection.Unique>(
            Selector.Select(Frame(direction, M5Fixture.At(15), setup.Realignment, stop), setup.Fact));
        Assert.Same(stop, selection.Fact);
        Assert.Equal(kind, selection.Fact.ProtectionAnchor.Kind);
        Assert.Equal(anchorPrice, selection.Fact.ProtectionAnchor.ProtectionAnchorPrice);
        Assert.Equal(M5Fixture.At(14, 30), selection.Fact.EffectiveAtUtc);
        Assert.Equal(M5Fixture.At(14, 30), selection.Fact.ObservedAtUtc);
        Assert.NotEqual(selection.Fact.ProtectionAnchor.ProtectionAnchorPrice, selection.Fact.StopPrice);
        Assert.Equal("review:stop", selection.Fact.SourceReference);
        Assert.Empty(selection.UnavailableSourceObservations);
    }

    [Fact]
    public void WrongKindTimeframeAndStopSideAreRejectedWithoutInferringAFormula()
    {
        var buy = Setup(NasdaqHumanH4PermittedDirection.Buy).Fact;
        var sell = Setup(NasdaqHumanH4PermittedDirection.Sell).Fact;
        var m5 = M5Fixture.Five(M5Fixture.At(14, 20));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanStructuralStopLossObservation(buy,
            new(NasdaqM5ProtectionAnchorKind.LowerHigh, [m5]), 111, M5Fixture.At(14, 30),
            M5Fixture.At(14, 30), "wrong:kind"));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanStructuralStopLossObservation(sell,
            new(NasdaqM5ProtectionAnchorKind.HigherLow, [m5]), 89, M5Fixture.At(14, 30),
            M5Fixture.At(14, 30), "wrong:kind"));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5ProtectionAnchor(
            NasdaqM5ProtectionAnchorKind.HigherLow, [Minute(20)]));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5ProtectionAnchor(
            NasdaqM5ProtectionAnchorKind.HigherLow, [LiquidityFixture.Candle(9)]));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5ProtectionAnchor(
            NasdaqM5ProtectionAnchorKind.HigherLow, [LiquidityFixture.Candle(8, hours: 4)]));
        Assert.Throws<ArgumentException>(() => Stop(buy, stopPrice: 90));
        Assert.Throws<ArgumentException>(() => Stop(sell, stopPrice: 110));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanStructuralStopLossObservation(buy,
            new(NasdaqM5ProtectionAnchorKind.HigherLow, [m5]), 89, M5Fixture.At(14, 15),
            M5Fixture.At(14, 30), "early:selection"));
    }

    [Fact]
    public void MissingCompatibleSupportAndCompetingAnchorsKeepAllProvenance()
    {
        var setup = Setup(NasdaqHumanH4PermittedDirection.Buy);
        Assert.IsType<NasdaqHumanStructuralStopLossSelection.Missing>(Selector.Select(
            Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), setup.Realignment), setup.Fact));
        var first = Stop(setup.Fact);
        var duplicate = Stop(setup.Fact, observed: M5Fixture.At(15), reference: "second:review");
        var compatible = Assert.IsType<NasdaqHumanStructuralStopLossSelection.Unique>(Selector.Select(
            Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), setup.Realignment, duplicate, first), setup.Fact));
        Assert.Equal(2, compatible.SupportingObservations.Count);
        Assert.Contains(first, compatible.SupportingObservations);
        Assert.Contains(duplicate, compatible.SupportingObservations);
        var other = Stop(setup.Fact, source: M5Fixture.Five(M5Fixture.At(14, 15)), reference: "other:anchor");
        var forward = Assert.IsType<NasdaqHumanStructuralStopLossSelection.Conflict>(Selector.Select(
            Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), setup.Realignment, first, other), setup.Fact));
        var reversed = Assert.IsType<NasdaqHumanStructuralStopLossSelection.Conflict>(Selector.Select(
            Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), setup.Realignment, other, first), setup.Fact));
        Assert.Equal(forward.Alternatives.Select(o => o.SourceReference), reversed.Alternatives.Select(o => o.SourceReference));
        Assert.Equal(2, forward.Alternatives.Count);
        var differentStop = Stop(setup.Fact, stopPrice: 88, reference: "other:stop");
        Assert.IsType<NasdaqHumanStructuralStopLossSelection.Conflict>(Selector.Select(
            Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), setup.Realignment, first, differentStop), setup.Fact));
    }

    [Fact]
    public void WrongUpstreamUnavailableSourceAndFutureEvidenceCannotQualify()
    {
        var setup = Setup(NasdaqHumanH4PermittedDirection.Buy);
        var other = Setup(NasdaqHumanH4PermittedDirection.Buy, swingLevel: 98).Fact;
        Assert.IsType<NasdaqHumanStructuralStopLossSelection.Missing>(Selector.Select(
            Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), setup.Realignment, Stop(other)), setup.Fact));
        var absent = new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, NasdaqHumanM5ProtectionAnchor.M5,
            M5Fixture.At(14, 19).AddSeconds(1), M5Fixture.At(14, 24).AddSeconds(1), 100, 110, 90, 100, null);
        var unresolved = Stop(setup.Fact, source: absent);
        var missing = Assert.IsType<NasdaqHumanStructuralStopLossSelection.Missing>(Selector.Select(
            Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), setup.Realignment, unresolved), setup.Fact));
        Assert.Same(unresolved, Assert.Single(missing.UnavailableSourceObservations));
        var future = Stop(setup.Fact, observed: M5Fixture.At(16));
        Assert.IsType<NasdaqHumanStructuralStopLossSelection.Missing>(Selector.Select(
            Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), setup.Realignment, future), setup.Fact));
        var futureSource = new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, NasdaqHumanM5ProtectionAnchor.M5,
            M5Fixture.At(15), M5Fixture.At(15, 5), 100, 110, 90, 100, null);
        var futureAnchor = new NasdaqHumanM5ProtectionAnchor(NasdaqM5ProtectionAnchorKind.HigherLow, [futureSource]);
        var laterSelection = new NasdaqHumanStructuralStopLossObservation(setup.Fact, futureAnchor, 89,
            M5Fixture.At(15, 5), M5Fixture.At(15, 5), "future:source");
        Assert.IsType<NasdaqHumanStructuralStopLossSelection.Missing>(Selector.Select(
            Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), setup.Realignment, laterSelection), setup.Fact));
    }

    [Fact]
    public void TerminalSessionAndCanonicalCutoffDoNotReviveStopEvidence()
    {
        var setup = Setup(NasdaqHumanH4PermittedDirection.Buy);
        var stop = Stop(setup.Fact);
        Assert.IsType<NasdaqHumanStructuralStopLossSelection.Missing>(Selector.Select(
            Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(16), setup.Realignment, stop), setup.Fact));
        var invalid = M5Fixture.Take(true, effective: M5Fixture.At(14, 30), observed: M5Fixture.At(14, 30));
        var initiating = setup.Fact.Realignment.Pullback.Selection.Fact.ApprovedQuality.Fact.Fvg.Trigger.DecisiveTake;
        var terminal = Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), setup.Realignment, stop,
            invalid, new NasdaqHumanRelevantLiquidityTakeObservation(invalid, invalid.ObservedAtUtc,
                "terminal:event", initiating));
        Assert.IsType<NasdaqHumanStructuralStopLossSelection.Missing>(Selector.Select(terminal, setup.Fact));
    }
}

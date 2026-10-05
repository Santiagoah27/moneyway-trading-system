using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;

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
        var frame = Frame(NasdaqHumanH4PermittedDirection.Buy, M5Fixture.At(15), later);
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

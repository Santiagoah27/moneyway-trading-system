using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanM1CorrectiveRetracementTests
{
    private static readonly NasdaqHumanM1CorrectiveRetracementObservationSelector Selector = new();

    private static NasdaqHumanM5TriggerObservation Trigger(NasdaqHumanLiquidityTakeObservation take,
        NasdaqHumanH4PermittedDirection direction)
    {
        var source = M5Fixture.Five(M5Fixture.At(14));
        return new(take, new NasdaqHumanM5TriggerEvent.StructuralChange(M5Fixture.Five(M5Fixture.At(14, 10)),
            direction, [source], [source], 100), M5Fixture.At(14, 10), "trigger:source");
    }

    private static NasdaqHumanM5FvgObservation Fvg(NasdaqHumanM5TriggerObservation trigger, int minute = 10) =>
        new(trigger, new([M5Fixture.Five(M5Fixture.At(14, minute - 10)),
            M5Fixture.Five(M5Fixture.At(14, minute - 5)), M5Fixture.Five(M5Fixture.At(14, minute))]),
            M5Fixture.At(14, Math.Max(10, minute)), "fvg:source");

    private static NasdaqHumanM5FvgQualityObservation Quality(NasdaqHumanM5FvgObservation fvg,
        NasdaqHumanM5FvgQualityDecision decision = NasdaqHumanM5FvgQualityDecision.Approved,
        int effective = 10) => new(new(fvg, decision, M5Fixture.At(14, effective), "Reviewed strength."),
            M5Fixture.At(14, 15), "quality:source");

    private static NasdaqHumanM1CorrectiveRetracementObservation Pullback(NasdaqHumanM5FvgQualityObservation quality,
        int minute = 20, int observed = 20, string provenance = "review:pullback", params int[] members)
    {
        var closes = members.Length == 0 ? [minute] : members;
        return new(quality, new(closes.Select(m => M5Fixture.Series().Single(s => s.Timeframe == M5Fixture.Minute)
            .Candles.Single(c => c.CloseTimeUtc == M5Fixture.At(14, m)))), M5Fixture.At(14, observed), provenance);
    }

    private static (StrategyReplayContext Context, NasdaqHumanM5FvgQualityRuleFact Fact) Frame(
        NasdaqHumanH4PermittedDirection direction = NasdaqHumanH4PermittedDirection.Buy,
        IStrategyReplayInputObservation[]? additional = null, DateTimeOffset? asOf = null)
    {
        var take = M5Fixture.Take(direction == NasdaqHumanH4PermittedDirection.Sell);
        var trigger = Trigger(take, direction);
        var fvg = Fvg(trigger);
        var quality = Quality(fvg);
        var inputs = M5Fixture.Inputs(direction, take).Concat([trigger, fvg, quality])
            .Concat(additional ?? []).ToArray();
        var definitions = new StrategyDefinitionCatalog().GetAll();
        var run = new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(MoneyWayReplayRuleEvaluators.GetAll()),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), new(),
            new(definitions, MoneyWayReplayLifecyclePolicies.GetAll()), new(new()))
            .Execute(LiquidityFixture.Definition, M5Fixture.Series(), inputs);
        var time = asOf ?? M5Fixture.At(14, 30);
        var history = run.StrategyObservations.Where(o => o.AsOfUtc < time).ToArray();
        var fact = history.SelectMany(o => o.RuleFacts).Where(f => f.RuleId.Value == "NQ-FVG-002")
            .Select(f => f.Fact).OfType<NasdaqHumanM5FvgQualityRuleFact>().Last();
        return (M5Fixture.Context(time, inputs).WithPriorObservations(history), fact);
    }

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell)]
    public void ExactApprovedFvgAndCountertrendDirectionSelectUnique(NasdaqHumanH4PermittedDirection direction)
    {
        var baseFrame = Frame(direction);
        var quality = baseFrame.Fact.Selection.Fact;
        var pullback = Pullback(quality);
        var frame = Frame(direction, [pullback]);
        var unique = Assert.IsType<NasdaqHumanM1CorrectiveRetracementSelection.Unique>(Selector.Select(frame.Context, frame.Fact));
        Assert.Same(pullback, unique.Fact);
        Assert.Equal(direction, unique.Fact.SetupDirection);
        Assert.Equal(M5Fixture.At(14, 20), unique.Fact.PullbackEffectiveAtUtc);
        Assert.Equal(M5Fixture.At(14, 20), unique.Fact.ObservedAtUtc);
        Assert.Same(quality.Fact.Fvg, unique.Fact.ApprovedQuality.Fact.Fvg);
        Assert.IsType<NasdaqHumanM1CorrectiveRetracementSelection.Missing>(Selector.Select(baseFrame.Context, baseFrame.Fact));
    }

    [Fact]
    public void EarlierMovementAndSameTimestampApprovalAreAllowed()
    {
        var baseFrame = Frame();
        var quality = baseFrame.Fact.Selection.Fact;
        var approvedAtClose = new NasdaqHumanM5FvgQualityObservation(
            new(quality.Fact.Fvg, NasdaqHumanM5FvgQualityDecision.Approved, M5Fixture.At(14, 20), "Reviewed strength."),
            M5Fixture.At(14, 20), "quality:same-close");
        var same = Pullback(approvedAtClose, 20, 20, "same", 15, 20);
        Assert.Equal(M5Fixture.At(14, 20), same.PullbackEffectiveAtUtc);
        Assert.Equal(same.ApprovedQuality.EffectiveAtUtc, same.PullbackEffectiveAtUtc);
        Assert.True(same.Event.SourceCandles[0].CloseTimeUtc < same.PullbackEffectiveAtUtc);
        Assert.Throws<ArgumentException>(() => Pullback(quality, 5, 20));
    }

    [Fact]
    public void RejectedAndOtherFvgCannotBeSilentlyRebound()
    {
        var frame = Frame();
        var approved = frame.Fact.Selection.Fact;
        var otherFvg = Fvg(approved.Fact.Fvg.Trigger, 15);
        var other = Pullback(Quality(otherFvg, effective: 15));
        Assert.IsType<NasdaqHumanM1CorrectiveRetracementSelection.Missing>(
            Selector.Select(Frame(additional: [other]).Context, frame.Fact));
        Assert.Throws<ArgumentException>(() => Pullback(Quality(approved.Fact.Fvg,
            NasdaqHumanM5FvgQualityDecision.Rejected)));
    }

    [Fact]
    public void MissingExactOneMinuteSourceIsPreserved()
    {
        var quality = Frame().Fact.Selection.Fact;
        var source = new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, M5Fixture.Minute,
            M5Fixture.At(14, 24), M5Fixture.At(14, 25), 100, 110, 90, 100, null);
        var observation = new NasdaqHumanM1CorrectiveRetracementObservation(quality,
            new([source]), M5Fixture.At(14, 30), "review:missing-source");
        var frame = Frame(additional: [observation]);
        var missing = Assert.IsType<NasdaqHumanM1CorrectiveRetracementSelection.Missing>(Selector.Select(frame.Context, frame.Fact));
        Assert.Same(observation, Assert.Single(missing.UnavailableSourceObservations));
    }

    [Fact]
    public void CompatibleSupportAndCompetingSelectionsAreDeterministic()
    {
        var quality = Frame().Fact.Selection.Fact;
        var first = Pullback(quality);
        var duplicate = Pullback(quality, provenance: "second reviewer");
        var second = Pullback(quality, 30, 30, "different selected event");
        var one = Frame(additional: [duplicate, first]);
        var unique = Assert.IsType<NasdaqHumanM1CorrectiveRetracementSelection.Unique>(Selector.Select(one.Context, one.Fact));
        Assert.Equal(2, unique.SupportingObservations.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanM1CorrectiveRetracementObservation>)unique.SupportingObservations).Clear());
        var a = Frame(additional: [second, first]);
        var b = Frame(additional: [first, second]);
        var conflict = Assert.IsType<NasdaqHumanM1CorrectiveRetracementSelection.Conflict>(Selector.Select(a.Context, a.Fact));
        Assert.Equal(2, conflict.Alternatives.Count);
        Assert.Equal(conflict.Alternatives, Assert.IsType<NasdaqHumanM1CorrectiveRetracementSelection.Conflict>(Selector.Select(b.Context, b.Fact)).Alternatives);
    }

    [Fact]
    public void FutureObservationIsInvisibleAtEarlierFrame()
    {
        var quality = Frame().Fact.Selection.Fact;
        var future = Pullback(quality, 20, 60);
        var early = Frame(additional: [future]);
        Assert.IsType<NasdaqHumanM1CorrectiveRetracementSelection.Missing>(Selector.Select(early.Context, early.Fact));
        var later = Frame(additional: [future], asOf: M5Fixture.At(15));
        Assert.IsType<NasdaqHumanM1CorrectiveRetracementSelection.Unique>(Selector.Select(later.Context, later.Fact));
    }

    [Fact]
    public void WrongTimeframeFutureSourceAndCutoffCannotProgress()
    {
        var baseFrame = Frame();
        var quality = baseFrame.Fact.Selection.Fact;
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM1CorrectiveRetracementEvent([M5Fixture.Five(M5Fixture.At(14, 20))]));
        var future = M5Fixture.Series().Single(s => s.Timeframe == M5Fixture.Minute).Candles
            .Single(c => c.CloseTimeUtc == M5Fixture.At(14, 30));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM1CorrectiveRetracementObservation(quality,
            new([future]), M5Fixture.At(14, 20), "review:future"));
        var pullback = Pullback(quality);
        var later = Frame(additional: [pullback], asOf: M5Fixture.At(38, 5));
        Assert.IsType<NasdaqHumanM1CorrectiveRetracementSelection.Missing>(Selector.Select(later.Context, later.Fact));
    }

    [Fact]
    public void TerminalSessionCannotRevivePullbackEvidence()
    {
        var quality = Frame().Fact.Selection.Fact;
        var invalid = M5Fixture.Take(true, effective: M5Fixture.At(14, 30), observed: M5Fixture.At(14, 30));
        var terminal = Frame(additional: [Pullback(quality), invalid,
            new NasdaqHumanRelevantLiquidityTakeObservation(invalid, invalid.ObservedAtUtc,
                "terminal event", quality.Fact.Fvg.Trigger.DecisiveTake)], asOf: M5Fixture.At(15));
        Assert.Contains(terminal.Context.PriorObservations.SelectMany(o => o.RuleFacts).Select(f => f.Fact)
            .OfType<NasdaqLiquidityTakeRuleFact>(), f => f.IsSessionInvalidated);
        Assert.IsType<NasdaqHumanM1CorrectiveRetracementSelection.Missing>(Selector.Select(terminal.Context, terminal.Fact));
    }
}

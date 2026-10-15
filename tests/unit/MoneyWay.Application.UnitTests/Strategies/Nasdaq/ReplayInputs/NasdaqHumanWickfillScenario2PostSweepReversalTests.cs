using System.Text.Json;
using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanWickfillScenario2PostSweepReversalTests
{
    private static readonly Timeframe M1 = new(1, TimeframeUnit.Minute);
    private static DateTimeOffset At(int hour, int minute = 0) => LiquidityFixture.At(hour).AddMinutes(minute);
    private readonly NasdaqHumanWickfillScenario2PostSweepReversalObservationSelector selector = new();

    [Fact]
    public void DocumentedIntrabarReversalRetainsImmutableAncestryAndNeedsNoClosedCandle()
    {
        var f = Build(); var sweep = Sweep(f);
        var observation = Observation(sweep);
        Assert.Same(sweep, observation.OpeningSweep);
        Assert.Same(sweep.Take.Take.Reference, observation.OpeningSweep.Take.Take.Reference);
        Assert.Equal(sweep.Session, observation.Session);
        Assert.Equal(NasdaqHumanH4PermittedDirection.Sell, observation.Direction);
        Assert.Equal("source:turn", observation.EventId);
        Assert.Equal(At(13, 32), observation.EffectiveAtUtc);
        Assert.Equal(At(13, 32), observation.ObservedAtUtc);
        Assert.Empty(observation.SupportingCandles);
        Assert.All(typeof(NasdaqHumanWickfillScenario2PostSweepReversalObservation).GetProperties(), p => Assert.Null(p.SetMethod));
        var context = Frame(f, At(13, 32), observation);
        Assert.False(context.IsAvailable(new(30, TimeframeUnit.Minute)));
        Assert.Same(observation, Assert.IsType<NasdaqHumanWickfillScenario2PostSweepReversalSelection.Unique>(selector.Select(context, sweep)).Fact);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EqualOrEarlierEffectiveTimeIsRejected(int seconds)
    {
        var sweep = Sweep(Build());
        Assert.Throws<ArgumentException>(() => Observation(sweep, effective: sweep.EffectiveAtUtc.AddSeconds(seconds)));
    }

    [Theory]
    [InlineData("effective-offset")]
    [InlineData("observed-offset")]
    [InlineData("before-effective")]
    [InlineData("before-parent")]
    [InlineData("other-session")]
    public void InvalidTimesAndSessionDateAreRejected(string issue)
    {
        var f = Build(preOpenObserved: issue == "before-parent" ? At(14) : null);
        var sweep = Sweep(f);
        var effective = issue == "other-session" ? At(37, 32) : issue == "effective-offset"
            ? At(13, 32).ToOffset(TimeSpan.FromHours(-5)) : At(13, 32);
        var observed = issue == "observed-offset" ? At(13, 33).ToOffset(TimeSpan.FromHours(-5))
            : issue == "before-effective" ? At(13, 31) : effective.ToUniversalTime();
        Assert.Throws<ArgumentException>(() => Observation(sweep, effective: effective, observed: observed));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" padded")]
    [InlineData("padded ")]
    [InlineData(null)]
    public void EventIdentityAndProvenanceAreRequired(string? invalid)
    {
        var sweep = Sweep(Build());
        Assert.ThrowsAny<ArgumentException>(() => Observation(sweep, eventId: invalid!));
        Assert.ThrowsAny<ArgumentException>(() => Observation(sweep, source: invalid!));
    }

    [Fact]
    public void NullOpeningSweepCannotConstructAnIndependentOrStandardPathAssertion() =>
        Assert.Throws<ArgumentNullException>(() => Observation(null!));

    [Fact]
    public void SupportingCandlesAreOptionalProvenanceClosedByAvailabilityNotByEvent()
    {
        var f = Build(); var sweep = Sweep(f); var candles = new List<Candle> { Support() };
        var observation = Observation(sweep, observed: At(14), sources: candles);
        candles.Clear();
        Assert.Single(observation.SupportingCandles);
        Assert.True(observation.SupportingCandles[0].CloseTimeUtc > observation.EffectiveAtUtc);
        Assert.Throws<NotSupportedException>(() => ((IList<Candle>)observation.SupportingCandles).Clear());
        Assert.Throws<ArgumentException>(() => Observation(sweep, observed: At(13, 33), sources: [Support()]));
        Assert.IsType<NasdaqHumanWickfillScenario2PostSweepReversalSelection.Missing>(selector.Select(Frame(f, At(13, 33), observation), sweep));
        Assert.IsType<NasdaqHumanWickfillScenario2PostSweepReversalSelection.Unique>(selector.Select(Frame(f, At(14), observation), sweep));
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("symbol")]
    [InlineData("null")]
    [InlineData("duplicate")]
    public void InvalidSupportingSourceIdentityIsRejected(string issue)
    {
        var sweep = Sweep(Build()); var candle = Support();
        Candle[] sources = issue switch
        {
            "null" => [null!],
            "duplicate" => [candle, candle],
            _ => [new(issue == "provider" ? new("other") : candle.ProviderId, issue == "symbol" ? new("OTHER") : candle.Symbol,
                candle.Timeframe, candle.OpenTimeUtc, candle.CloseTimeUtc, candle.Open, candle.High, candle.Low, candle.Close, candle.Volume)],
        };
        Assert.Throws<ArgumentException>(() => Observation(sweep, observed: At(14), sources: sources));
    }

    [Fact]
    public void MissingAndUniqueRemainEvidenceOnlyAndPreserveSourceReferences()
    {
        var f = Build(); var sweep = Sweep(f); var observation = Observation(sweep);
        Assert.IsType<NasdaqHumanWickfillScenario2PostSweepReversalSelection.Missing>(selector.Select(Frame(f, At(13, 33)), sweep));
        var selected = Assert.IsType<NasdaqHumanWickfillScenario2PostSweepReversalSelection.Unique>(selector.Select(Frame(f, At(13, 33), observation), sweep));
        Assert.Same(observation, selected.Fact);
        Assert.Equal("review:turn", selected.Fact.SourceReference);
        Assert.Equal("review:pre-open", selected.Fact.OpeningSweep.PreOpenContext.Fact.SourceReference);
        Assert.Equal("review:take", selected.Fact.OpeningSweep.Take.Take.SourceReference);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanWickfillScenario2PostSweepReversalObservation>)selected.SupportingObservations).Clear());
    }

    [Fact]
    public void DuplicateReviewsRetainAllSupportAndDoNotChooseLatestAuthority()
    {
        var f = Build(); var sweep = Sweep(f);
        var first = Observation(sweep, source: "z");
        var other = Observation(Sweep(Build()), observed: At(14), source: "a", sources: [Support()]);
        var selected = Assert.IsType<NasdaqHumanWickfillScenario2PostSweepReversalSelection.Unique>(selector.Select(Frame(f, At(14), other, first, first), sweep));
        Assert.Equal([first, first, other], selected.SupportingObservations);
        Assert.Single(selected.SupportingObservations[2].SupportingCandles);
        Assert.Equal(At(14), selected.SupportingObservations[2].ObservedAtUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompetingEventIdentitiesOrTimesConflictWithoutWinnerAndIndependentOfOrder(bool changedTime)
    {
        var f = Build(); var sweep = Sweep(f);
        var first = Observation(sweep, source: "same");
        var other = Observation(sweep, eventId: changedTime ? first.EventId : "source:other", effective: changedTime ? At(13, 33) : first.EffectiveAtUtc,
            observed: At(14), source: "same");
        var forward = Assert.IsType<NasdaqHumanWickfillScenario2PostSweepReversalSelection.Conflict>(selector.Select(Frame(f, At(14), first, other, first), sweep));
        var reverse = Assert.IsType<NasdaqHumanWickfillScenario2PostSweepReversalSelection.Conflict>(selector.Select(Frame(f, At(14), other, first, first), sweep));
        Assert.Equal(3, forward.Alternatives.Count);
        Assert.Equal(forward.Alternatives, reverse.Alternatives);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanWickfillScenario2PostSweepReversalObservation>)forward.Alternatives).Clear());
    }

    [Fact]
    public void FutureAssertionsDoNotChangeAnEarlierSelection()
    {
        var f = Build(); var sweep = Sweep(f);
        var early = Observation(sweep); var late = Observation(sweep, eventId: "other", observed: At(14));
        var before = Frame(f, At(13, 33), early, late);
        var selected = Assert.IsType<NasdaqHumanWickfillScenario2PostSweepReversalSelection.Unique>(selector.Select(before, sweep));
        Assert.Same(early, selected.Fact);
        Assert.IsType<NasdaqHumanWickfillScenario2PostSweepReversalSelection.Conflict>(selector.Select(Frame(f, At(14), early, late), sweep));
        Assert.Equal(selected.SupportingObservations, Assert.IsType<NasdaqHumanWickfillScenario2PostSweepReversalSelection.Unique>(selector.Select(before, sweep)).SupportingObservations);
    }

    [Theory]
    [InlineData("take")]
    [InlineData("h4")]
    [InlineData("session")]
    public void AReversalCannotBeReattachedToDifferentOpeningSweepAncestry(string difference)
    {
        var f = Build(); var target = Sweep(f);
        var other = difference switch { "take" => Build(takeEventId: "other:opening"), "h4" => Build(zoneUpper: 121), _ => Build(shift: 24) };
        var observation = Observation(Sweep(other), effective: difference == "session" ? At(37, 32) : At(13, 32),
            observed: difference == "session" ? At(37, 32) : At(13, 32));
        Assert.IsType<NasdaqHumanWickfillScenario2PostSweepReversalSelection.Missing>(selector.Select(Frame(f, At(14), observation), target));
    }

    [Fact]
    public void AVisibleOlderSessionAssertionCannotSatisfyTheNextSessionSweep()
    {
        var previous = Build(); var current = Build(shift: 24);
        var oldObservation = Observation(Sweep(previous));
        var frame = Frame(current, At(38), oldObservation);
        Assert.Contains(oldObservation, frame.InputObservations);
        Assert.IsType<NasdaqHumanWickfillScenario2PostSweepReversalSelection.Missing>(selector.Select(frame, Sweep(current)));
    }

    [Fact]
    public void MissingSourcesRemainDiagnosticsWhileAlteredSourcesAreUnusable()
    {
        var f = Build(); var sweep = Sweep(f);
        var absent = new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, new(5, TimeframeUnit.Minute), At(13, 30), At(13, 35), 100, 110, 90, 100, null);
        var missing = Observation(sweep, observed: At(14), sources: [absent]);
        var result = Assert.IsType<NasdaqHumanWickfillScenario2PostSweepReversalSelection.Missing>(selector.Select(Frame(f, At(14), missing), sweep));
        Assert.Same(missing, Assert.Single(result.UnavailableSourceObservations));
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanWickfillScenario2PostSweepReversalObservation>)result.UnavailableSourceObservations).Clear());
        var actual = Support(); var altered = new Candle(actual.ProviderId, actual.Symbol, actual.Timeframe, actual.OpenTimeUtc, actual.CloseTimeUtc, 100, 111, 90, 100, null);
        var invalid = Observation(sweep, observed: At(14), sources: [altered]);
        var unusable = Assert.IsType<NasdaqHumanWickfillScenario2PostSweepReversalSelection.Missing>(selector.Select(Frame(f, At(14), invalid), sweep));
        Assert.Empty(unusable.UnavailableSourceObservations);
    }

    [Fact]
    public void FutureOrCurrentlyUnusableParentCannotBeConsumed()
    {
        var f = Build(); var sweep = Sweep(f); var observation = Observation(sweep);
        Assert.IsType<NasdaqHumanWickfillScenario2PostSweepReversalSelection.Missing>(selector.Select(Frame(f, At(13, 30), observation), sweep));
        var broken = Sources().Select(c => c.Timeframe == NasdaqHumanH4ContextObservation.H4 && c.OpenTimeUtc == At(8)
            ? new Candle(c.ProviderId, c.Symbol, c.Timeframe, c.OpenTimeUtc, c.CloseTimeUtc, 100, 111, 90, 100, null) : c).ToArray();
        var context = Context(At(14), [.. f.Inputs, observation], sources: broken).WithPriorObservations(f.Context.PriorObservations);
        Assert.IsType<NasdaqHumanWickfillScenario2PostSweepReversalSelection.Missing>(selector.Select(context, sweep));
    }

    [Fact]
    public void SourceOrderAndSelectionAreDeterministicAndDoNotChangeStandardEvaluationsOrWorkflow()
    {
        var f = Build(); var sweep = Sweep(f);
        var sources = new[] { Support(), Sources().First(c => c.Timeframe == NasdaqHumanH4ContextObservation.H4) };
        var one = Observation(sweep, observed: At(14), sources: sources);
        var two = Observation(sweep, observed: At(14), sources: sources.Reverse());
        Assert.Equal(one.SupportingCandles.Select(c => (c.Timeframe, c.OpenTimeUtc)), two.SupportingCandles.Select(c => (c.Timeframe, c.OpenTimeUtc)));
        var forward = Assert.IsType<NasdaqHumanWickfillScenario2PostSweepReversalSelection.Unique>(selector.Select(Frame(f, At(14), one, two), sweep));
        var reverse = Assert.IsType<NasdaqHumanWickfillScenario2PostSweepReversalSelection.Unique>(selector.Select(Frame(f, At(14), two, one), sweep));
        Assert.Equal(JsonSerializer.Serialize(forward.SupportingObservations), JsonSerializer.Serialize(reverse.SupportingObservations));
        Assert.Equal(2, forward.SupportingObservations.Count);
        Assert.Contains(one, forward.SupportingObservations);
        Assert.Contains(two, forward.SupportingObservations);
        var evaluator = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll());
        var baseline = evaluator.Execute(LiquidityFixture.Definition, f.Context);
        var context = Frame(f, At(14), one, two);
        var history = JsonSerializer.Serialize(context.PriorObservations);
        selector.Select(context, sweep);
        var withEvidence = evaluator.Execute(LiquidityFixture.Definition, context);
        Assert.Equal(baseline.Evaluations, withEvidence.Evaluations);
        Assert.Equal(history, JsonSerializer.Serialize(context.PriorObservations));
        Assert.DoesNotContain(withEvidence.RuleFacts, r => r.RuleId.Value == "NQ-M1-003");
        Assert.False((object)one is IReplayRuleFact);
        Assert.False((object)forward is IReplayRuleFact);
        Assert.DoesNotContain(typeof(NasdaqHumanWickfillScenario2PostSweepReversalObservation).GetProperties(), p => p.PropertyType == typeof(StrategyVerdict));
    }

    [Fact]
    public void NullSelectorArgumentsAreRejected()
    {
        var f = Build(); var sweep = Sweep(f);
        Assert.Throws<ArgumentNullException>(() => selector.Select(null!, sweep));
        Assert.Throws<ArgumentNullException>(() => selector.Select(f.Context, null!));
    }

    private static NasdaqHumanWickfillScenario2PostSweepReversalObservation Observation(NasdaqWickfillScenario2OpeningSweepContext sweep,
        string eventId = "source:turn", DateTimeOffset? effective = null, DateTimeOffset? observed = null, string source = "review:turn", IEnumerable<Candle>? sources = null) =>
        new(sweep, eventId, effective ?? At(13, 32), observed ?? effective ?? At(13, 32), source, sources);
    private static NasdaqWickfillScenario2OpeningSweepContext Sweep(Fixture f) => NasdaqWickfillScenario2OpeningSweepContext.From(f.Context, f.Selection, f.Owner);
    private static StrategyReplayContext Frame(Fixture f, DateTimeOffset at, params IStrategyReplayInputObservation[] observations) =>
        Context(at, [.. f.Inputs, .. observations], shift: (int)(f.Context.AsOfUtc - At(14)).TotalHours).WithPriorObservations(f.Context.PriorObservations.Where(o => o.AsOfUtc < at));

    private static NasdaqHumanWickfillScenario2ContextSelection.Unique SelectContext(StrategyReplayContext context) =>
        Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Unique>(new NasdaqHumanWickfillScenario2ContextObservationSelector().Select(context, LiquidityFixture.Session(context.AsOfUtc)));

    private static NasdaqHumanH4ContextObservation H4(int shift = 0, decimal upper = 120, NasdaqHumanH4PermittedDirection direction = NasdaqHumanH4PermittedDirection.Sell) =>
        new(LiquidityFixture.Session(At(13 + shift)), new(direction, NasdaqHumanH4ContextKind.Wickfill, At(8 + shift), At(12 + shift), [],
            [new(NasdaqHumanH4StructuralRole.LowerHigh, 100, upper, NasdaqHumanH4ZoneBoundaryPrecision.Approximate, [At(8 + shift)])]), At(13 + shift, 5), "review:h4");
    private static NasdaqHumanWickfillScenario2ContextObservation PreOpen(NasdaqHumanH4ContextObservation h4, int shift = 0, DateTimeOffset? observed = null) =>
        new(h4, h4.Fact.ZoneAnchors.Single(), Sources(shift).Where(c => c.Timeframe == NasdaqHumanH4ContextObservation.H4),
            At(13 + shift, 10), observed ?? At(13 + shift, 10), "review:pre-open");
    private static Candle[] Sources(int shift = 0) => Enumerable.Range(-2, 14).Select(h => LiquidityFixture.Candle(h + shift))
        .Concat(new[] { 0, 4, 8 }.Select(h => LiquidityFixture.Candle(h + shift, hours: 4)))
        .Append(Support(shift)).ToArray();
    private static Candle Support(int shift = 0) => new(LiquidityFixture.Provider, LiquidityFixture.Symbol, new(30, TimeframeUnit.Minute),
        At(13 + shift, 30), At(14 + shift), 100, 110, 90, 100, null);
    private static DateTimeOffset[] Boundaries(int shift = 0) => [At(13 + shift, 5), At(13 + shift, 10), At(13 + shift, 15), At(13 + shift, 30), At(13 + shift, 31), At(13 + shift, 32), At(13 + shift, 33), At(14 + shift)];
    private static CandleSeries[] Series(int shift = 0, Candle[]? sources = null) => (sources ?? Sources(shift)).GroupBy(c => c.Timeframe)
        .Select(g => new CandleSeries(LiquidityFixture.Provider, LiquidityFixture.Symbol, g.Key, g.OrderBy(c => c.OpenTimeUtc)))
        .Append(new(LiquidityFixture.Provider, LiquidityFixture.Symbol, M1, Boundaries(shift).Select(t =>
            new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, M1, t.AddMinutes(-1), t, 100, 110, 90, 100, null)))).ToArray();
    private static StrategyReplayContext Context(DateTimeOffset time, IStrategyReplayInputObservation[] inputs, int shift = 0, Candle[]? sources = null)
    {
        var cursor = new MultiTimeframeCandleReplayCursor(Series(shift, sources));
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == time) return new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition, frame, inputs);
        throw new InvalidOperationException("Fixture frame missing.");
    }

    private static Fixture Build(NasdaqLiquidityTakeReference.SessionEndpoint endpoint = NasdaqLiquidityTakeReference.SessionEndpoint.LondonHigh,
        NasdaqHumanH4PermittedDirection direction = NasdaqHumanH4PermittedDirection.Sell, int shift = 0, bool structural = false,
        DateTimeOffset? preOpenObserved = null, DateTimeOffset? takeEffective = null, string takeEventId = "synthetic:opening", decimal zoneUpper = 120)
    {
        var h4 = H4(shift, upper: zoneUpper, direction: direction);
        var preOpen = PreOpen(direction == NasdaqHumanH4PermittedDirection.Sell ? h4 : H4(shift), shift, preOpenObserved);
        var upstream = Context(At(13 + shift, 5), [], shift);
        NasdaqLiquidityTakeReference reference = new NasdaqLiquidityTakeReference.SessionLevel(upstream,
            new NasdaqSessionLiquidityCalculator().Calculate(upstream), endpoint, upstream.AsOfUtc, "review:endpoint");
        var inputs = new List<IStrategyReplayInputObservation> { h4, preOpen,
            new NasdaqPreparationCompletionObservation(LiquidityFixture.Definition.StrategyId, LiquidityFixture.Definition.Version,
                LiquidityFixture.Provider, LiquidityFixture.Symbol, preOpen.Session.TradingDay, At(13 + shift, 15), "review:prep") };
        if (structural)
        {
            var member = LiquidityFixture.Human(Sources(shift).First(c => c.Timeframe == new Timeframe(1, TimeframeUnit.Hour)), price: 110);
            var observation = new NasdaqHumanStructuralLiquidityObservation(preOpen.Session, [member], At(12 + shift), At(13 + shift, 5), "review:structural");
            inputs.Add(observation);
            var selected = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(new NasdaqHumanStructuralLiquidityObservationSelector()
                .Select(Context(At(13 + shift, 15), inputs.ToArray(), shift), preOpen.Session));
            reference = new NasdaqLiquidityTakeReference.Structural(selected, member);
        }
        var take = new NasdaqHumanLiquidityTakeObservation(reference, new NasdaqHumanLiquidityTakeEvent.Documented(
            LiquidityFixture.Provider, LiquidityFixture.Symbol, takeEventId, reference.ReferencePrice + (reference.Side == NasdaqStructuralLiquiditySide.High ? 1 : -1),
            takeEffective ?? At(13 + shift, 31), "review:event"), upstream.AsOfUtc, At(13 + shift, 31), "review:take");
        inputs.Add(take); inputs.Add(new NasdaqHumanRelevantLiquidityTakeObservation(take, take.ObservedAtUtc, "review:relevant"));
        var definitions = new StrategyDefinitionCatalog().GetAll();
        var run = new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(MoneyWayReplayRuleEvaluators.GetAll()),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), new()).Execute(LiquidityFixture.Definition, Series(shift), inputs);
        var owner = run.StrategyObservations.Single(o => o.AsOfUtc == At(13 + shift, 31));
        var context = Context(At(14 + shift), inputs.ToArray(), shift).WithPriorObservations(run.StrategyObservations.Where(o => o.AsOfUtc < At(14 + shift)));
        return new(context, SelectContext(context), owner, inputs.ToArray());
    }

    private sealed record Fixture(StrategyReplayContext Context, NasdaqHumanWickfillScenario2ContextSelection.Unique Selection,
        StrategyReplayContextObservation Owner, IStrategyReplayInputObservation[] Inputs);
}

using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanM5FvgTests
{
    private readonly NasdaqHumanM5FvgObservationSelector selector = new();
    private readonly NasdaqHumanM5FvgQualityObservationSelector qualitySelector = new();
    private readonly NasdaqHumanLiquidityTakeObservation take = M5Fixture.Take(false);
    private NasdaqHumanM5TriggerObservation Trigger(bool ifvg = false, decimal swing = 100,
        NasdaqHumanLiquidityTakeObservation? otherTake = null)
    {
        var reference = M5Fixture.Five(M5Fixture.At(14));
        NasdaqHumanM5TriggerEvent ev = ifvg
            ? new NasdaqHumanM5TriggerEvent.Ifvg(M5Fixture.Five(M5Fixture.At(14, 10)), NasdaqHumanH4PermittedDirection.Buy, [reference], [reference])
            : new NasdaqHumanM5TriggerEvent.StructuralChange(M5Fixture.Five(M5Fixture.At(14, 10)), NasdaqHumanH4PermittedDirection.Buy, [reference], [reference], swing);
        return new(otherTake ?? take, ev, M5Fixture.At(14, 10), "trigger:source");
    }
    private NasdaqHumanM5FvgObservation Fvg(int minute = 10, int? observed = null, string source = "fvg:source",
        NasdaqHumanM5TriggerObservation? trigger = null, bool unavailable = false, bool mismatch = false)
    {
        var close = M5Fixture.At(14, minute);
        return new(trigger ?? Trigger(), new([M5Fixture.Five(unavailable ? M5Fixture.At(13, 20) : close.AddMinutes(-10)),
            M5Fixture.Five(close.AddMinutes(-5)), M5Fixture.Five(close, mismatch ? 101 : 100)]), M5Fixture.At(14, observed ?? Math.Max(minute, 10)), source);
    }
    private static NasdaqHumanM5FvgQualityObservation Quality(NasdaqHumanM5FvgObservation fvg,
        NasdaqHumanM5FvgQualityDecision decision = NasdaqHumanM5FvgQualityDecision.Approved, int effective = 10, int observed = 15,
        string source = "quality:source", string rationale = "Source review approves the selected FVG strength.") =>
        new(new(fvg, decision, M5Fixture.At(14, effective), rationale), M5Fixture.At(14, observed), source);
    private static MultiTimeframeStrategyBacktestRun Run(IStrategyReplayInputObservation[] inputs)
    {
        var definitions = new StrategyDefinitionCatalog().GetAll();
        return new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(MoneyWayReplayRuleEvaluators.GetAll()),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), new(), new(definitions, MoneyWayReplayLifecyclePolicies.GetAll()), new(new()))
            .Execute(LiquidityFixture.Definition, M5Fixture.Series(), inputs);
    }
    private (StrategyReplayContext Context, NasdaqHumanM5TriggerRuleFact Trigger) Established(int minute = 15,
        NasdaqHumanM5TriggerObservation? trigger = null, params IStrategyReplayInputObservation[] extra)
    {
        trigger ??= Trigger();
        var inputs = M5Fixture.Inputs(NasdaqHumanH4PermittedDirection.Buy, take).Concat([trigger]).Concat(extra).ToArray();
        var history = Run(inputs).StrategyObservations.Where(o => o.AsOfUtc < M5Fixture.At(14, minute)).ToArray();
        var fact = history.SelectMany(o => o.RuleFacts).Where(f => f.RuleId.Value == "NQ-M5-001")
            .Select(f => f.Fact).OfType<NasdaqHumanM5TriggerRuleFact>().Last();
        return (M5Fixture.Context(M5Fixture.At(14, minute), inputs).WithPriorObservations(history), fact);
    }
    private NasdaqHumanM5FvgSelection Select(params IStrategyReplayInputObservation[] extra)
    {
        var established = Established(extra: extra);
        return selector.Select(established.Context, established.Trigger);
    }
    private NasdaqHumanM5FvgQualitySelection SelectQuality(params IStrategyReplayInputObservation[] extra)
    {
        var e = Established(extra: extra);
        var fvg = Assert.IsType<NasdaqHumanM5FvgSelection.Unique>(selector.Select(e.Context, e.Trigger));
        return qualitySelector.Select(e.Context, e.Trigger, fvg);
    }

    [Fact]
    public void MissingDoesNotInventFvgOrQuality() => Assert.IsType<NasdaqHumanM5FvgSelection.Missing>(Select());

    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 10)]
    [InlineData(false, 15)]
    [InlineData(true, 15)]
    public void SameCloseAndLaterFvgAreIndependentOfScOrIfvg(bool ifvg, int minute)
    {
        var trigger = Trigger(ifvg);
        var e = Established(trigger: trigger);
        Assert.IsType<NasdaqHumanM5FvgSelection.Missing>(selector.Select(e.Context, e.Trigger));
        var fvg = Fvg(minute, trigger: trigger);
        e = Established(trigger: trigger, extra: [fvg]);
        var selected = Assert.IsType<NasdaqHumanM5FvgSelection.Unique>(selector.Select(e.Context, e.Trigger));
        Assert.Same(fvg, selected.Fact);
        Assert.Equal(M5Fixture.At(14, minute), fvg.EffectiveAtUtc);
        Assert.Same(fvg.Event.Candle3, fvg.Event.SourceCandles[2]);
        Assert.Equal(3, fvg.Event.SourceCandles.Count);
        Assert.Equal(trigger.Event.Direction, fvg.Direction);
        // Fixture prices have no geometric gap; human classification is deliberately not an OHLC detector.
        Assert.Equal(fvg.Event.Candle1.High, fvg.Event.Candle3.High);
    }

    [Fact]
    public void PreTriggerCannotBeReused() => Assert.IsType<NasdaqHumanM5FvgSelection.Missing>(Select(Fvg(5, observed: 15)));

    [Fact]
    public void DifferentExactTriggerAndTakeDoNotMatchByTimeKindOrSession()
    {
        Assert.IsType<NasdaqHumanM5FvgSelection.Missing>(Select(Fvg(trigger: Trigger(swing: 101))));
        Assert.IsType<NasdaqHumanM5FvgSelection.Missing>(Select(Fvg(trigger: Trigger(otherTake: M5Fixture.Take(false, eventId: "other setup")))));
        Assert.IsType<NasdaqHumanM5FvgSelection.Missing>(Select(Fvg(trigger: Trigger(true))));
    }

    [Fact]
    public void NoCanonicalPriorTriggerCannotBeBypassedWithCandidateInputs()
    {
        var e = Established(extra: [Fvg()]);
        Assert.IsType<NasdaqHumanM5FvgSelection.Missing>(selector.Select(M5Fixture.Context(M5Fixture.At(14, 15), [Fvg()]), e.Trigger));
        var before = e.Context.PriorObservations.Where(o => o.AsOfUtc < M5Fixture.At(14, 10));
        Assert.IsType<NasdaqHumanM5FvgSelection.Missing>(selector.Select(M5Fixture.Context(M5Fixture.At(14, 10), [Fvg()]).WithPriorObservations(before), e.Trigger));
    }

    [Fact]
    public void CompatibleDuplicatesPreserveEveryProvenanceAndAreImmutable()
    {
        var a = Fvg(); var b = Fvg(observed: 15, source: "another reviewer");
        var u = Assert.IsType<NasdaqHumanM5FvgSelection.Unique>(Select(b, a));
        Assert.Equal(new[] { a, b }, u.SupportingObservations);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanM5FvgObservation>)u.SupportingObservations).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<Candle>)a.Event.SourceCandles).Clear());
    }

    [Fact]
    public void ExplicitSelectedFvgDisagreementConflictsWithoutFirstOrLatestRanking()
    {
        var a = Fvg(); var b = Fvg(15);
        var x = Assert.IsType<NasdaqHumanM5FvgSelection.Conflict>(Select(a, b));
        var y = Assert.IsType<NasdaqHumanM5FvgSelection.Conflict>(Select(b, a));
        Assert.Equal(x.SupportingObservations, y.SupportingObservations);
        Assert.Equal(2, x.SupportingObservations.Count);
    }

    [Fact]
    public void UnavailableNamedSourcesAreRetainedInEverySelectionVariant()
    {
        var missing = Fvg(unavailable: true, source: "absent source");
        Assert.Same(missing, Assert.Single(Assert.IsType<NasdaqHumanM5FvgSelection.Missing>(Select(missing)).UnavailableSourceObservations));
        Assert.Same(missing, Assert.Single(Assert.IsType<NasdaqHumanM5FvgSelection.Unique>(Select(Fvg(), missing)).UnavailableSourceObservations));
        var conflict = Assert.IsType<NasdaqHumanM5FvgSelection.Conflict>(Select(Fvg(), Fvg(15), missing));
        Assert.Same(missing, Assert.Single(conflict.UnavailableSourceObservations));
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanM5FvgObservation>)conflict.UnavailableSourceObservations).Clear());
    }

    [Fact]
    public void SourceMismatchIsUnusableRatherThanUnavailable()
    {
        var s = Assert.IsType<NasdaqHumanM5FvgSelection.Missing>(Select(Fvg(mismatch: true)));
        Assert.Empty(s.UnavailableSourceObservations);
    }

    [Fact]
    public void FutureObservationAndOpenCandleThreeCannotChangeEarlierFrame()
    {
        var late = Fvg(observed: 20); var futureSource = Fvg(20);
        Assert.IsType<NasdaqHumanM5FvgSelection.Missing>(Select(late, futureSource));
        var e = Established(20, extra: [late]);
        Assert.Same(late, Assert.IsType<NasdaqHumanM5FvgSelection.Unique>(selector.Select(e.Context, e.Trigger)).Fact);
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5FvgObservation(Trigger(), futureSource.Event, M5Fixture.At(14, 15), "open third candle"));
    }

    [Fact]
    public void FutureMarketPathAndAnnotationsLeaveStoredHistoricalContextUnchanged()
    {
        var fvg = Fvg(); var quality = Quality(fvg);
        var e = Established(extra: [fvg, quality]);
        var bounded = M5Fixture.Context(M5Fixture.At(14, 15), [fvg, quality], includeFutureCandles: false).WithPriorObservations(e.Context.PriorObservations);
        var expanded = M5Fixture.Context(M5Fixture.At(14, 15), [fvg, quality, Fvg(20), Quality(fvg, observed: 30)]).WithPriorObservations(e.Context.PriorObservations);
        var original = Assert.IsType<NasdaqHumanM5FvgSelection.Unique>(selector.Select(bounded, e.Trigger));
        Assert.Equal(original.SupportingObservations, Assert.IsType<NasdaqHumanM5FvgSelection.Unique>(selector.Select(expanded, e.Trigger)).SupportingObservations);
        Assert.Equal(Assert.IsType<NasdaqHumanM5FvgQualitySelection.Unique>(qualitySelector.Select(bounded, e.Trigger, original)).SupportingObservations,
            Assert.IsType<NasdaqHumanM5FvgQualitySelection.Unique>(qualitySelector.Select(expanded, e.Trigger, original)).SupportingObservations);
        Assert.Equal(2, bounded.InputObservations.Count);
        Assert.Equal(2, expanded.InputObservations.Count);
    }

    [Fact]
    public void QualityMissingIsSeparateFromFvgExistence() => Assert.IsType<NasdaqHumanM5FvgQualitySelection.Missing>(SelectQuality(Fvg()));

    [Theory]
    [InlineData(NasdaqHumanM5FvgQualityDecision.Approved)]
    [InlineData(NasdaqHumanM5FvgQualityDecision.Rejected)]
    public void QualityApprovalAndRejectionAreSemanticFactsAtReviewTime(NasdaqHumanM5FvgQualityDecision decision)
    {
        var fvg = Fvg(); var quality = Quality(fvg, decision, rationale: "Mentor review of the exact selected FVG.");
        var u = Assert.IsType<NasdaqHumanM5FvgQualitySelection.Unique>(SelectQuality(fvg, quality));
        Assert.Same(quality, u.Fact);
        Assert.Equal(decision, u.Fact.Fact.Decision);
        Assert.Equal(M5Fixture.At(14, 10), u.Fact.EffectiveAtUtc);
        Assert.Equal(M5Fixture.At(14, 15), u.Fact.ObservedAtUtc);
        Assert.Same(fvg, u.Fact.Fact.Fvg);
    }

    [Fact]
    public void CompatibleQualityReviewsKeepRationaleAndProvenanceWithoutVoting()
    {
        var fvg = Fvg(); var a = Quality(fvg); var b = Quality(fvg, observed: 14, source: "second review", rationale: "Independent source approves the same quality fact.");
        var first = Assert.IsType<NasdaqHumanM5FvgQualitySelection.Unique>(SelectQuality(fvg, a, b));
        var reversed = Assert.IsType<NasdaqHumanM5FvgQualitySelection.Unique>(SelectQuality(b, fvg, a));
        Assert.Equal(first.SupportingObservations, reversed.SupportingObservations);
        Assert.Equal(2, first.SupportingObservations.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanM5FvgQualityObservation>)first.SupportingObservations).Clear());
    }

    [Fact]
    public void ContradictoryQualityDecisionsConflictAndRetainAlternatives()
    {
        var fvg = Fvg(); var a = Quality(fvg); var b = Quality(fvg, NasdaqHumanM5FvgQualityDecision.Rejected);
        var x = Assert.IsType<NasdaqHumanM5FvgQualitySelection.Conflict>(SelectQuality(fvg, a, b));
        Assert.Equal(x.SupportingObservations, Assert.IsType<NasdaqHumanM5FvgQualitySelection.Conflict>(SelectQuality(b, a, fvg)).SupportingObservations);
        Assert.Equal(2, x.SupportingObservations.Count);
    }

    [Fact]
    public void WrongExactFvgCannotSupplyQuality() => Assert.IsType<NasdaqHumanM5FvgQualitySelection.Missing>(SelectQuality(Fvg(), Quality(Fvg(15), effective: 15)));

    [Fact]
    public void FutureQualityAvailabilityBecomesVisibleOnlyLater()
    {
        var fvg = Fvg(); var quality = Quality(fvg, observed: 20);
        Assert.IsType<NasdaqHumanM5FvgQualitySelection.Missing>(SelectQuality(fvg, quality));
        var e = Established(20, extra: [fvg, quality]);
        var selected = Assert.IsType<NasdaqHumanM5FvgSelection.Unique>(selector.Select(e.Context, e.Trigger));
        Assert.Same(quality, Assert.IsType<NasdaqHumanM5FvgQualitySelection.Unique>(qualitySelector.Select(e.Context, e.Trigger, selected)).Fact);
    }

    [Fact]
    public void StaleUniqueCannotHideNewFvgConflictOrUnavailableSources()
    {
        var fvg = Fvg(); var quality = Quality(fvg);
        var e = Established(extra: [fvg, quality]);
        var selected = Assert.IsType<NasdaqHumanM5FvgSelection.Unique>(selector.Select(e.Context, e.Trigger));
        var conflict = Established(extra: [fvg, Fvg(15), quality]);
        Assert.IsType<NasdaqHumanM5FvgQualitySelection.Missing>(qualitySelector.Select(conflict.Context, conflict.Trigger, selected));
        // Preserve canonical boundary/step numbering while omitting the exact named candle identity.
        var missingSeries = M5Fixture.Series().Select(s => s.Timeframe != NasdaqHumanM5TriggerEvent.M5 ? s :
            new CandleSeries(s.ProviderId, s.Symbol, s.Timeframe, s.Candles.Select(c => c.OpenTimeUtc != fvg.Event.Candle1.OpenTimeUtc ? c :
                new Candle(c.ProviderId, c.Symbol, c.Timeframe, c.OpenTimeUtc.AddSeconds(1), c.CloseTimeUtc, c.Open, c.High, c.Low, c.Close, c.Volume)))).ToArray();
        var cursor = new MoneyWay.Domain.MarketData.Replay.MultiTimeframeCandleReplayCursor(missingSeries);
        StrategyReplayContext? context = null;
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == M5Fixture.At(14, 15)) context = new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition, frame, [fvg, quality]).WithPriorObservations(e.Context.PriorObservations);
        var unavailable = Assert.IsType<NasdaqHumanM5FvgQualitySelection.Missing>(qualitySelector.Select(context!, e.Trigger, selected));
        Assert.Same(quality, Assert.Single(unavailable.UnavailableSourceObservations));
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanM5FvgQualityObservation>)unavailable.UnavailableSourceObservations).Clear());
    }

    [Fact]
    public void TerminalSessionCannotReviveFvgOrQuality()
    {
        var fvg = Fvg(); var quality = Quality(fvg);
        var e = Established(extra: [fvg, quality]);
        var selected = Assert.IsType<NasdaqHumanM5FvgSelection.Unique>(selector.Select(e.Context, e.Trigger));
        var invalid = M5Fixture.Take(true, effective: M5Fixture.At(14, 20), observed: M5Fixture.At(14, 20));
        var inputs = M5Fixture.Inputs(NasdaqHumanH4PermittedDirection.Buy, take).Concat([Trigger(), fvg, quality, invalid,
            new NasdaqHumanRelevantLiquidityTakeObservation(invalid, invalid.ObservedAtUtc, "terminal event", take)]).ToArray();
        var history = Run(inputs).StrategyObservations.Where(o => o.AsOfUtc < M5Fixture.At(14, 30)).ToArray();
        Assert.Contains(history.SelectMany(o => o.RuleFacts).Select(f => f.Fact).OfType<NasdaqLiquidityTakeRuleFact>(), f => f.IsSessionInvalidated);
        var context = M5Fixture.Context(M5Fixture.At(14, 30), inputs).WithPriorObservations(history);
        Assert.IsType<NasdaqHumanM5FvgSelection.Missing>(selector.Select(context, e.Trigger));
        Assert.IsType<NasdaqHumanM5FvgQualitySelection.Missing>(qualitySelector.Select(context, e.Trigger, selected));
    }

    [Fact]
    public void WrongDayAndCutoffCannotReuseEvidence()
    {
        var fvg = Fvg(); var e = Established(extra: [fvg]);
        foreach (var time in new[] { M5Fixture.At(16), M5Fixture.At(38, 5) })
            Assert.IsType<NasdaqHumanM5FvgSelection.Missing>(selector.Select(M5Fixture.Context(time, [fvg]).WithPriorObservations(e.Context.PriorObservations), e.Trigger));
    }

    [Fact]
    public void EventInvariantsSnapshotAllThreeExactSourcesWithoutInferringZone()
    {
        var sources = Fvg().Event.SourceCandles.ToList();
        var ev = new NasdaqHumanM5FvgEvent(sources.AsEnumerable().Reverse());
        sources.Clear();
        Assert.Equal(3, ev.SourceCandles.Count);
        Assert.Equal(M5Fixture.At(14, 10), ev.EffectiveAtUtc);
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5FvgEvent([]));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5FvgEvent([ev.Candle1, ev.Candle1, ev.Candle3]));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5FvgEvent([LiquidityFixture.Candle(13), ev.Candle2, ev.Candle3]));
        var foreign = new Candle(new("foreign"), LiquidityFixture.Symbol, ev.Candle1.Timeframe, ev.Candle1.OpenTimeUtc, ev.Candle1.CloseTimeUtc, 100, 110, 90, 100, null);
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5FvgEvent([foreign, ev.Candle2, ev.Candle3]));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5FvgObservation(Trigger(), ev, M5Fixture.At(14, 5), "early"));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5FvgObservation(Trigger(), ev, M5Fixture.At(14, 15).ToOffset(TimeSpan.FromHours(1)), "local"));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5FvgObservation(Trigger(), ev, M5Fixture.At(14, 15), " "));
    }

    [Fact]
    public void EquivalentTriggerSupportDoesNotCreateCompetingIdentity()
    {
        var trigger = Trigger();
        var duplicate = new NasdaqHumanM5TriggerObservation(take, trigger.Event, M5Fixture.At(14, 15), "other trigger provenance");
        var a = Fvg(trigger: trigger); var b = Fvg(trigger: duplicate, observed: 15, source: "other FVG provenance");
        Assert.Equal(2, Assert.IsType<NasdaqHumanM5FvgSelection.Unique>(Select(a, b)).SupportingObservations.Count);
    }

    [Fact]
    public void ReplayInputTransportRejectsForeignStrategyForFvgAndQuality()
    {
        var fvg = Fvg(); var quality = Quality(fvg);
        var cursor = new MoneyWay.Domain.MarketData.Replay.MultiTimeframeCandleReplayCursor(M5Fixture.Series());
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == M5Fixture.At(14, 15))
            {
                Assert.Throws<ArgumentException>(() => new CreateStrategyReplayContextUseCase().Execute(
                    MoneyWay.Application.StrategyDefinitions.Forex.MoneyWayForexStrategyDefinition.Instance, frame, [fvg, quality]));
                return;
            }
        Assert.Fail("Fixture boundary not found.");
    }

    [Fact]
    public void LatestCanonicalTriggerConflictBlocksStalePositiveFact()
    {
        var fvg = Fvg(); var e = Established(extra: [fvg]);
        var competing = new NasdaqHumanM5TriggerObservation(take, Trigger(swing: 101).Event, M5Fixture.At(14, 20), "late conflicting trigger");
        var inputs = M5Fixture.Inputs(NasdaqHumanH4PermittedDirection.Buy, take).Concat([Trigger(), competing, fvg]).ToArray();
        var history = Run(inputs).StrategyObservations.Where(o => o.AsOfUtc < M5Fixture.At(14, 30)).ToArray();
        var context = M5Fixture.Context(M5Fixture.At(14, 30), inputs).WithPriorObservations(history);
        Assert.IsType<NasdaqHumanM5FvgSelection.Missing>(selector.Select(context, e.Trigger));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ForeignProviderOrInstrumentCannotBindToTrigger(bool provider)
    {
        var ev = Fvg().Event;
        var foreign = new NasdaqHumanM5FvgEvent(ev.SourceCandles.Select(c => new Candle(provider ? new("other") : c.ProviderId,
            provider ? c.Symbol : new("OTHER"), c.Timeframe, c.OpenTimeUtc, c.CloseTimeUtc, c.Open, c.High, c.Low, c.Close, c.Volume)));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5FvgObservation(Trigger(), foreign, M5Fixture.At(14, 15), "foreign sources"));
    }

    [Theory]
    [InlineData(1, TimeframeUnit.Minute)]
    [InlineData(1, TimeframeUnit.Hour)]
    [InlineData(4, TimeframeUnit.Hour)]
    public void NonM5SourceCannotEnterFvgEvidence(int amount, TimeframeUnit unit)
    {
        var ev = Fvg().Event;
        var other = new Candle(ev.Candle1.ProviderId, ev.Candle1.Symbol, new(amount, unit), ev.Candle1.OpenTimeUtc,
            ev.Candle1.CloseTimeUtc, 100, 110, 90, 100, null);
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5FvgEvent([other, ev.Candle2, ev.Candle3]));
    }

    [Fact]
    public void QualityInvariantsDoNotAcceptBackdatingOrArbitraryDecisionDomains()
    {
        var fvg = Fvg();
        Assert.Throws<ArgumentOutOfRangeException>(() => new NasdaqHumanM5FvgQualityFact(fvg, (NasdaqHumanM5FvgQualityDecision)42, fvg.EffectiveAtUtc, "review"));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5FvgQualityFact(fvg, NasdaqHumanM5FvgQualityDecision.Approved, M5Fixture.At(14, 5), "review"));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5FvgQualityFact(fvg, NasdaqHumanM5FvgQualityDecision.Approved, fvg.EffectiveAtUtc, " "));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5FvgQualityObservation(Quality(fvg).Fact, M5Fixture.At(14, 5), "early"));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5FvgQualityObservation(Quality(fvg).Fact, M5Fixture.At(14, 15).ToOffset(TimeSpan.FromHours(1)), "local"));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5FvgQualityObservation(Quality(fvg).Fact, M5Fixture.At(14, 15), " "));
    }
}

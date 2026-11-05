using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;
using F = MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs.NasdaqHumanWickfillScenario2ReversalEvaluatorTests;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

// All prices, times, source labels and candles here are synthetic, never Replay #2 evidence.
public sealed class NasdaqHumanWickfillScenario2StopLossTests
{
    private static readonly NasdaqHumanWickfillScenario2StopLossObservationSelector Selector = new();
    private static DateTimeOffset At(int hour, int minute = 0) => LiquidityFixture.At(hour).AddMinutes(minute);
    private sealed record Setup(StrategyReplayContextObservation Owner, StrategyReplayContextObservation[] History,
        IStrategyReplayInputObservation[] Inputs);

    private static Setup BuildStopSetup()
    {
        var f = F.Build();
        var inputs = f.Inputs.Concat([F.Observation(f, NasdaqStrategyPath.WickfillScenario2), F.Reversal(f)]).ToArray();
        // Extend the actual prefix, retaining f.Owner by reference rather than rerunning its Start.
        var history = f.Run.StrategyObservations.Where(o => o.AsOfUtc <= At(13, 31)).ToList();
        foreach (var time in new[] { At(13, 32), At(13, 33) })
        {
            var context = F.Context(time, inputs).WithPriorObservations(history);
            var raw = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll()).Execute(LiquidityFixture.Definition, context);
            var advanced = new AdvanceStrategyReplayLifecycleUseCase(new()).Execute(MoneyWayReplayWorkflowDefinitions.GetAll().Single(),
                new MoneyWayNasdaqReplayLifecyclePolicy(), raw, history.Last().LifecycleProgression);
            history.Add(raw.WithProgressions(advanced.WorkflowProgression, advanced.LifecycleProgression));
        }
        var owner = history.Last();
        Assert.IsType<NasdaqWickfillScenario2PreEntryEligibilityRuleFact>(owner.RuleFacts.Single(f => f.RuleId.Value == "NQ-WF2-002").Fact);
        return new(owner, history.ToArray(), inputs);
    }

    private static NasdaqHumanWickfillScenario2StopLossReference Reference(string id = "synthetic:peak", decimal price = 115,
        DateTimeOffset? observed = null, IEnumerable<Candle>? sources = null, string source = "synthetic:peak-review") =>
        new(LiquidityFixture.Provider, LiquidityFixture.Symbol, id, price, observed ?? At(13, 31), source, sources);
    private static NasdaqHumanWickfillScenario2StopLossObservation Stop(Setup setup, decimal? price = 116,
        DateTimeOffset? effective = null, DateTimeOffset? observed = null, string source = "synthetic:initial-sl",
        NasdaqHumanWickfillScenario2StopLossReference? reference = null) =>
        new(setup.Owner, reference ?? Reference(), price, effective ?? At(13, 31), observed ?? At(13, 31), source);
    private static StrategyReplayContext Frame(Setup setup, DateTimeOffset? time = null,
        params IStrategyReplayInputObservation[] extra) => F.Context(time ?? At(14), setup.Inputs.Concat(extra).ToArray())
        .WithPriorObservations(setup.History.Where(o => o.AsOfUtc < (time ?? At(14))));

    [Fact]
    public void CompleteImmutableEvidenceRetainsNativeIdentityExactOwnersAndUnclampedChronology()
    {
        var setup = BuildStopSetup(); var stop = Stop(setup);
        var selected = Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Unique>(Selector.Select(Frame(setup, null, stop)));
        Assert.Same(stop, selected.Fact);
        Assert.Same(setup.Owner, stop.EligibilityOwner);
        Assert.Same(setup.History.Single(o => o.AsOfUtc == At(13, 31)), stop.StartOwner);
        Assert.Same(stop.PreEntryEligibility.Reversal.Reversal.Fact.OpeningSweep.Take,
            stop.StartOwner.RuleFacts.Single(f => f.RuleId.Value == "NQ-LIQ-003").Fact);
        Assert.Equal(NasdaqStrategyPath.WickfillScenario2, stop.Path);
        Assert.Equal(NasdaqHumanH4PermittedDirection.Sell, stop.Direction);
        Assert.Equal(stop.Session.ProviderId, stop.ProtectiveReference.ProviderId);
        Assert.Equal(stop.Session.Symbol, stop.ProtectiveReference.Symbol);
        Assert.Equal(At(13, 31), stop.EffectiveAtUtc);
        Assert.Equal(At(13, 31), stop.ObservedAtUtc);
        Assert.True(stop.ObservedAtUtc < stop.PreEntryEligibility.EligibilityEffectiveAtUtc);
        Assert.True(stop.ObservedAtUtc < stop.EligibilityOwner.AsOfUtc);
        Assert.Equal(116, stop.StopPrice);
        Assert.All(stop.GetType().GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.All(stop.ProtectiveReference.GetType().GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.False(typeof(IReplayRuleFact).IsAssignableFrom(stop.GetType()));
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("reference")]
    [InlineData("source")]
    [InlineData("reference-source")]
    [InlineData("reference-id")]
    [InlineData("effective")]
    [InlineData("observed")]
    [InlineData("reference-observed")]
    [InlineData("reversed")]
    [InlineData("non-utc")]
    [InlineData("wrong-day")]
    [InlineData("unavailable-reference")]
    [InlineData("equal-stop")]
    [InlineData("below-stop")]
    public void IncompleteOrInvalidEvidenceCannotBecomeAnObservation(string invalid)
    {
        var setup = BuildStopSetup();
        Assert.ThrowsAny<ArgumentException>(() =>
        {
            var reference = invalid switch
            {
                "reference-source" => Reference(source: " "),
                "reference-id" => Reference(id: ""),
                "reference-observed" => Reference(observed: default(DateTimeOffset)),
                "unavailable-reference" => Reference(observed: At(14)),
                _ => Reference(),
            };
            _ = new NasdaqHumanWickfillScenario2StopLossObservation(setup.Owner,
                invalid == "reference" ? null! : reference,
                invalid == "stop" ? null : invalid == "equal-stop" ? 115 : invalid == "below-stop" ? 114 : 116,
                invalid == "effective" ? default : invalid == "non-utc" ? At(13, 31).ToOffset(TimeSpan.FromHours(-5))
                    : invalid == "wrong-day" ? At(13, 31).AddDays(1) : At(13, 31),
                invalid == "observed" ? default : invalid == "reversed" ? At(13, 30) : At(13, 31),
                invalid == "source" ? "" : "synthetic:sl");
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ForeignPriceDomainIncludingUnmappedM30VisualEvidenceIsRejected(bool provider)
    {
        var setup = BuildStopSetup();
        var reference = new NasdaqHumanWickfillScenario2StopLossReference(provider ? new("foreign-mt5") : LiquidityFixture.Provider,
            provider ? LiquidityFixture.Symbol : new("USTEC"), "external:M30-wick", 115, At(13, 31), "external:visual-only-no-native-mapping");
        Assert.Throws<ArgumentException>(() => new NasdaqHumanWickfillScenario2StopLossObservation(setup.Owner, reference, 116,
            At(13, 31), At(13, 31), "synthetic:sl"));
    }

    [Fact]
    public void StandardAndUnestablishedOwnersCannotSupplyScenario2Ancestry()
    {
        var f = F.Build();
        Assert.Throws<ArgumentException>(() => new NasdaqHumanWickfillScenario2StopLossObservation(f.Owner, Reference(), 116,
            At(13, 31), At(13, 31), "synthetic:sl"));
        var run = F.Run(f.Inputs.Concat([F.Observation(f)]).ToArray());
        Assert.Throws<ArgumentException>(() => new NasdaqHumanWickfillScenario2StopLossObservation(
            run.StrategyObservations.Single(o => o.AsOfUtc == At(13, 33)), Reference(), 116, At(13, 31), At(13, 31), "synthetic:sl"));
    }

    [Fact]
    public void ForeignRunWithIdenticalCoordinatesCannotQualifyEvenWithCopiedOwnerFacts()
    {
        var setup = BuildStopSetup(); var foreign = BuildStopSetup(); var stop = Stop(foreign);
        Assert.Equal(setup.Owner.AsOfUtc, foreign.Owner.AsOfUtc);
        Assert.Equal(setup.Owner.LifecycleProgression!.ActiveInstance!.InstanceId, foreign.Owner.LifecycleProgression!.ActiveInstance!.InstanceId);
        Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Missing>(Selector.Select(Frame(setup, null, stop)));
        // Poison a local history slot with the foreign fact: matching time/ordinal still does not establish its Start reference.
        var poisoned = new StrategyReplayContextObservation(setup.Owner.StrategyId, setup.Owner.StrategyVersion, setup.Owner.ProviderId,
            setup.Owner.Symbol, setup.Owner.Step, setup.Owner.AsOfUtc, setup.Owner.Evaluations,
            setup.Owner.WorkflowProgression, setup.Owner.LifecycleProgression, ruleFacts: foreign.Owner.RuleFacts);
        var context = Frame(setup, null, stop).WithPriorObservations(setup.History.Take(setup.History.Length - 1).Append(poisoned));
        Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Missing>(Selector.Select(context));
    }

    [Fact]
    public void PriorOwnerIsRequiredAndRawHistoricalTimesNeverCreateSameFrameEligibility()
    {
        var setup = BuildStopSetup(); var stop = Stop(setup);
        Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Missing>(Selector.Select(Frame(setup, At(13, 32), stop)));
        Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Missing>(Selector.Select(Frame(setup, At(13, 33), stop)));
        Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Unique>(Selector.Select(Frame(setup, At(14), stop)));
        Assert.Equal(At(13, 31), stop.ObservedAtUtc);
    }

    [Fact]
    public void FutureAssertionAppearsOnlyAtItsOriginalAvailabilityAndDoesNotRewriteEarlierSelection()
    {
        var setup = BuildStopSetup(); var stop = Stop(setup, observed: At(14));
        Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Missing>(Selector.Select(Frame(setup, At(13, 33), stop)));
        Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Unique>(Selector.Select(Frame(setup, At(14), stop)));
        var future = Stop(setup, observed: At(17));
        Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Missing>(Selector.Select(Frame(setup, At(14), future)));
        Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Missing>(Selector.Select(Frame(setup, At(17), future)));
    }

    [Fact]
    public void CompatibleDuplicatesPreserveAllProvenanceAndImmutableSourceCopies()
    {
        var setup = BuildStopSetup();
        var sources = new[] { LiquidityFixture.Candle(8) };
        var reference = Reference(observed: At(13, 31), sources: sources);
        var first = Stop(setup, reference: reference);
        sources[0] = LiquidityFixture.Candle(7);
        Assert.Equal(LiquidityFixture.Candle(8).OpenTimeUtc, reference.SupportingCandles.Single().OpenTimeUtc);
        var second = Stop(setup, observed: At(14), source: "synthetic:another-review",
            reference: Reference(observed: At(13, 32), sources: [LiquidityFixture.Candle(8)], source: "synthetic:another-peak-review"));
        var selected = Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Unique>(Selector.Select(Frame(setup, null, second, first, first)));
        Assert.Equal(3, selected.SupportingObservations.Count);
        Assert.Contains(second, selected.SupportingObservations);
        Assert.Throws<NotSupportedException>(() => ((IList<Candle>)reference.SupportingCandles).Add(LiquidityFixture.Candle(7)));
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanWickfillScenario2StopLossObservation>)selected.SupportingObservations).Clear());
    }

    [Theory]
    [InlineData("price")]
    [InlineData("reference")]
    [InlineData("reference-price")]
    [InlineData("effective")]
    [InlineData("candles")]
    public void ConflictRetainsAlternativesWithoutRankingAndIsRepeatable(string difference)
    {
        var setup = BuildStopSetup(); var first = Stop(setup);
        var other = Stop(setup, price: difference == "price" ? 117 : 116,
            effective: difference == "effective" ? At(13, 30) : At(13, 31), source: "synthetic:alternative",
            reference: Reference(id: difference == "reference" ? "synthetic:other-peak" : "synthetic:peak",
                price: difference == "reference-price" ? 114 : 115,
                sources: difference == "candles" ? [LiquidityFixture.Candle(8)] : null));
        var forward = Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Conflict>(Selector.Select(Frame(setup, null, first, other)));
        var reverse = Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Conflict>(Selector.Select(Frame(setup, null, other, first)));
        Assert.Equal(forward.Alternatives.Select(o => o.SourceReference), reverse.Alternatives.Select(o => o.SourceReference));
        Assert.Equal(JsonSerializer.Serialize(forward), JsonSerializer.Serialize(Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Conflict>(Selector.Select(Frame(setup, null, first, other)))));
        Assert.Equal(2, forward.Alternatives.Count);
    }

    [Fact]
    public void UnresolvedNamedSourcesAreRetainedAndFinalOpenCandleHighCannotBeImportedEarly()
    {
        var setup = BuildStopSetup();
        var absent = new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, new(30, TimeframeUnit.Minute),
            At(13), At(13, 30), 100, 115, 90, 100, null);
        var stop = Stop(setup, reference: Reference(sources: [absent]));
        var missing = Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Missing>(Selector.Select(Frame(setup, null, stop)));
        Assert.Same(stop, Assert.Single(missing.UnavailableSourceObservations));
        var good = Stop(setup);
        var mixed = Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Unique>(Selector.Select(Frame(setup, null, good, stop)));
        Assert.Same(stop, Assert.Single(mixed.UnavailableSourceObservations));
        var future = new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, new(30, TimeframeUnit.Minute),
            At(13, 30), At(14), 100, 115, 90, 100, null);
        Assert.Throws<ArgumentException>(() => Reference(sources: [future]));
        Assert.Throws<ArgumentException>(() => Reference(sources: [LiquidityFixture.Candle(8), LiquidityFixture.Candle(8)]));
    }

    [Fact]
    public void LatestNonPassedEligibilityAndCurrentConflictingReviewBlockOlderEvidence()
    {
        var setup = BuildStopSetup(); var stop = Stop(setup);
        var raw = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll()).Execute(LiquidityFixture.Definition, Frame(setup));
        var altered = raw.Evaluations.Select(e => e.RuleId.Value == "NQ-WF2-002"
            ? new RuleEvaluation(e.RuleId, e.DefinitionStatus, RuleEvaluationResult.Waiting, e.Sequence, e.IsRequired, "synthetic blocked", e.EvaluatedAtUtc, null) : e);
        var blocked = new StrategyReplayContextObservation(raw.StrategyId, raw.StrategyVersion, raw.ProviderId, raw.Symbol, raw.Step,
            raw.AsOfUtc, altered);
        var later = FrameAfter14(setup, stop).WithPriorObservations(setup.History.Append(blocked));
        Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Missing>(Selector.Select(later));
        var conflict = new NasdaqHumanBranchApplicabilityObservation(stop.StartOwner, NasdaqStrategyPath.Standard,
            At(13, 30), At(14), "synthetic:conflicting-path");
        Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Missing>(Selector.Select(Frame(setup, null, stop, conflict)));
    }


    private static StrategyReplayContext FrameAfter14(Setup setup, params IStrategyReplayInputObservation[] extra)
    {
        var current = Frame(setup); var m1 = new Timeframe(1, TimeframeUnit.Minute);
        var series = current.AvailableTimeframes.Select(t =>
        {
            Assert.True(current.TryGetFrame(t, out var frame));
            var candles = frame!.AvailableCandles.AsEnumerable();
            if (t == m1) candles = candles.Append(new Candle(current.ProviderId, current.Symbol, m1, At(14), At(14, 1), 100, 110, 90, 100, null));
            return new CandleSeries(current.ProviderId, current.Symbol, t, candles);
        });
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == At(14, 1)) return new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition,
                frame, setup.Inputs.Concat(extra)).WithPriorObservations(setup.History);
        throw new InvalidOperationException("Synthetic later frame missing.");
    }

    [Fact]
    public void CurrentReversalConflictAndUnavailableContextCannotReusePriorEligibility()
    {
        var setup = BuildStopSetup(); var stop = Stop(setup);
        var raw = stop.PreEntryEligibility.Reversal.Reversal.Fact;
        var conflict = new NasdaqHumanWickfillScenario2PostSweepReversalObservation(raw.OpeningSweep,
            "synthetic:other-reversal", raw.EffectiveAtUtc, At(14), "synthetic:conflicting-turn");
        Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Missing>(Selector.Select(Frame(setup, null, stop, conflict)));
        var inputs = setup.Inputs.Where(o => o is not NasdaqHumanWickfillScenario2ContextObservation).Append(stop).ToArray();
        var noContext = F.Context(At(14), inputs).WithPriorObservations(setup.History);
        Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Missing>(Selector.Select(noContext));
    }

    [Fact]
    public void CurrentTerminationAndForeignCanonicalReversalCannotExposeTheEvidence()
    {
        var setup = BuildStopSetup(); var stop = Stop(setup); var owner = setup.Owner;
        var ended = new StrategyReplayLifecycleSnapshot(owner.StrategyId, owner.StrategyVersion, owner.ProviderId, owner.Symbol,
            owner.Step, owner.AsOfUtc, [], []);
        var replacement = new StrategyReplayContextObservation(owner.StrategyId, owner.StrategyVersion, owner.ProviderId, owner.Symbol,
            owner.Step, owner.AsOfUtc, owner.Evaluations, owner.WorkflowProgression, ended, ruleFacts: owner.RuleFacts);
        var terminated = Frame(setup, null, stop).WithPriorObservations(setup.History.Take(setup.History.Length - 1).Append(replacement));
        Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Missing>(Selector.Select(terminated));
        var foreign = BuildStopSetup();
        var poisonedHistory = setup.History.Select(o => o.AsOfUtc == At(13, 32)
            ? new StrategyReplayContextObservation(o.StrategyId, o.StrategyVersion, o.ProviderId, o.Symbol, o.Step, o.AsOfUtc,
                o.Evaluations, o.WorkflowProgression, o.LifecycleProgression,
                ruleFacts: foreign.History.Single(other => other.AsOfUtc == o.AsOfUtc).RuleFacts) : o);
        Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Missing>(Selector.Select(
            Frame(setup, null, stop).WithPriorObservations(poisonedHistory)));
    }


    [Theory]
    [InlineData("strategy")]
    [InlineData("version")]
    [InlineData("provider")]
    [InlineData("symbol")]
    public void CanonicalIdentityGuardsRejectForeignHistoryAndDoNotExposeItsInputs(string field)
    {
        var setup = BuildStopSetup(); var stop = Stop(setup); var current = Frame(setup);
        var provider = field == "provider" ? new MarketDataProviderId("synthetic-foreign") : current.ProviderId;
        var symbol = field == "symbol" ? new MarketSymbol("synthetic-foreign") : current.Symbol;
        var definition = new StrategyDefinition(field == "strategy" ? new("synthetic-foreign") : current.StrategyId,
            field == "version" ? new("synthetic-foreign") : current.StrategyVersion,
            "Synthetic identity test", "synthetic:definition", LiquidityFixture.Definition.Rules);
        var series = current.AvailableTimeframes.Select(t =>
        {
            Assert.True(current.TryGetFrame(t, out var frame));
            return new CandleSeries(provider, symbol, t, frame!.AvailableCandles.Select(c =>
                new Candle(provider, symbol, t, c.OpenTimeUtc, c.CloseTimeUtc, c.Open, c.High, c.Low, c.Close, null)));
        });
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        while (cursor.TryAdvance(out var frame))
        {
            if (frame!.AsOfUtc != current.AsOfUtc) continue;
            Assert.Throws<ArgumentException>(() => new CreateStrategyReplayContextUseCase().Execute(definition, frame, [stop]));
            var foreign = new CreateStrategyReplayContextUseCase().Execute(definition, frame);
            Assert.Throws<ArgumentException>(() => foreign.WithPriorObservations(setup.History));
            Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Missing>(Selector.Select(foreign));
            return;
        }
        throw new InvalidOperationException("Synthetic identity frame missing.");
    }

    [Fact]
    public void EligibleWithoutSLStaysMissingAndCanonicalLiquidityInvalidationBlocksLaterSelection()
    {
        var setup = BuildStopSetup(); var stop = Stop(setup);
        Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Missing>(Selector.Select(Frame(setup)));
        var upstream = F.Context(At(13, 5), []);
        var reference = new NasdaqLiquidityTakeReference.SessionLevel(upstream,
            new MoneyWay.Application.Strategies.Nasdaq.Liquidity.NasdaqSessionLiquidityCalculator().Calculate(upstream),
            NasdaqLiquidityTakeReference.SessionEndpoint.LondonLow, upstream.AsOfUtc, "synthetic:opposite-level");
        var opposite = new NasdaqHumanLiquidityTakeObservation(reference, new NasdaqHumanLiquidityTakeEvent.Documented(
            LiquidityFixture.Provider, LiquidityFixture.Symbol, "synthetic:opposite", reference.ReferencePrice - 1, At(14), "synthetic:opposite-event"),
            upstream.AsOfUtc, At(14), "synthetic:opposite-review");
        var root = stop.PreEntryEligibility.Reversal.Applicability.Fact.InitialTake.Take;
        var selected = new NasdaqHumanRelevantLiquidityTakeObservation(opposite, At(14), "synthetic:opposite-selected", root);
        var terminalContext = Frame(setup, null, stop, opposite, selected);
        // Raw inputs do not constitute a canonical terminal fact. Selection is not rule evaluation.
        Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Unique>(Selector.Select(terminalContext));
        var raw = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll()).Execute(LiquidityFixture.Definition, terminalContext);
        Assert.Equal(RuleEvaluationResult.Failed, raw.Evaluations.Single(e => e.RuleId.Value == "NQ-WF2-002").Result);
        var advanced = new AdvanceStrategyReplayLifecycleUseCase(new()).Execute(MoneyWayReplayWorkflowDefinitions.GetAll().Single(),
            new MoneyWayNasdaqReplayLifecyclePolicy(), raw, setup.Owner.LifecycleProgression);
        var terminal = raw.WithProgressions(advanced.WorkflowProgression, advanced.LifecycleProgression);
        Assert.Null(terminal.LifecycleProgression!.ActiveInstance);
        var later = FrameAfter14(setup, stop, opposite, selected).WithPriorObservations(setup.History.Append(terminal));
        Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Missing>(Selector.Select(later));
    }

    [Fact]
    public void FutureTailCannotChangeEarlierSelectionAndPrimitiveCannotAlterStandardOrSLResults()
    {
        var setup = BuildStopSetup(); var stop = Stop(setup); var context = Frame(setup, null, stop);
        var series = context.AvailableTimeframes.Select(t =>
        {
            Assert.True(context.TryGetFrame(t, out var frame));
            var last = frame!.AvailableCandles.Last();
            var future = new Candle(last.ProviderId, last.Symbol, t, At(18), At(18).Add(last.CloseTimeUtc - last.OpenTimeUtc), 100, 120, 80, 100, null);
            return new CandleSeries(last.ProviderId, last.Symbol, t, frame.AvailableCandles.Append(future));
        });
        var cursor = new MultiTimeframeCandleReplayCursor(series); StrategyReplayContext? expanded = null;
        while (cursor.TryAdvance(out var frame))
        {
            if (frame!.AsOfUtc == context.AsOfUtc)
            {
                expanded = new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition, frame,
                    setup.Inputs.Append(stop)).WithPriorObservations(setup.History);
                break;
            }
        }
        Assert.NotNull(expanded);
        var selected = Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Unique>(Selector.Select(context));
        var expandedSelection = Assert.IsType<NasdaqHumanWickfillScenario2StopLossSelection.Unique>(Selector.Select(expanded!));
        Assert.Equal(JsonSerializer.Serialize(selected), JsonSerializer.Serialize(expandedSelection));
        var before = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll()).Execute(LiquidityFixture.Definition, Frame(setup));
        var after = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll()).Execute(LiquidityFixture.Definition, context);
        Assert.Equal(JsonSerializer.Serialize(before.Evaluations), JsonSerializer.Serialize(after.Evaluations));
        Assert.Equal(JsonSerializer.Serialize(before.RuleFacts), JsonSerializer.Serialize(after.RuleFacts));
        Assert.Equal(RuleEvaluationResult.Waiting, after.Evaluations.Single(e => e.RuleId.Value == "NQ-SL-001").Result);
        Assert.DoesNotContain(after.RuleFacts, f => f.Fact is NasdaqHumanStructuralStopLossRuleFact);
    }
}

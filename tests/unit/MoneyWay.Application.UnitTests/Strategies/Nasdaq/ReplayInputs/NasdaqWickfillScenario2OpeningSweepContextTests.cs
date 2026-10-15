using System.Text.Json;
using MoneyWay.Application.Backtesting;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqWickfillScenario2OpeningSweepContextTests
{
    private static readonly Timeframe M1 = new(1, TimeframeUnit.Minute);
    private static DateTimeOffset At(int hour, int minute = 0) => LiquidityFixture.At(hour).AddMinutes(minute);

    [Fact]
    public void CanonicalLondonHighAndUniqueContextComposeWithoutCopyingEvidenceOrPrices()
    {
        var f = Build();
        var result = Join(f);
        Assert.Same(f.Selection, result.PreOpenContext);
        Assert.Same(f.Owner, result.TakeObservation);
        Assert.Same(Take(f.Owner), result.Take);
        Assert.Same(result.Take.Take.Reference, result.TakeSelection.Fact.Reference);
        Assert.Equal(f.Selection.Fact.Session, result.Session);
        Assert.Equal(f.Selection.Fact.H4Context.Fact, result.CanonicalH4Context.Selection.Fact);
        Assert.Equal(At(13, 31), result.EffectiveAtUtc);
        Assert.Equal(f.Owner.AsOfUtc, result.ObservedAtUtc);
        Assert.Equal("review:pre-open", result.PreOpenContext.Fact.SourceReference);
        Assert.Equal("review:h4", result.CanonicalH4Context.Selection.SupportingObservations[0].SourceReference);
        Assert.Equal("review:take", result.Take.Take.SourceReference);
        Assert.Equal("review:endpoint", Assert.IsType<NasdaqLiquidityTakeReference.SessionLevel>(result.Take.Take.Reference).SourceReference);
        Assert.Equal("review:event", Assert.IsType<NasdaqHumanLiquidityTakeEvent.Documented>(result.Take.Take.Event).SourceReference);
        Assert.Contains("review:relevant", result.TakeObservation.Evaluations.Single(e => e.RuleId.Value == "NQ-LIQ-003").EvidenceReference);
        Assert.All(typeof(NasdaqWickfillScenario2OpeningSweepContext).GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.Empty(typeof(NasdaqWickfillScenario2OpeningSweepContext).GetConstructors());
    }

    [Fact]
    public void AlreadyEmittedCanonicalOwnerCanBeComposedAtItsOwnFrameWithoutPropagatingAnyRule()
    {
        var f = Build();
        var context = Context(f.Owner.AsOfUtc, f.Inputs).WithPriorObservations(f.Context.PriorObservations.Where(o => o.AsOfUtc < f.Owner.AsOfUtc));
        var result = NasdaqWickfillScenario2OpeningSweepContext.From(context, Select(context), f.Owner);
        Assert.Equal(f.Owner.AsOfUtc, result.ObservedAtUtc);
        Assert.Same(f.Owner, result.TakeObservation);
        Assert.DoesNotContain(f.Owner.RuleFacts, r => r.RuleId.Value == "NQ-M1-003");
    }

    [Theory]
    [InlineData(NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh, NasdaqHumanH4PermittedDirection.Sell)]
    [InlineData(NasdaqLiquidityTakeReference.SessionEndpoint.AsiaLow, NasdaqHumanH4PermittedDirection.Buy)]
    public void SuccessfulCanonicalAsiaEndpointDoesNotQualify(
        NasdaqLiquidityTakeReference.SessionEndpoint endpoint, NasdaqHumanH4PermittedDirection direction)
    {
        var f = Build(endpoint: endpoint, direction: direction);
        Assert.Equal(RuleEvaluationResult.Passed, Result(f.Owner));
        Assert.Contains("Only a reviewed LondonHigh or LondonLow", Assert.Throws<ArgumentException>(() => Join(f)).Message);
    }

    [Fact]
    public void SuccessfulCanonicalStructuralTakeDoesNotQualify()
    {
        var f = Build(structural: true);
        Assert.Equal(RuleEvaluationResult.Passed, Result(f.Owner));
        Assert.IsType<NasdaqLiquidityTakeReference.Structural>(Take(f.Owner).Take.Reference);
        Assert.Contains("Only a reviewed LondonHigh or LondonLow", Assert.Throws<ArgumentException>(() => Join(f)).Message);
    }

    [Fact]
    public void LondonLowRemainsAnAdmissibleEndpointButCurrentSellOnlyContextCannotOverrideCanonicalAlignment()
    {
        var sell = Build(endpoint: NasdaqLiquidityTakeReference.SessionEndpoint.LondonLow);
        Assert.Equal(RuleEvaluationResult.Failed, Result(sell.Owner));
        Assert.True(Take(sell.Owner).IsSessionInvalidated);
        Assert.Throws<ArgumentException>(() => Join(sell));
        var buy = Build(endpoint: NasdaqLiquidityTakeReference.SessionEndpoint.LondonLow, direction: NasdaqHumanH4PermittedDirection.Buy);
        Assert.Equal(RuleEvaluationResult.Passed, Result(buy.Owner));
        Assert.Contains("direction must match", Assert.Throws<ArgumentException>(() => Join(buy)).Message);
        // No positive BUY Scenario 2 observation is invented to make this endpoint pass.
    }

    [Fact]
    public void ASelectionFromAnotherExactSessionIsRejected()
    {
        var current = Build(); var other = Build(shift: 24);
        Assert.NotEqual(current.Selection.Fact.Session, other.Selection.Fact.Session);
        Assert.Throws<ArgumentException>(() => NasdaqWickfillScenario2OpeningSweepContext.From(current.Context, other.Selection, current.Owner));
        Assert.Throws<ArgumentException>(() => NasdaqWickfillScenario2OpeningSweepContext.From(current.Context, current.Selection, other.Owner));
    }

    [Fact]
    public void SameDirectionAndSessionDoNotSubstituteForExactH4Ancestry()
    {
        var f = Build();
        var alternate = PreOpen(H4(upper: 121));
        var context = Context(At(14), f.Inputs.Where(o => o is not NasdaqHumanWickfillScenario2ContextObservation).Append(alternate).ToArray())
            .WithPriorObservations(f.Context.PriorObservations);
        var selection = Select(context);
        Assert.Equal(f.Selection.Fact.Session, selection.Fact.Session);
        Assert.Equal(f.Selection.Fact.Direction, selection.Fact.Direction);
        Assert.Contains("canonical H4 fact", Assert.Throws<ArgumentException>(() =>
            NasdaqWickfillScenario2OpeningSweepContext.From(context, selection, f.Owner)).Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrConflictingCurrentContextCannotReuseAnEarlierUniqueSelection(bool conflict)
    {
        var f = Build();
        var inputs = conflict ? f.Inputs.Append(PreOpen(H4(upper: 121))).ToArray()
            : f.Inputs.Where(o => o is not NasdaqHumanWickfillScenario2ContextObservation).ToArray();
        var context = Context(At(14), inputs).WithPriorObservations(f.Context.PriorObservations);
        Assert.Throws<ArgumentException>(() => NasdaqWickfillScenario2OpeningSweepContext.From(context, f.Selection, f.Owner));
    }

    [Fact]
    public void ACanonicalFactWithoutItsPassedEligibleOwnerIsInsufficient()
    {
        var f = Build();
        var raw = new StrategyReplayContextObservation(f.Owner.StrategyId, f.Owner.StrategyVersion, f.Owner.ProviderId,
            f.Owner.Symbol, f.Owner.Step, f.Owner.AsOfUtc, f.Owner.Evaluations, ruleFacts: f.Owner.RuleFacts);
        var context = ReplaceOwner(f, raw);
        Assert.Equal(RuleEvaluationResult.Passed, Result(raw));
        Assert.Throws<ArgumentException>(() => NasdaqWickfillScenario2OpeningSweepContext.From(context, f.Selection, raw));
        var failedEvaluations = f.Owner.Evaluations.Select(e => e.RuleId.Value == "NQ-LIQ-003"
            ? new RuleEvaluation(e.RuleId, e.DefinitionStatus, RuleEvaluationResult.Waiting, e.Sequence, e.IsRequired, e.Reason, e.EvaluatedAtUtc, e.EvidenceReference) : e);
        var notPassed = new StrategyReplayContextObservation(raw.StrategyId, raw.StrategyVersion, raw.ProviderId, raw.Symbol,
            raw.Step, raw.AsOfUtc, failedEvaluations, ruleFacts: raw.RuleFacts);
        Assert.Throws<ArgumentException>(() => NasdaqWickfillScenario2OpeningSweepContext.From(ReplaceOwner(f, notPassed), f.Selection, notPassed));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EqualityOrReversedEffectiveOrderCannotProduceAUsableCanonicalComposition(int minuteOffset)
    {
        // Both instants are pre-open. The real canonical evaluator refuses the take, so no successful fact can
        // be fabricated just to reach a redundant strict-order guard in the composition.
        var f = Build(takeEffective: At(13, 10).AddMinutes(minuteOffset));
        Assert.True(f.Selection.Fact.EffectiveAtUtc >= f.Inputs.OfType<NasdaqHumanLiquidityTakeObservation>().Single().EffectiveAtUtc);
        Assert.NotEqual(RuleEvaluationResult.Passed, Result(f.Owner));
        Assert.Throws<ArgumentException>(() => Join(f));
    }

    [Fact]
    public void LateScenarioAssertionIsNotBackfilledAndCompositionAvailabilityIncludesCanonicalEmission()
    {
        var f = Build(preOpenObserved: At(14));
        var result = Join(f);
        Assert.Equal(At(14), result.ObservedAtUtc);
        Assert.Equal(At(13, 31), result.EffectiveAtUtc);
        var earlier = Context(At(13, 31), f.Inputs).WithPriorObservations(f.Context.PriorObservations.Where(o => o.AsOfUtc < At(13, 31)));
        Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Missing>(new NasdaqHumanWickfillScenario2ContextObservationSelector().Select(earlier, f.Selection.Fact.Session));
        Assert.Throws<ArgumentException>(() => NasdaqWickfillScenario2OpeningSweepContext.From(earlier, f.Selection, f.Owner));
    }

    [Fact]
    public void EquivalentSelectedContextUsesTypedValuesRatherThanObjectAddresses()
    {
        var f = Build(); var reconstructed = Build();
        Assert.NotSame(f.Selection.Fact, reconstructed.Selection.Fact);
        var result = NasdaqWickfillScenario2OpeningSweepContext.From(f.Context, reconstructed.Selection, f.Owner);
        Assert.Same(reconstructed.Selection, result.PreOpenContext);
        Assert.Equal(f.Selection.Fact.H4Context.Fact, result.CanonicalH4Context.Selection.Fact);
    }

    [Fact]
    public void FutureUnavailableDiagnosticsCannotBeSmuggledThroughAnOtherwiseIdenticalUniqueSelection()
    {
        var f = Build();
        var source = new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, new(5, TimeframeUnit.Minute),
            At(13, 5), At(13, 10), 100, 110, 90, 100, null);
        var observation = new NasdaqHumanWickfillScenario2ContextObservation(f.Selection.Fact.H4Context,
            f.Selection.Fact.ApplicableZone, [.. f.Selection.Fact.ReviewedSourceCandles, source], At(13, 10), At(14), "review:late missing source");
        var later = Context(At(14), [.. f.Inputs, observation]).WithPriorObservations(f.Context.PriorObservations);
        var futureSelection = Select(later);
        Assert.Single(futureSelection.UnavailableSourceObservations);
        var earlier = Context(f.Owner.AsOfUtc, f.Inputs).WithPriorObservations(f.Context.PriorObservations.Where(o => o.AsOfUtc < f.Owner.AsOfUtc));
        Assert.Throws<ArgumentException>(() => NasdaqWickfillScenario2OpeningSweepContext.From(earlier, futureSelection, f.Owner));
    }

    [Fact]
    public void LateCompatibleTakeSupportIsRetainedWithoutBackdatingItsAvailability()
    {
        var f = Build(); var take = Take(f.Owner).Take;
        var duplicate = new NasdaqHumanLiquidityTakeObservation(take.Reference, take.Event, take.ReferenceEligibleAtUtc, At(14), "review:late duplicate");
        var context = Context(At(14), [.. f.Inputs, duplicate]).WithPriorObservations(f.Context.PriorObservations);
        var result = NasdaqWickfillScenario2OpeningSweepContext.From(context, Select(context), f.Owner);
        Assert.Equal(2, result.TakeSelection.SupportingObservations.Count);
        Assert.Contains(duplicate, result.TakeSelection.SupportingObservations);
        Assert.Equal(At(14), result.ObservedAtUtc);
    }

    [Fact]
    public void MissingOrAlteredSourcesAndFutureOwnerAreNotAccepted()
    {
        var f = Build();
        foreach (var altered in new[] { false, true })
        {
            var candles = Sources().Where(c => altered || c.Timeframe != new Timeframe(1, TimeframeUnit.Hour) || c.OpenTimeUtc != At(3)).Select(c =>
                altered && c.Timeframe == new Timeframe(1, TimeframeUnit.Hour) && c.OpenTimeUtc == At(8)
                    ? new Candle(c.ProviderId, c.Symbol, c.Timeframe, c.OpenTimeUtc, c.CloseTimeUtc, c.Open, c.High + 1, c.Low, c.Close, c.Volume) : c).ToArray();
            var context = Context(At(14), f.Inputs, sources: candles).WithPriorObservations(f.Context.PriorObservations);
            Assert.Throws<ArgumentException>(() => NasdaqWickfillScenario2OpeningSweepContext.From(context, Select(context), f.Owner));
        }
        var before = Context(At(13, 30), f.Inputs).WithPriorObservations(f.Context.PriorObservations.Where(o => o.AsOfUtc < At(13, 30)));
        Assert.Throws<ArgumentException>(() => NasdaqWickfillScenario2OpeningSweepContext.From(before, Select(before), f.Owner));
    }

    [Fact]
    public void JoinIsDeterministicAndDoesNotChangeCanonicalEvaluationOrWorkflow()
    {
        var f = Build(); var first = Join(f);
        var reversed = Context(At(14), f.Inputs.Reverse().ToArray()).WithPriorObservations(f.Context.PriorObservations);
        var second = NasdaqWickfillScenario2OpeningSweepContext.From(reversed, Select(reversed), f.Owner);
        Assert.Same(first.Take, second.Take);
        Assert.Equal(first.PreOpenContext.SupportingObservations, second.PreOpenContext.SupportingObservations);
        Assert.Equal(first.ObservedAtUtc, second.ObservedAtUtc);
        var evaluator = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll());
        var before = evaluator.Execute(LiquidityFixture.Definition, f.Context);
        var history = JsonSerializer.Serialize(f.Context.PriorObservations);
        Join(f);
        var after = evaluator.Execute(LiquidityFixture.Definition, f.Context);
        Assert.Equal(before.Evaluations, after.Evaluations);
        Assert.Equal(history, JsonSerializer.Serialize(f.Context.PriorObservations));
        Assert.DoesNotContain(after.RuleFacts, r => r.RuleId.Value == "NQ-M1-003");
        Assert.False((object)first is IReplayRuleFact);
        Assert.False((object)first is IStrategyReplayInputObservation);
    }

    [Fact]
    public void NullFactoryInputsAreRejected()
    {
        var f = Build();
        Assert.Throws<ArgumentNullException>(() => NasdaqWickfillScenario2OpeningSweepContext.From(null!, f.Selection, f.Owner));
        Assert.Throws<ArgumentNullException>(() => NasdaqWickfillScenario2OpeningSweepContext.From(f.Context, null!, f.Owner));
        Assert.Throws<ArgumentNullException>(() => NasdaqWickfillScenario2OpeningSweepContext.From(f.Context, f.Selection, null!));
    }

    private static NasdaqWickfillScenario2OpeningSweepContext Join(Fixture f) => NasdaqWickfillScenario2OpeningSweepContext.From(f.Context, f.Selection, f.Owner);
    private static NasdaqLiquidityTakeRuleFact Take(StrategyReplayContextObservation o) => o.RuleFacts.Select(f => f.Fact).OfType<NasdaqLiquidityTakeRuleFact>().Single();
    private static RuleEvaluationResult Result(StrategyReplayContextObservation o) => o.Evaluations.Single(e => e.RuleId.Value == "NQ-LIQ-003").Result;
    private static NasdaqHumanWickfillScenario2ContextSelection.Unique Select(StrategyReplayContext context) =>
        Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Unique>(new NasdaqHumanWickfillScenario2ContextObservationSelector().Select(context, LiquidityFixture.Session(context.AsOfUtc)));

    private static NasdaqHumanH4ContextObservation H4(int shift = 0, decimal upper = 120, NasdaqHumanH4PermittedDirection direction = NasdaqHumanH4PermittedDirection.Sell) =>
        new(LiquidityFixture.Session(At(13 + shift)), new(direction, NasdaqHumanH4ContextKind.Wickfill, At(8 + shift), At(12 + shift), [],
            [new(NasdaqHumanH4StructuralRole.LowerHigh, 100, upper, NasdaqHumanH4ZoneBoundaryPrecision.Approximate, [At(8 + shift)])]), At(13 + shift, 5), "review:h4");
    private static NasdaqHumanWickfillScenario2ContextObservation PreOpen(NasdaqHumanH4ContextObservation h4, int shift = 0, DateTimeOffset? observed = null) =>
        new(h4, h4.Fact.ZoneAnchors.Single(), Sources(shift).Where(c => c.Timeframe == NasdaqHumanH4ContextObservation.H4),
            At(13 + shift, 10), observed ?? At(13 + shift, 10), "review:pre-open");
    private static Candle[] Sources(int shift = 0) => Enumerable.Range(-2, 14).Select(h => LiquidityFixture.Candle(h + shift))
        .Concat(new[] { 0, 4, 8 }.Select(h => LiquidityFixture.Candle(h + shift, hours: 4))).ToArray();
    private static DateTimeOffset[] Boundaries(int shift = 0) => [At(13 + shift, 5), At(13 + shift, 10), At(13 + shift, 15), At(13 + shift, 30), At(13 + shift, 31), At(14 + shift)];
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
        DateTimeOffset? preOpenObserved = null, DateTimeOffset? takeEffective = null)
    {
        var h4 = H4(shift, direction: direction);
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
            LiquidityFixture.Provider, LiquidityFixture.Symbol, "synthetic:opening", reference.ReferencePrice + (reference.Side == NasdaqStructuralLiquiditySide.High ? 1 : -1),
            takeEffective ?? At(13 + shift, 31), "review:event"), upstream.AsOfUtc, At(13 + shift, 31), "review:take");
        inputs.Add(take); inputs.Add(new NasdaqHumanRelevantLiquidityTakeObservation(take, take.ObservedAtUtc, "review:relevant"));
        var definitions = new StrategyDefinitionCatalog().GetAll();
        var run = new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(MoneyWayReplayRuleEvaluators.GetAll()),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), new()).Execute(LiquidityFixture.Definition, Series(shift), inputs);
        var owner = run.StrategyObservations.Single(o => o.AsOfUtc == At(13 + shift, 31));
        var context = Context(At(14 + shift), inputs.ToArray(), shift).WithPriorObservations(run.StrategyObservations.Where(o => o.AsOfUtc < At(14 + shift)));
        return new(context, Select(context), owner, inputs.ToArray());
    }

    private static StrategyReplayContext ReplaceOwner(Fixture f, StrategyReplayContextObservation owner) =>
        f.Context.WithPriorObservations(f.Context.PriorObservations.Select(o => ReferenceEquals(o, f.Owner) ? owner : o));
    private sealed record Fixture(StrategyReplayContext Context, NasdaqHumanWickfillScenario2ContextSelection.Unique Selection,
        StrategyReplayContextObservation Owner, IStrategyReplayInputObservation[] Inputs);
}
